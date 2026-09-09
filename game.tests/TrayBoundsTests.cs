using Godot;
using Game.Tray;

namespace Game.Tests
{
    // TrayBounds is the one place that answers "how big is the tray", and since F2 everything
    // that used to hand-copy a world coordinate asks it instead: escape detection, label
    // clamping, where the rings lie.
    //
    // Two kinds of thing are guarded here, and they are worth telling apart:
    //
    //   the derivations must not drift from the scene - the Shipped cases
    //   the derivations must hold for ANY tray        - the resize cases
    //
    // The second is the point of the milestone. A number that happens to be right for a
    // 0.64 x 0.49 tray and wrong for every other one is the bug F2 exists to remove, and it
    // reads as correct right up until the tray is resized.
    //
    // Godot-free in the sense game.tests means: Vector2 is a managed struct, so this needs no
    // engine behind it. TrayBounds touches no Node and no Resource, on purpose.
    public class TrayBoundsTests
    {
        // ---- the tray M9 shipped ----
        //
        // dice_tray.tscn: floor box 0.64 x 0.02 x 0.49 centred at y -0.01, walls 0.02 thick.
        // DiceTray reads exactly those off the scene at load; TrayBounds.Shipped is the second
        // statement of them and the only one left in the project. These cases hold the two
        // together, so resizing the scene without updating Shipped fails here rather than in
        // front of a player - the arrangement PoolOdds uses for the Snag rate.

        [Fact]
        public void Shipped_IsTheTrayTheSceneWasAuthoredWith()
        {
            TrayBounds t = TrayBounds.Shipped;

            Assert.Equal(0.64f, t.HalfWidth * 2f, 4);
            Assert.Equal(0.49f, t.HalfDepth * 2f, 4);
            Assert.Equal(0.02f, t.WallThickness, 4);

            // the felt is the top of the floor box, which the scene puts at the tray's own zero
            Assert.Equal(0f, t.FeltY, 4);
        }

        [Fact]
        public void Shipped_ReproducesTheEdgesThatWereHandWrittenBeforeF2()
        {
            // DieMark carried these as 0.300 and 0.225, in world coordinates, with a comment
            // saying it assumed the tray sat on the world origin. they are the inner faces of
            // the walls, so the floor minus one wall thickness a side is where they come from
            TrayBounds t = TrayBounds.Shipped;

            Assert.Equal(0.300f, t.FeltSideEdge, 4);
            Assert.Equal(0.225f, t.FeltNearEdge, 4);

            // DieBody carried this one as a world Y
            Assert.Equal(-0.2f, t.LostBelowY, 4);

            // and this one as a flat 0.6. it is now the felt's half-diagonal times the margin,
            // which lands a shade wider - a die is called lost fractionally later than it was,
            // and being slow to give up on a die is the safe direction to be wrong in
            Assert.Equal(0.6045f, t.LostRadius, 4);
        }

        // ---- the properties that must hold for any tray ----

        public static TheoryData<float, float> Trays => new()
        {
            { 0.32f, 0.245f },  // the tray as shipped
            { 0.50f, 0.50f },   // square, and much bigger
            { 0.90f, 0.20f },   // a long thin gutter
            { 0.10f, 0.40f },   // and the same the other way round
            { 0.08f, 0.08f },   // barely wider than its own walls
        };

        [Theory]
        [MemberData(nameof(Trays))]
        public void EveryCornerOfTheFelt_IsInsideTheLostRadius(float halfWidth, float halfDepth)
        {
            // THE case this milestone exists for. LostRadius was a constant 0.6 that happened to
            // clear the shipped tray's corners; on a tray 1.2 m wide the same constant reports a
            // die as gone while it is sitting on the felt in front of you, and the throw ends
            // with a forced settle nobody asked for. deriving it from the diagonal cannot do that
            var t = new TrayBounds(halfWidth, halfDepth, 0f, 0.02f);

            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                var corner = new Vector2(sx * t.HalfWidth, sz * t.HalfDepth);

                Assert.True(corner.Length() < t.LostRadius,
                    $"a die in the {sx},{sz} corner of a {halfWidth * 2f} x {halfDepth * 2f} tray " +
                    $"is {corner.Length():0.0000} out, past a lost radius of {t.LostRadius:0.0000}");
            }
        }

        [Theory]
        [MemberData(nameof(Trays))]
        public void TheFeltALabelMayUse_IsInsideTheFloorItIsPaintedOn(float halfWidth, float halfDepth)
        {
            var t = new TrayBounds(halfWidth, halfDepth, 0f, 0.02f);

            Assert.True(t.FeltSideEdge > 0f && t.FeltSideEdge < t.HalfWidth);
            Assert.True(t.FeltNearEdge > 0f && t.FeltNearEdge < t.HalfDepth);

            // exactly one wall thickness in from the floor's edge, both ways - a name clamped to
            // the floor rather than to the felt slides under the woodwork, which looks identical
            // to a die that was never marked
            Assert.Equal(t.WallThickness, t.HalfWidth - t.FeltSideEdge, 5);
            Assert.Equal(t.WallThickness, t.HalfDepth - t.FeltNearEdge, 5);
        }

        [Theory]
        [MemberData(nameof(Trays))]
        public void NoDieOnTheFelt_IsBelowTheLostFloor(float halfWidth, float halfDepth)
        {
            // a die at rest sits ON the felt, so the drop has to be a real gap under it and not
            // a rounding error. checked at a felt that is not at zero, because the shipped tray's
            // felt being at the tray's own zero is the coincidence that hid this before F2
            var t = new TrayBounds(halfWidth, halfDepth, 0.35f, 0.02f);

            Assert.Equal(0.15f, t.LostBelowY, 5);
            Assert.True(t.LostBelowY < t.FeltY);
        }

        // ---- the guard ----

        [Theory]
        [InlineData(0.02f, 0.245f)]  // exactly as wide as its own walls
        [InlineData(0.01f, 0.245f)]  // narrower than them
        [InlineData(0.32f, 0.00f)]   // no depth at all
        public void ATrayWithNoFeltInsideIt_SaysSo(float halfWidth, float halfDepth)
        {
            // DiceTray refuses a measurement that fails this and says so loudly rather than
            // drawing every label at a negative edge, which reads as labels that simply vanished
            Assert.False(new TrayBounds(halfWidth, halfDepth, 0f, 0.02f).IsUsable);
        }

        [Fact]
        public void TheShippedTray_IsUsable() => Assert.True(TrayBounds.Shipped.IsUsable);

        [Fact]
        public void ToString_IsDeveloperOutput_AndNotLocalized()
        {
            string line = TrayBounds.Shipped.ToString();

            Assert.Contains("0.640", line);
            Assert.Contains("0.490", line);
        }
    }
}
