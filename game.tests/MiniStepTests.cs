using Godot;
using Game.Board;

namespace Game.Tests
{
    // The move is the milestone, so the move is what is guarded. B1 is verified by eye - it has
    // to be, because "it should sound like a piece on a mat" is not a number - but three of its
    // claims are machine-checkable and they are the ones that would rot silently:
    //
    //   IT NEVER ENDS UP BETWEEN CELLS. the curve ends AT the destination, exactly
    //   IT NEVER SINKS THROUGH THE FELT. no phase dips below the surface it is travelling over
    //   IT HESITATES. there is a stretch where the piece is over its destination and NOT MOVING
    //
    // The third is the one worth having a test for at all. A hover that quietly turns into a
    // deceleration still looks fine in motion, reads as damping rather than deliberation, and
    // nobody would ever catch it by watching - it is only the difference between a piece being
    // placed and a piece coasting to a stop.
    //
    // Godot-free in the sense game.tests means: Vector3 is a managed struct.
    public class MiniStepTests
    {
        // a square's width apart, the ordinary case
        static MiniStep NextDoor() =>
            new MiniStep(new Vector3(-0.03f, 0f, 0.09f), new Vector3(0.03f, 0f, 0.09f));

        static MiniStep CornerToCorner() =>
            new MiniStep(new Vector3(-0.21f, 0f, -0.21f), new Vector3(0.21f, 0f, 0.21f));

        static Vector3 Flat(Vector3 v) => new Vector3(v.X, 0f, v.Z);

        // ---- the ends ----

        [Fact]
        public void ItStartsWhereThePieceIs()
        {
            MiniStep step = NextDoor();

            Assert.Equal(step.From, step.At(0f));
            Assert.Equal(step.From, step.At(-1f));
        }

        // the whole of "it never ends up between cells": the destination is RETURNED, not
        // integrated toward, so a long session cannot drift a piece off its square
        [Fact]
        public void ItEndsExactlyOnTheSquare()
        {
            MiniStep step = CornerToCorner();

            Assert.Equal(step.To, step.At(step.Duration));
            Assert.Equal(step.To, step.At(step.Duration + 10f));
        }

        [Fact]
        public void ItIsDoneOnlyWhenItIsDone()
        {
            MiniStep step = NextDoor();

            Assert.False(step.IsDone(step.Duration - 0.001f));
            Assert.True(step.IsDone(step.Duration));
        }

        // ---- how long it takes ----

        [Fact]
        public void FurtherTakesLonger()
        {
            Assert.True(CornerToCorner().Duration > NextDoor().Duration);
        }

        [Fact]
        public void ButNeverQuickerThanAMove_NorSlowerThanPatience()
        {
            var onTheSpot = new MiniStep(Vector3.Zero, Vector3.Zero);
            var absurd = new MiniStep(Vector3.Zero, new Vector3(50f, 0f, 0f));

            Assert.Equal(MiniStep.MinSlideSeconds, onTheSpot.SlideSeconds, 4);
            Assert.Equal(MiniStep.MaxSlideSeconds, absurd.SlideSeconds, 4);
        }

        // the lift is choreography and must not be charged for as distance
        [Fact]
        public void HeightDoesNotMakeAMoveTakeLonger()
        {
            var flat = new MiniStep(new Vector3(0f, 0f, 0f), new Vector3(0.3f, 0f, 0f));
            var lifted = new MiniStep(new Vector3(0f, 0.4f, 0f), new Vector3(0.3f, 0f, 0f));

            Assert.Equal(flat.SlideSeconds, lifted.SlideSeconds, 5);
        }

        [Fact]
        public void TheDurationIsItsPhasesAndNothingElse()
        {
            MiniStep step = NextDoor();

            Assert.Equal(MiniStep.LiftSeconds, step.LiftEnds, 5);
            Assert.Equal(step.LiftEnds + step.SlideSeconds, step.SlideEnds, 5);
            Assert.Equal(step.SlideEnds + MiniStep.HoverSeconds, step.HoverEnds, 5);
            Assert.Equal(step.HoverEnds + MiniStep.SetDownSeconds, step.Duration, 5);
        }

        // ---- what it does on the way ----

        [Fact]
        public void ItLeavesTheSquareOnlyAfterItHasLifted()
        {
            MiniStep step = NextDoor();

            for (float t = 0f; t < step.LiftEnds; t += 0.01f)
                Assert.Equal(Flat(step.From), Flat(step.At(t)));
        }

        // it commits over the destination and THEN comes down - the whole reason the hesitation
        // reads as deliberation rather than as a slow landing
        [Fact]
        public void ItIsOverItsDestinationForTheWholeHoverAndSetDown()
        {
            MiniStep step = CornerToCorner();

            for (float t = step.SlideEnds; t <= step.Duration; t += 0.005f)
                Assert.Equal(Flat(step.To), Flat(step.At(t)));
        }

        // THE HESITATION. a stretch where the piece is neither travelling nor descending
        [Fact]
        public void ItHolds_Still_BeforeItCommits()
        {
            MiniStep step = NextDoor();

            float held = step.SlideEnds + MiniStep.HoverSeconds * MiniStep.HoverRiseShare;

            Vector3 first = step.At(held + 0.001f);
            Vector3 last = step.At(step.HoverEnds - 0.001f);

            // long enough to read as a pause rather than as a frame of stillness
            Assert.True(step.HoverEnds - held > 0.08f);
            Assert.Equal(first.Y, last.Y, 5);
            Assert.Equal(Flat(first), Flat(last));
        }

        [Fact]
        public void ItIsHighestWhileItHesitates()
        {
            MiniStep step = CornerToCorner();

            float highest = 0f;
            float when = 0f;

            for (float t = 0f; t <= step.Duration; t += 0.002f)
                if (step.At(t).Y > highest)
                {
                    highest = step.At(t).Y;
                    when = t;
                }

            Assert.Equal(MiniStep.HoverHeight, highest, 4);
            Assert.InRange(when, step.SlideEnds, step.HoverEnds);
        }

        // it slides, it does not fly: THE_BOARD.md B1 says the piece slides across the felt, and
        // a piece carried a centimetre up is a hand moving a piece through the air
        [Fact]
        public void ItSlidesLowAcrossTheFelt()
        {
            MiniStep step = CornerToCorner();

            for (float t = step.LiftEnds; t <= step.SlideEnds; t += 0.005f)
                Assert.Equal(MiniStep.LiftHeight, step.At(t).Y, 5);

            Assert.True(MiniStep.LiftHeight < 0.005f);
        }

        [Fact]
        public void ItNeverSinksThroughTheFelt()
        {
            MiniStep step = CornerToCorner();

            for (float t = -0.5f; t <= step.Duration + 0.5f; t += 0.002f)
                Assert.True(step.At(t).Y >= 0f, $"below the felt at {t}s");
        }

        // a jump in the curve is a piece teleporting for one frame, which is exactly the thing
        // this whole class exists to prevent
        [Fact]
        public void ThePathIsContinuous()
        {
            MiniStep step = CornerToCorner();

            Vector3 previous = step.At(0f);

            for (float t = 0f; t <= step.Duration + 0.05f; t += 0.004f)
            {
                Vector3 now = step.At(t);

                // 4 ms of the fastest phase, with room to spare - the slide clamps at 0.9 s
                Assert.True((now - previous).Length() < 0.01f, $"jumped at {t}s");
                previous = now;
            }
        }

        [Fact]
        public void TheSetDownOnlyEverGoesDown()
        {
            MiniStep step = NextDoor();

            float previous = step.At(step.HoverEnds).Y;

            for (float t = step.HoverEnds; t <= step.Duration; t += 0.002f)
            {
                float y = step.At(t).Y;

                Assert.True(y <= previous + 0.00001f, $"rose again at {t}s");
                previous = y;
            }
        }

        // ---- the click ----

        [Fact]
        public void ItIsSetDownOnce()
        {
            MiniStep step = NextDoor();

            int clicks = 0;
            float was = 0f;

            for (float t = 0f; t <= step.Duration + 0.5f; t += 0.016f)
            {
                if (step.SetsDownBetween(was, t)) clicks++;
                was = t;
            }

            Assert.Equal(1, clicks);
        }

        // a frame that steps clean over the end of the move still has to make a noise - a piece
        // that lands silently every twentieth move is worse than one that never makes a sound,
        // because the second one gets fixed
        [Fact]
        public void ALongFrameDoesNotSwallowTheClick()
        {
            MiniStep step = NextDoor();

            Assert.True(step.SetsDownBetween(0f, step.Duration + 5f));
        }

        [Fact]
        public void ItIsNotSetDownBeforeItArrives()
        {
            MiniStep step = NextDoor();

            Assert.False(step.SetsDownBetween(0f, step.HoverEnds));
            Assert.False(step.SetsDownBetween(step.Duration, step.Duration + 1f));
        }
    }
}
