using Godot;
using Content.Places;
using Game.Board;

namespace Game.Tests
{
    public class MatSwapTests
    {
        static Vector3 Flat(Vector3 v) => new Vector3(v.X, 0f, v.Z);

        [Fact]
        public void ItStartsAndEndsWithAMatOnTheTable()
        {
            var swap = new MatSwap();

            Assert.Equal(Vector3.Zero, swap.At(0f));
            Assert.Equal(Vector3.Zero, swap.At(-1f));
            Assert.Equal(Vector3.Zero, swap.At(swap.Duration));
            Assert.Equal(Vector3.Zero, swap.At(swap.Duration + 10f));
        }

        [Fact]
        public void ItIsDoneOnlyWhenItIsDone()
        {
            var swap = new MatSwap();

            Assert.False(swap.IsDone(swap.Duration - 0.001f));
            Assert.True(swap.IsDone(swap.Duration));
        }

        [Fact]
        public void TheDurationIsItsPhasesAndNothingElse()
        {
            var swap = new MatSwap();

            Assert.Equal(MatSwap.LiftSeconds, swap.LiftEnds, 5);
            Assert.Equal(swap.LiftEnds + MatSwap.BareSeconds, swap.BareEnds, 5);
            Assert.Equal(swap.BareEnds + MatSwap.LaySeconds, swap.Duration, 5);
        }

        // the one property the whole swap exists for: the old place is gone before the new one
        // arrives, so two maps are never on the table at once
        [Fact]
        public void TheTableIsBareBetweenTheTwoMats()
        {
            var swap = new MatSwap();

            Assert.True(swap.Bare(swap.LiftEnds));
            Assert.True(swap.Bare(swap.BareEnds - 0.001f));

            Assert.False(swap.Bare(swap.LiftEnds - 0.001f));
            Assert.False(swap.Bare(swap.BareEnds));
        }

        [Fact]
        public void TheNextMapIsBuiltExactlyOnce()
        {
            var swap = new MatSwap();

            int built = 0;
            float was = 0f;

            for (float t = 0f; t <= swap.Duration + 0.5f; t += 0.016f)
            {
                if (swap.LaysBetween(was, t)) built++;
                was = t;
            }

            Assert.Equal(1, built);
        }

        [Fact]
        public void ALongFrameDoesNotSwallowTheHandOver()
        {
            var swap = new MatSwap();

            Assert.True(swap.LaysBetween(0f, swap.Duration + 5f));
        }

        [Fact]
        public void ItIsNotBuiltBeforeTheOldMatIsAway()
        {
            var swap = new MatSwap();

            Assert.False(swap.LaysBetween(0f, swap.LiftEnds - 0.001f));
            Assert.False(swap.LaysBetween(swap.LiftEnds, swap.Duration));
        }

        // the hand-over happens with the mat as far from the table as it ever gets, which is what
        // makes the new one appear from the DM's side rather than fade in over the old one
        [Fact]
        public void TheHandOverHappensWithTheMatFullyAway()
        {
            var swap = new MatSwap();

            Assert.Equal(-MatSwap.AwayDepth, swap.At(swap.LiftEnds).Z, 4);
            Assert.Equal(-MatSwap.AwayDepth, swap.At(swap.BareEnds - 0.001f).Z, 3);
        }

        [Fact]
        public void ItGoesTowardTheDmAndComesBackTheSameWay()
        {
            var swap = new MatSwap();

            for (float t = 0f; t <= swap.Duration; t += 0.005f)
                Assert.True(swap.At(t).Z <= 0.0001f, $"the mat came toward the player at {t}s");
        }

        [Fact]
        public void ItNeverSinksThroughTheTable()
        {
            var swap = new MatSwap();

            for (float t = -0.5f; t <= swap.Duration + 0.5f; t += 0.002f)
                Assert.True(swap.At(t).Y >= 0f, $"below the table at {t}s");
        }

        [Fact]
        public void ItLiftsClearOfThePiecesBeforeItTravels()
        {
            var swap = new MatSwap();

            // a tenth of the way out it is already most of the way up
            Vector3 early = swap.At(MatSwap.LiftSeconds * 0.25f);

            Assert.True(early.Y > MatSwap.LiftHeight * 0.3f);
            Assert.True(Mathf.Abs(early.Z) < MatSwap.AwayDepth * 0.5f);
        }

        [Fact]
        public void ThePathIsContinuous()
        {
            var swap = new MatSwap();

            Vector3 previous = swap.At(0f);

            for (float t = 0f; t <= swap.Duration + 0.05f; t += 0.004f)
            {
                Vector3 now = swap.At(t);

                Assert.True((now - previous).Length() < 0.02f, $"jumped at {t}s");
                previous = now;
            }
        }

        [Fact]
        public void ItTravelsOutThenBack()
        {
            var swap = new MatSwap();

            float furthest = 0f;
            float when = 0f;

            for (float t = 0f; t <= swap.Duration; t += 0.002f)
                if (Flat(swap.At(t)).Length() > furthest)
                {
                    furthest = Flat(swap.At(t)).Length();
                    when = t;
                }

            Assert.Equal(MatSwap.AwayDepth, furthest, 3);
            Assert.InRange(when, swap.LiftEnds, swap.BareEnds);
        }

        // a swap invents no gesture: the hands already lift things away and lay things down
        [Fact]
        public void ItUsesTwoGesturesTheHandsAlreadyHave()
        {
            Assert.Equal(Gesture.Withdraw, MatSwap.Lifts);
            Assert.Equal(Gesture.Place, MatSwap.Lays);

            Assert.True(Gestures.Has(MatSwap.Lifts.Word()));
            Assert.True(Gestures.Has(MatSwap.Lays.Word()));
        }

        // long enough to read as a pair of hands, short enough that a doorway is not a loading
        // screen. The numbers are tunable; that they are in that range is not
        [Fact]
        public void APlaceChangeIsABeat_NotAPause()
        {
            var swap = new MatSwap();

            Assert.InRange(swap.Duration, 0.5f, 1.5f);
        }
    }
}
