using System.Linq;
using Core.Space;
using Xunit;

namespace Core.Tests
{
    // What a map answers, and what a tile means. Small, but two of these are load-bearing for
    // everything B3 does: off the map is Void rather than an exception, and passable and
    // transparent are two questions rather than one.
    public class MapLayoutTests
    {
        static MapLayout Read(params string[] lines)
        {
            Assert.True(MapReader.TryRead(string.Join("\n", lines), out MapLayout map, out string problem),
                        problem);

            return map;
        }

        static MapLayout Room() => Read(
            "#####",
            "#.~+#",
            "#.@.#",
            "#####");

        // ---- the map ----

        [Fact]
        public void ItKnowsWhichSquaresItHas()
        {
            MapLayout map = Room();

            Assert.True(map.Contains(new Cell(0, 0)));
            Assert.True(map.Contains(new Cell(4, 3)));
            Assert.False(map.Contains(new Cell(5, 3)));
            Assert.False(map.Contains(new Cell(-1, 0)));
        }

        // the property Route and Sight are both built on: they walk cells that step outside, and
        // neither wants a bounds check of its own
        [Fact]
        public void OffTheMapIsVoid_NotAnExceptionAndNotAWall()
        {
            MapLayout map = Room();

            Assert.Equal(Tile.Void, map.At(new Cell(-1, 0)));
            Assert.Equal(Tile.Void, map.At(new Cell(0, -1)));
            Assert.Equal(Tile.Void, map.At(new Cell(99, 99)));

            Assert.False(map.IsPassable(new Cell(-1, 0)));
            Assert.False(map.IsTransparent(new Cell(-1, 0)));
        }

        [Fact]
        public void Cells_AreEveryCellOnce_InRowMajorOrder()
        {
            var cells = Room().Cells.ToList();

            Assert.Equal(20, cells.Count);
            Assert.Equal(20, cells.Distinct().Count());
            Assert.Equal(new Cell(0, 0), cells[0]);
            Assert.Equal(new Cell(4, 0), cells[4]);
            Assert.Equal(new Cell(0, 1), cells[5]);
        }

        // a map and a grid are two structures over one extent, and the caller that read the file
        // builds both from the same numbers - so they agree by construction
        [Fact]
        public void AGridBuiltFromAMapCoversExactlyTheSameSquares()
        {
            MapLayout map = Room();
            var grid = new Grid<object>(map.Columns, map.Rows);

            foreach (Cell cell in map.Cells) Assert.True(grid.Contains(cell));

            Assert.Equal(map.Count, grid.Count);
        }

        // ---- what a tile means ----

        [Fact]
        public void WallsAndShutDoorsStopAPieceAndALine()
        {
            Assert.False(Tile.Wall.IsPassable());
            Assert.False(Tile.Wall.IsTransparent());

            Assert.False(Tile.Door.IsPassable());
            Assert.False(Tile.Door.IsTransparent());

            Assert.False(Tile.Void.IsPassable());
            Assert.False(Tile.Void.IsTransparent());
        }

        [Fact]
        public void FloorAndDifficultGroundStopNeither()
        {
            Assert.True(Tile.Floor.IsPassable());
            Assert.True(Tile.Floor.IsTransparent());

            Assert.True(Tile.Rough.IsPassable());
            Assert.True(Tile.Rough.IsTransparent());
        }

        [Fact]
        public void DifficultGroundCostsDouble()
        {
            Assert.Equal(1, Tile.Floor.MoveCost());
            Assert.Equal(2, Tile.Rough.MoveCost());
        }

        // A* is only correct while nothing costs less than the heuristic assumes
        [Fact]
        public void NothingCostsLessThanTheMinimum()
        {
            foreach (Tile tile in System.Enum.GetValues<Tile>())
                Assert.True(tile.MoveCost() >= Tiles.MinimumCost);
        }
    }
}
