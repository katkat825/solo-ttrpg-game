using Godot;
using Game.Tray;

namespace Game.Tests
{
    public class TrayBoundsTests
    {
        // traybounds.shipped restates dice_tray.tscn's dimensions; a scene resize without updating it fails here

        [Fact]
        public void Shipped_IsTheTrayTheSceneWasAuthoredWith()
        {
            TrayBounds t = TrayBounds.Shipped;

            Assert.Equal(0.64f, t.HalfWidth * 2f, 4);
            Assert.Equal(0.49f, t.HalfDepth * 2f, 4);
            Assert.Equal(0.02f, t.WallThickness, 4);

            Assert.Equal(0f, t.FeltY, 4);
        }

        [Fact]
        public void Shipped_ReproducesTheEdgesThatWereHandWrittenBeforeF2()
        {
            // inner wall faces: floor minus one wall thickness a side (were hardcoded 0.300 and 0.225)
            TrayBounds t = TrayBounds.Shipped;

            Assert.Equal(0.300f, t.FeltSideEdge, 4);
            Assert.Equal(0.225f, t.FeltNearEdge, 4);

            Assert.Equal(-0.2f, t.LostBelowY, 4);

            Assert.Equal(0.6045f, t.LostRadius, 4);
        }


        public static TheoryData<float, float> Trays => new()
        {
            { 0.32f, 0.245f },
            { 0.50f, 0.50f },
            { 0.90f, 0.20f },
            { 0.10f, 0.40f },
            { 0.08f, 0.08f },
        };

        [Theory]
        [MemberData(nameof(Trays))]
        public void EveryCornerOfTheFelt_IsInsideTheLostRadius(float halfWidth, float halfDepth)
        {
            // on a wide tray a constant lost-radius reports a die gone while it sits on the felt; derive it from the diagonal
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

            Assert.Equal(t.WallThickness, t.HalfWidth - t.FeltSideEdge, 5);
            Assert.Equal(t.WallThickness, t.HalfDepth - t.FeltNearEdge, 5);
        }

        [Theory]
        [MemberData(nameof(Trays))]
        public void NoDieOnTheFelt_IsBelowTheLostFloor(float halfWidth, float halfDepth)
        {
            // test at a non-zero felt: the shipped tray's felt-at-zero coincidence hid this before
            var t = new TrayBounds(halfWidth, halfDepth, 0.35f, 0.02f);

            Assert.Equal(0.15f, t.LostBelowY, 5);
            Assert.True(t.LostBelowY < t.FeltY);
        }


        [Theory]
        [InlineData(0.02f, 0.245f)]
        [InlineData(0.01f, 0.245f)]
        [InlineData(0.32f, 0.00f)]
        public void ATrayWithNoFeltInsideIt_SaysSo(float halfWidth, float halfDepth)
        {
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
