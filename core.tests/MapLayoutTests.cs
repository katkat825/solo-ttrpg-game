using System.Linq;
using Core.Space;
using Xunit;

namespace Core.Tests
{
    // What a map answers - about its squares, and since EDGE_WALLS.md about the lines between them.
    // Small, but three of these are load-bearing for everything Route and Sight do: off the map is
    // Void rather than an exception, a line has ONE name from both sides, and a map never changes.
    public class MapLayoutTests
    {
        static MapLayout Read(params string[] lines)
        {
            Assert.True(MapReader.TryRead(string.Join("\n", lines), out MapLayout map, out string problem),
                        problem);

            return map;
        }

        //  a room of six squares: floor along the top, and below it floor, rock and rough walled
        //  apart, with a shut door under the middle
        static MapLayout Room() => Read(
            "+-+-+-+",
            "|@ . .|",
            "+ +x+ +",
            "|.|#|~|",
            "+-+-+-+");

        // ---- the squares ----

        [Fact]
        public void ItKnowsWhichSquaresItHas()
        {
            MapLayout map = Room();

            Assert.True(map.Contains(new Cell(0, 0)));
            Assert.True(map.Contains(new Cell(2, 1)));
            Assert.False(map.Contains(new Cell(3, 1)));
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

            Assert.Equal(6, cells.Count);
            Assert.Equal(6, cells.Distinct().Count());
            Assert.Equal(new Cell(0, 0), cells[0]);
            Assert.Equal(new Cell(2, 0), cells[2]);
            Assert.Equal(new Cell(0, 1), cells[3]);
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

        // ---- the lines ----

        // THE PROPERTY THAT MAKES Border A TYPE. A line has two squares beside it and would
        // otherwise have two names; a wall put up from one side and looked for from the other would
        // not be found, and nothing about that failure would look like anything
        [Fact]
        public void ALineHasOneNameFromBothSides()
        {
            Assert.Equal(Border.East(new Cell(1, 2)), Border.West(new Cell(2, 2)));
            Assert.Equal(Border.South(new Cell(1, 2)), Border.North(new Cell(1, 3)));

            Assert.True(Border.Between(new Cell(1, 2), new Cell(2, 2), out Border there));
            Assert.True(Border.Between(new Cell(2, 2), new Cell(1, 2), out Border back));
            Assert.Equal(there, back);
        }

        [Fact]
        public void EachSideOfALineNamesTheOther()
        {
            Border border = Border.West(new Cell(4, 3));

            Assert.Equal(new Cell(4, 3), border.Cell);
            Assert.Equal(new Cell(3, 3), border.Across);

            Border along = Border.North(new Cell(4, 3));

            Assert.Equal(new Cell(4, 3), along.Cell);
            Assert.Equal(new Cell(4, 2), along.Across);
        }

        // squares that touch at a corner share no line, and nothing can stand on a corner
        [Fact]
        public void DiagonalNeighboursHaveNoLineBetweenThem()
        {
            Assert.False(Border.Between(new Cell(0, 0), new Cell(1, 1), out _));
            Assert.False(Border.Between(new Cell(0, 0), new Cell(0, 2), out _));
            Assert.False(Border.Between(new Cell(0, 0), new Cell(0, 0), out _));
        }

        [Fact]
        public void AMapKnowsWhichLinesItHas()
        {
            MapLayout map = Room();   // 3 x 2

            // the outer boundary is a line like any other, one past the last column and row
            Assert.True(map.Contains(Border.East(new Cell(2, 0))));
            Assert.True(map.Contains(Border.South(new Cell(0, 1))));

            Assert.False(map.Contains(new Border(new Cell(4, 0), true)));
            Assert.False(map.Contains(new Border(new Cell(0, 3), false)));
        }

        [Fact]
        public void Borders_AreEveryLineOnce()
        {
            MapLayout map = Room();   // 3 x 2: (3+1)*2 vertical and 3*(2+1) horizontal

            var borders = map.Borders.ToList();

            Assert.Equal(8 + 9, borders.Count);
            Assert.Equal(borders.Count, borders.Distinct().Count());
            Assert.All(borders, b => Assert.True(map.Contains(b)));
        }

        // ---- crossing them ----

        [Fact]
        public void AWallStopsAPieceCrossingAndALineOfSight()
        {
            MapLayout map = Room();

            // the wall between the floor and the rock on the bottom row
            Assert.False(map.CanCross(new Cell(0, 1), new Cell(1, 1)));
            Assert.False(map.CanSee(new Cell(0, 1), new Cell(1, 1)));
        }

        [Fact]
        public void AShutDoorStopsBothTheSameWay()
        {
            MapLayout map = Room();

            Assert.Equal(Edge.Door, map.Between(new Cell(1, 0), new Cell(1, 1)));
            Assert.False(map.CanCross(new Cell(1, 0), new Cell(1, 1)));
            Assert.False(map.CanSee(new Cell(1, 0), new Cell(1, 1)));
        }

        [Fact]
        public void AnOpenLineStopsNeither()
        {
            MapLayout map = Room();

            Assert.Equal(Edge.None, map.Between(new Cell(0, 0), new Cell(1, 0)));
            Assert.True(map.CanCross(new Cell(0, 0), new Cell(1, 0)));
            Assert.True(map.CanSee(new Cell(0, 0), new Cell(1, 0)));
        }

        // crossing asks about the square as well - you cannot step into rock even with nothing on
        // the line. SEEING does not: "neither end blocks" is Sight's rule and only Sight knows
        // which squares are its ends
        [Fact]
        public void CrossingAsksAboutTheSquareToo_AndSeeingDoesNot()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@ #|",
                "+-+-+");

            Assert.Equal(Edge.None, map.Between(new Cell(0, 0), new Cell(1, 0)));
            Assert.False(map.CanCross(new Cell(0, 0), new Cell(1, 0)));
            Assert.True(map.CanSee(new Cell(0, 0), new Cell(1, 0)));
        }

        [Fact]
        public void SquaresThatAreNotSideBySideCannotBeCrossedBetween()
        {
            MapLayout map = Room();

            Assert.Equal(Edge.Wall, map.Between(new Cell(0, 0), new Cell(1, 1)));
            Assert.False(map.CanCross(new Cell(0, 0), new Cell(1, 1)));
            Assert.False(map.CanSee(new Cell(0, 0), new Cell(1, 1)));
        }

        // a line out beyond the map is open, and the VOID past it is what stops anything going that
        // way. inventing a wall out there would be a second reason for the same refusal
        [Fact]
        public void ALineTheMapDoesNotHaveIsOpen()
        {
            MapLayout map = Room();

            Assert.Equal(Edge.None, map.At(new Border(new Cell(9, 9), true)));
            Assert.False(map.CanCross(new Cell(0, 0), new Cell(-1, 0)));
        }

        // ---- one thing changed ----

        // B4 opens a door. the map it opens is a NEW map, because the one that was loaded is
        // content and content does not change - what a session accumulates is the difference
        [Fact]
        public void OpeningADoorLeavesTheMapItWasLoadedFrom_Alone()
        {
            MapLayout shut = Room();
            Border door = Border.South(new Cell(1, 0));

            Assert.Equal(Edge.Door, shut.At(door));

            MapLayout open = shut.With(door, Edge.None);

            Assert.Equal(Edge.None, open.At(door));
            Assert.Equal(Edge.Door, shut.At(door));

            Assert.NotSame(shut, open);
            Assert.True(open.CanCross(new Cell(1, 0), new Cell(1, 1)) ==
                        open.IsPassable(new Cell(1, 1)));
        }

        [Fact]
        public void EverythingElseComesWithIt()
        {
            MapLayout shut = Room();
            Border door = Border.South(new Cell(1, 0));
            MapLayout open = shut.With(door, Edge.None);

            Assert.Equal(shut.Columns, open.Columns);
            Assert.Equal(shut.Rows, open.Rows);
            Assert.Equal(shut.Start, open.Start);

            foreach (Cell cell in shut.Cells) Assert.Equal(shut.At(cell), open.At(cell));

            foreach (Border border in shut.Borders)
                if (border != door)
                    Assert.Equal(shut.At(border), open.At(border));
        }

        [Fact]
        public void ASquareChangesTheSameWay()
        {
            MapLayout floor = Room();
            var wrecked = new Cell(0, 1);

            MapLayout rubble = floor.With(wrecked, Tile.Rough);

            Assert.Equal(Tile.Rough, rubble.At(wrecked));
            Assert.Equal(Tile.Floor, floor.At(wrecked));

            // and the lines came with it
            foreach (Border border in floor.Borders) Assert.Equal(floor.At(border), rubble.At(border));
        }

        [Fact]
        public void ChangingNothingIsTheSameMap()
        {
            MapLayout room = Room();

            Assert.Same(room, room.With(new Cell(0, 0), Tile.Floor));         // already floor
            Assert.Same(room, room.With(new Cell(99, 0), Tile.Floor));        // not a square it has
            Assert.Same(room, room.With(Border.West(new Cell(0, 0)), Edge.Wall));   // already a wall
            Assert.Same(room, room.With(new Border(new Cell(9, 9), true), Edge.Wall));
        }

        // ---- what a tile means ----

        [Fact]
        public void RockStopsAPieceAndALine()
        {
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

        // ---- what an edge means ----

        [Fact]
        public void AWallAndAShutDoorAreClosedAndOpaque()
        {
            Assert.False(Edge.Wall.IsOpen());
            Assert.False(Edge.Wall.IsTransparent());

            Assert.False(Edge.Door.IsOpen());
            Assert.False(Edge.Door.IsTransparent());
        }

        // an OPEN door is not a value - it is nothing on the line, because a doorway you can walk
        // and see through is a gap in the wall and the frame round it is the view's business
        [Fact]
        public void NothingOnTheLineIsOpenAndSeeThrough()
        {
            Assert.True(Edge.None.IsOpen());
            Assert.True(Edge.None.IsTransparent());
        }
    }
}
