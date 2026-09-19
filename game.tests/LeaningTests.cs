using Godot;
using Game.Room;

namespace Game.Tests
{
    // LOOKING CLOSELY AT SOMETHING, AND LOOKING ACROSS A MAT BIGGER THAN THE TABLE.
    //
    // Two things, tested together because they are the two halves of "zoom is something you do as a
    // person at the table". Lean is the leaning; Looking is the pan and the zoom. Neither is a
    // control, and the properties below are what stops either becoming one - a lean always comes
    // back to where the table is seen from, and a pan can never take you off the edge of the mat.
    public class LeaningTests
    {
        // table.tscn's own camera: pitched 60 degrees down, back and above the table
        static Transform3D Overview() =>
            new Transform3D(new Basis(Vector3.Right, -Mathf.DegToRad(TableView.PitchDegrees)),
                            new Vector3(0.08f, 1.3423f, 0.805f));

        static readonly Vector3 OnTheMat = new Vector3(-0.36f, 0f, -0.03f);


        // ---- leaning --------------------------------------------------------------------------

        [Fact]
        public void ALeanStartsWhereTheEyeAlreadyIs()
        {
            Lean lean = Lean.Toward(Overview(), OnTheMat);

            Assert.Equal(Overview().Origin, lean.At(0f).Origin);
            Assert.Equal(Overview().Origin, lean.At(-1f).Origin);
        }

        [Fact]
        public void ItEndsExactlyWhereItWasGoing()
        {
            Lean lean = Lean.Toward(Overview(), OnTheMat);

            Assert.Equal(lean.To.Origin, lean.At(lean.Seconds).Origin);
            Assert.Equal(lean.To.Origin, lean.At(lean.Seconds + 10f).Origin);
        }

        [Fact]
        public void ItIsDoneOnlyWhenItIsDone()
        {
            Lean lean = Lean.Toward(Overview(), OnTheMat);

            Assert.False(lean.IsDone(lean.Seconds - 0.001f));
            Assert.True(lean.IsDone(lean.Seconds));
        }

        // THE ANGLE NEVER CHANGES. Keeping the basis is what makes this read as leaning in rather
        // than as orbiting, and it is also what centres the thing for free
        [Fact]
        public void LeaningInNeverChangesTheAngleTheTableIsSeenFrom()
        {
            Lean lean = Lean.Toward(Overview(), OnTheMat);

            for (float t = 0f; t <= lean.Seconds; t += 0.01f)
                Assert.True(lean.At(t).Basis.IsEqualApprox(Overview().Basis), $"it turned at {t}s");
        }

        [Fact]
        public void TheThingLeanedOverEndsUpDeadAhead()
        {
            Transform3D eye = Overview();

            Vector3 over = Lean.Over(eye, OnTheMat, Lean.Nearest);

            Vector3 looking = (-eye.Basis.Z).Normalized();

            // exactly Nearest away, and exactly along the way the eye is already looking
            Assert.Equal(Lean.Nearest, (OnTheMat - over).Length(), 4);
            Assert.True((OnTheMat - over).Normalized().Dot(looking) > 0.9999f);
        }

        [Fact]
        public void ItStopsShortOfTheThing_NeverInsideIt()
        {
            Lean lean = Lean.Toward(Overview(), OnTheMat);

            for (float t = 0f; t <= lean.Seconds; t += 0.005f)
                Assert.True((lean.At(t).Origin - OnTheMat).Length() >= Lean.Nearest - 0.0001f,
                            $"the eye was inside the thing at {t}s");
        }

        [Fact]
        public void LookingAwayGoesBackToTheTable()
        {
            Transform3D leaned = Lean.Toward(Overview(), OnTheMat).To;

            Lean back = Lean.Away(leaned, Overview());

            Assert.Equal(Overview().Origin, back.At(back.Seconds).Origin);
        }

        // you lean in deliberately and sit back without thinking about it
        [Fact]
        public void SittingBackIsQuickerThanLeaningIn()
        {
            Assert.True(Lean.OutSeconds < Lean.InSeconds);
        }

        [Fact]
        public void ThePathIsContinuous()
        {
            Lean lean = Lean.Toward(Overview(), OnTheMat);

            Vector3 previous = lean.At(0f).Origin;

            for (float t = 0f; t <= lean.Seconds + 0.05f; t += 0.004f)
            {
                Vector3 now = lean.At(t).Origin;

                Assert.True((now - previous).Length() < 0.05f, $"jumped at {t}s");
                previous = now;
            }
        }

        // A HANDHELD THING COMES UP IN YOUR HANDS and the camera does not move for it
        [Fact]
        public void SomethingPickedUpIsInFrontOfYourFaceAndBelowTheEyeLine()
        {
            Transform3D eye = Overview();

            Vector3 held = Lean.InHand(eye);

            Assert.True(held.Y < eye.Origin.Y, "it came up above the eye");
            Assert.True((held - eye.Origin).Length() < Lean.Held + Lean.HeldBelow + 0.001f);
            Assert.True((held - eye.Origin).Length() > 0.1f, "it is being held against your nose");
        }


        // ---- the mat's pan and zoom -------------------------------------------------------------

        // a dungeon room: the whole thing fits the table and there is nowhere to pan to
        static Looking ARoom() => new Looking(new Vector2(0.48f, 0.48f));

        // a town: bigger than the table, so you move your head across it
        static Looking ATown()
        {
            var looking = new Looking(new Vector2(1.44f, 1.20f));

            looking.ZoomTo(3f);

            return looking;
        }

        [Fact]
        public void AMatStartsPulledAllTheWayBack()
        {
            Looking looking = ARoom();

            Assert.Equal(Looking.Widest, looking.Zoom);
            Assert.Equal(Vector2.Zero, looking.Pan);
            Assert.Equal(looking.Mat, looking.Shown);
        }

        // A SINGLE ROOM FITS THE TABLE AT ONCE, so there is nothing to pan and nothing drifts
        [Fact]
        public void AMatThatFitsCannotBePanned()
        {
            Looking looking = ARoom();

            Assert.True(looking.Fits);

            looking.PanBy(new Vector2(10f, -10f));

            Assert.Equal(Vector2.Zero, looking.Pan);
        }

        [Fact]
        public void ZoomingInMakesThereSomewhereToPanTo()
        {
            Looking looking = ARoom();

            looking.ZoomTo(2f);

            Assert.False(looking.Fits);
            Assert.Equal(looking.Mat / 2f, looking.Shown);
        }

        [Fact]
        public void YouCanNeverLookPastTheEdgeOfTheMat()
        {
            Looking looking = ATown();

            looking.PanBy(new Vector2(99f, 99f));

            Assert.True(Mathf.Abs(looking.Pan.X) <= looking.Limit.X + 0.0001f);
            Assert.True(Mathf.Abs(looking.Pan.Y) <= looking.Limit.Y + 0.0001f);

            looking.PanBy(new Vector2(-99f, -99f));

            Assert.True(Mathf.Abs(looking.Pan.X) <= looking.Limit.X + 0.0001f);
            Assert.True(Mathf.Abs(looking.Pan.Y) <= looking.Limit.Y + 0.0001f);
        }

        // THE CASE THAT BITES: a window that was legal zoomed in hangs off the edge zoomed out
        [Fact]
        public void PullingBackBringsTheWindowHomeWithIt()
        {
            Looking looking = ATown();

            looking.PanBy(new Vector2(99f, 99f));

            Assert.True(looking.Pan.Length() > 0f);

            looking.ZoomTo(Looking.Widest);

            Assert.Equal(Vector2.Zero, looking.Pan);
        }

        [Fact]
        public void ZoomIsClampedAtBothEnds()
        {
            Looking looking = ATown();

            looking.ZoomTo(-40f);
            Assert.Equal(Looking.Widest, looking.Zoom);

            looking.ZoomTo(4000f);
            Assert.Equal(Looking.Closest, looking.Zoom);
        }

        [Fact]
        public void PullingAllTheWayBackShowsTheWholeMat()
        {
            Looking looking = ATown();

            looking.Back();

            Assert.Equal(looking.Mat, looking.Shown);
            Assert.True(looking.Fits);
        }

        // a mat with no size is a board that has not loaded, and it must not divide by anything
        [Fact]
        public void AMatOfNothingIsNotACrash()
        {
            var looking = new Looking(Vector2.Zero);

            looking.ZoomTo(Looking.Closest);
            looking.PanBy(new Vector2(3f, 3f));

            Assert.True(looking.Fits);
            Assert.Equal(Vector2.Zero, looking.Pan);
        }

        [Fact]
        public void PanIsWhereTheEyeSitsOverTheMat()
        {
            Looking looking = ATown();

            looking.PanTo(new Vector2(0.2f, -0.1f));

            Assert.Equal(looking.Pan.X, looking.Middle.X, 5);
            Assert.Equal(0f, looking.Middle.Y);
            Assert.Equal(looking.Pan.Y, looking.Middle.Z, 5);
        }
    }
}
