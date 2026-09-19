using Godot;
using Game.Room;

namespace Game.Tests
{
    // WHAT THE FIXED CAMERA CAN SEE, held to arithmetic.
    //
    // The four eye-check findings this exists to stop coming back were all the same shape - a thing
    // whose middle was in the picture and whose edge was not - so the cases here are mostly about
    // edges, depth and the fact that the frame narrows toward the player.
    public class FramingTests
    {
        // table.tscn's camera: 1.34 m up, 0.8 m back, pitched 60 degrees down, 35 degree lens
        static Framing Table()
        {
            var basis = new Basis(Vector3.Right, Mathf.DegToRad(-60f));

            return new Framing(new Transform3D(basis, new Vector3(0.08f, 1.3423f, 0.805f)), 35f);
        }

        static Framing Straight(float fov = 90f, float aspect = Framing.Widescreen) =>
            new Framing(Transform3D.Identity, fov, aspect);


        [Fact]
        public void ACameraLessFramingSeesNothingAndSaysSo()
        {
            var none = default(Framing);

            Assert.False(none.Exists);
            Assert.False(none.Holds(Vector3.Zero));
        }


        // Godot keeps the vertical angle and widens, so a widescreen frame is wider than it is tall
        // and that is why things went off the SIDES first
        [Fact]
        public void TheFrameIsWiderThanItIsTallByTheAspect()
        {
            Framing frame = Straight();

            Assert.Equal(1f, frame.HalfHeight(1f), 4);
            Assert.Equal(Framing.Widescreen, frame.HalfWidth(1f), 4);
        }


        [Fact]
        public void TheFrameWidensWithDistance()
        {
            Framing frame = Straight();

            Assert.Equal(2f, frame.HalfHeight(2f), 4);
            Assert.Equal(0.5f, frame.HalfHeight(0.5f), 4);
        }


        // a camera looks down its own -Z
        [Fact]
        public void DepthIsMeasuredAlongTheWayItIsLooking()
        {
            Framing frame = Straight();

            Assert.Equal(3f, frame.Depth(new Vector3(0f, 0f, -3f)), 4);
            Assert.Equal(-3f, frame.Depth(new Vector3(0f, 0f, 3f)), 4);
        }


        [Fact]
        public void NothingBehindTheCameraIsInThePicture()
        {
            Framing frame = Straight();

            Assert.False(frame.Holds(new Vector3(0f, 0f, 1f)));
            Assert.True(frame.Margin(new Vector3(0f, 0f, 1f)) < 0f);
        }


        // the point of the whole file: a margin, in metres, that a failure can print
        [Fact]
        public void TheMarginIsHowFarInsideTheNearestEdgeItIs()
        {
            Framing frame = Straight();

            // 1 m ahead the frame is 1 m to the top and 1.778 m to the side, so the top is nearest
            Assert.Equal(0.75f, frame.Margin(new Vector3(0f, 0.25f, -1f)), 3);

            // and off the top by a quarter of a metre reads as a quarter of a metre outside
            Assert.Equal(-0.25f, frame.Margin(new Vector3(0f, 1.25f, -1f)), 3);
        }


        [Fact]
        public void AThingIsJudgedByItsWorstCornerAndNotItsMiddle()
        {
            Framing frame = Straight();

            var middle = new Vector3(0f, 0.9f, -1f);

            Assert.True(frame.Holds(middle));

            // 40 cm tall, so its top edge is 10 cm past the top of the picture. This is the
            // character sheet's bug: the middle was always in frame
            Assert.False(frame.Holds(middle, new Vector3(0.1f, 0.4f, 0f)));
            Assert.Equal(-0.1f, frame.Margin(middle, new Vector3(0.1f, 0.4f, 0f)), 3);
        }


        // THE FRAME NARROWS TOWARD THE PLAYER, which is why a card that passed at the back of the
        // table failed at the front of it - and why the speech card had to stop growing from its
        // middle
        [Fact]
        public void TheSameObjectHasLessRoomNearerTheCamera()
        {
            Framing frame = Straight();

            var span = new Vector3(1f, 1f, 0.01f);

            float far = frame.Margin(new Vector3(0f, 0f, -4f), span);
            float near = frame.Margin(new Vector3(0f, 0f, -1f), span);

            Assert.True(far > near);
        }


        // ---- the shipped table ----------------------------------------------------------------

        [Fact]
        public void TheShippedCameraLooksDownAtTheTable()
        {
            Framing frame = Table();

            Assert.True(frame.Exists);

            // the middle of the board, 1.6 m away down the lens
            Assert.Equal(1.592f, frame.Depth(new Vector3(-0.36f, -0.014f, -0.03f)), 2);
        }


        // the board, the tray and the sheet, which three eye-check passes were spent spreading out
        [Theory]
        [InlineData(-0.36f, -0.014f, -0.03f)]   // the board
        [InlineData(0.30f, 0.0f, 0.09f)]        // the dice tray
        [InlineData(0.765f, -0.038f, 0.05f)]    // the character sheet
        [InlineData(-0.625f, 0.02f, 0.33f)]     // the hint cord
        [InlineData(0.30f, -0.013f, -0.27f)]    // the companion
        public void EverythingTheEyeChecksMovedIsInThePicture(float x, float y, float z)
        {
            Assert.True(Table().Holds(new Vector3(x, y, z)));
        }


        // where the bottom of the picture lands on the felt. Everything nearer than this is below
        // the edge, which is the one number somebody laying a table out wants
        [Fact]
        public void TheNearEdgeOfThePictureLandsOnTheTableWhereItSays()
        {
            float z = Table().NearEdgeOn(-0.021f);

            Assert.Equal(0.503f, z, 2);
        }


        [Fact]
        public void AStackOfCardsPastTheNearEdgeIsOutOfThePicture()
        {
            Framing frame = Table();

            // where the loot stack used to stand: 12 cm below the bottom of the picture
            Assert.False(frame.Holds(new Vector3(-0.30f, -0.036f, 0.62f)));

            // and where it stands now
            Assert.True(frame.Holds(new Vector3(0.50f, -0.036f, 0.42f)));
        }


        // THE VERB CARDS, which are the only way to act on anything while exploring and were laid
        // out from 20 cm nearer the player than the node they hang off
        [Fact]
        public void TheColumnOfVerbCardsStaysInsideTheNearEdge()
        {
            Framing frame = Table();

            // Response stands at z 0.34 and the column runs from CardsAt toward the player, at
            // most CardsRun before the cards start to overlap
            const float responseAt = 0.34f;
            const float cardsAt = -0.05f;
            const float run = 0.16f;

            Assert.True(frame.Holds(new Vector3(-0.36f, -0.034f, responseAt + cardsAt)));
            Assert.True(frame.Holds(new Vector3(-0.36f, -0.034f, responseAt + cardsAt + run)));

            // and where the column used to start, which was already past the edge
            Assert.False(frame.Holds(new Vector3(-0.36f, -0.034f, responseAt + 0.20f)));
        }


        // THE INITIATIVE STAND stands off the mat's left edge, so how far out it is depends on how
        // wide the map is - which is campaign data, and will one day be Workshop data
        [Theory]
        [InlineData(8, true)]     // the shipped cellar: room beside it for the names
        [InlineData(12, false)]   // ashfall's yard: the stand's own names fall off the side
        public void HowFarOffTheMatTheTurnOrderStandsDependsOnTheMap(int columns, bool fits)
        {
            Framing frame = Table();

            // the board sits at x -0.36, and the stand goes a margin past the mat's left edge
            float mat = columns * 0.06f * 0.5f;
            float stand = -0.36f - mat - 0.055f;

            // a name runs LEFT from the stand, because the rows are right-aligned against the mat
            var longest = new Vector3(stand - 0.128f, 0.05f, -0.21f);

            Assert.Equal(fits, frame.Holds(longest));
        }


        [Fact]
        public void ASurfaceTheBottomEdgeNeverReachesHasNoNearEdge()
        {
            // above the camera's own eye, looking down: the bottom edge goes away from it forever
            Assert.True(float.IsNaN(Table().NearEdgeOn(3f)));
        }
    }
}
