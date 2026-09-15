using Godot;
using Game.Board;

namespace Game.Tests
{
    public class MiniStepTests
    {
        static MiniStep NextDoor() =>
            new MiniStep(new Vector3(-0.03f, 0f, 0.09f), new Vector3(0.03f, 0f, 0.09f));

        static MiniStep CornerToCorner() =>
            new MiniStep(new Vector3(-0.21f, 0f, -0.21f), new Vector3(0.21f, 0f, 0.21f));

        static Vector3 Flat(Vector3 v) => new Vector3(v.X, 0f, v.Z);


        [Fact]
        public void ItStartsWhereThePieceIs()
        {
            MiniStep step = NextDoor();

            Assert.Equal(step.From, step.At(0f));
            Assert.Equal(step.From, step.At(-1f));
        }

        // the destination is returned, not integrated toward, so a long session can't drift a piece off its square
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


        [Fact]
        public void ItLeavesTheSquareOnlyAfterItHasLifted()
        {
            MiniStep step = NextDoor();

            for (float t = 0f; t < step.LiftEnds; t += 0.01f)
                Assert.Equal(Flat(step.From), Flat(step.At(t)));
        }

        [Fact]
        public void ItIsOverItsDestinationForTheWholeHoverAndSetDown()
        {
            MiniStep step = CornerToCorner();

            for (float t = step.SlideEnds; t <= step.Duration; t += 0.005f)
                Assert.Equal(Flat(step.To), Flat(step.At(t)));
        }

        [Fact]
        public void ItHolds_Still_BeforeItCommits()
        {
            MiniStep step = NextDoor();

            float held = step.SlideEnds + MiniStep.HoverSeconds * MiniStep.HoverRiseShare;

            Vector3 first = step.At(held + 0.001f);
            Vector3 last = step.At(step.HoverEnds - 0.001f);

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

        [Fact]
        public void ThePathIsContinuous()
        {
            MiniStep step = CornerToCorner();

            Vector3 previous = step.At(0f);

            for (float t = 0f; t <= step.Duration + 0.05f; t += 0.004f)
            {
                Vector3 now = step.At(t);

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


        static Vector3[] RoundAWall() => new[]
        {
            new Vector3(-0.15f, 0f, 0.09f),
            new Vector3(-0.09f, 0f, 0.03f),
            new Vector3(-0.03f, 0f, -0.03f),
            new Vector3(0.03f, 0f, -0.03f),
            new Vector3(0.09f, 0f, 0.03f),
        };

        static float OffTheRoute(Vector3 point, Vector3[] through)
        {
            float nearest = float.MaxValue;

            for (int i = 1; i < through.Length; i++)
            {
                Vector3 a = Flat(through[i - 1]);
                Vector3 b = Flat(through[i]);
                Vector3 leg = b - a;

                float along = leg.LengthSquared() <= 0f
                    ? 0f
                    : Mathf.Clamp((Flat(point) - a).Dot(leg) / leg.LengthSquared(), 0f, 1f);

                nearest = Mathf.Min(nearest, (Flat(point) - (a + leg * along)).Length());
            }

            return nearest;
        }

        [Fact]
        public void ARouteStartsAndEndsOnItsOwnSquares()
        {
            Vector3[] through = RoundAWall();
            var step = new MiniStep(through);

            Assert.Equal(through[0], step.At(0f));
            Assert.Equal(through[through.Length - 1], step.At(step.Duration));
            Assert.Equal(5, step.Waypoints);
        }

        // easing between the two ends would cut the corner and walk the piece through the wall it routed around
        [Fact]
        public void ThePieceStaysOnTheRoute()
        {
            Vector3[] through = RoundAWall();
            var step = new MiniStep(through);

            for (float t = 0f; t <= step.Duration; t += 0.004f)
                Assert.True(OffTheRoute(step.At(t), through) < 0.0005f,
                            $"it left the route at {t}s, by {OffTheRoute(step.At(t), through):0.0000}m");
        }

        [Fact]
        public void ItPassesThroughEveryWaypointInOrder()
        {
            Vector3[] through = RoundAWall();
            var step = new MiniStep(through);

            int reached = 0;

            for (float t = 0f; t <= step.Duration && reached < through.Length; t += 0.001f)
                if (Flat(step.At(t) - through[reached]).Length() < 0.003f)
                    reached++;

            Assert.Equal(through.Length, reached);
        }

        [Fact]
        public void TheLongWayRoundTakesLonger()
        {
            Vector3[] through = RoundAWall();

            var round = new MiniStep(through);
            var straight = new MiniStep(through[0], through[through.Length - 1]);

            Assert.True(round.SlideSeconds > straight.SlideSeconds);
        }

        [Fact]
        public void AWholeRouteIsSetDownOnce()
        {
            var step = new MiniStep(RoundAWall());

            int clicks = 0;
            float was = 0f;

            for (float t = 0f; t <= step.Duration + 0.5f; t += 0.016f)
            {
                if (step.SetsDownBetween(was, t)) clicks++;
                was = t;
            }

            Assert.Equal(1, clicks);
        }

        [Fact]
        public void ARouteOfNowhereIsNotACrash()
        {
            var nothing = new MiniStep(new Vector3[0]);

            Assert.Equal(Vector3.Zero, nothing.At(0f));
            Assert.Equal(Vector3.Zero, nothing.At(nothing.Duration));

            var one = new MiniStep(new[] { new Vector3(0.1f, 0f, 0.2f) });

            Assert.Equal(one.From, one.At(one.Duration));
        }


        static readonly Vector3 Here = new Vector3(-0.03f, 0f, 0.09f);

        static readonly Vector3 Denied = new Vector3(0.15f, 0f, -0.21f);

        // a refusal is a path that ends where it began
        [Fact]
        public void ARefusedMoveEndsExactlyWhereItStarted()
        {
            MiniStep step = MiniStep.Refusing(Here, Denied);

            Assert.Equal(Here, step.At(0f));
            Assert.Equal(Here, step.At(step.Duration));
            Assert.Equal(Here, step.At(step.Duration + 5f));
        }

        [Fact]
        public void ItLeansAtTheSquareItCannotHave()
        {
            MiniStep step = MiniStep.Refusing(Here, Denied);

            Vector3 furthest = Here;

            for (float t = 0f; t <= step.Duration; t += 0.002f)
                if (Flat(step.At(t) - Here).Length() > Flat(furthest - Here).Length())
                    furthest = step.At(t);

            Assert.True(Flat(furthest - Here).Normalized().Dot(Flat(Denied - Here).Normalized()) > 0.99f);
            Assert.Equal(MiniStep.RefusalLean, Flat(furthest - Here).Length(), 3);
        }

        [Fact]
        public void ARefusalNeverReachesTheNextSquare()
        {
            MiniStep step = MiniStep.Refusing(Here, Denied);

            for (float t = 0f; t <= step.Duration; t += 0.002f)
                Assert.True(Flat(step.At(t) - Here).Length() < 0.03f);
        }

        [Fact]
        public void ARefusalIsSetBackDownOnce()
        {
            MiniStep step = MiniStep.Refusing(Here, Denied);

            int clicks = 0;
            float was = 0f;

            for (float t = 0f; t <= step.Duration + 0.5f; t += 0.016f)
            {
                if (step.SetsDownBetween(was, t)) clicks++;
                was = t;
            }

            Assert.Equal(1, clicks);
        }

        [Fact]
        public void ARefusalTowardNowhereStandsStill()
        {
            MiniStep step = MiniStep.Refusing(Here, Here);

            for (float t = 0f; t <= step.Duration; t += 0.01f)
            {
                Assert.Equal(Here.X, step.At(t).X, 5);
                Assert.Equal(Here.Z, step.At(t).Z, 5);
            }
        }


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
