using Core.Space;
using Xunit;

namespace Core.Tests
{
    // Line of sight has no picture in B3 - nothing draws it, and combat is what will lean on it -
    // so tests are the ONLY thing holding it up. That is exactly why it is worth building now and
    // not inside COMBAT_LOOP.md with a fight on top of it: every case here is a map you can read,
    // and the two properties that matter are checked exhaustively rather than by example.
    //
    // SINCE EDGE_WALLS.md A LINE IS STOPPED BY THE LINES IT CROSSES, not only by the squares it
    // enters. A thin wall between two floor squares is exactly the case the old cell model could
    // not express, and it is the commonest thing on a battle map.
    public class SightTests
    {
        static MapLayout Read(params string[] lines)
        {
            Assert.True(MapReader.TryRead(string.Join("\n", lines), out MapLayout map, out string problem),
                        problem);

            return map;
        }

        static bool Clear(MapLayout map, int fx, int fy, int tx, int ty) =>
            Sight.Clear(map, new Cell(fx, fy), new Cell(tx, ty));

        // ---- down a corridor and across a room ----

        [Fact]
        public void ItSeesDownAClearCorridor()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+-+",
                "|@ . . . . .|",
                "+-+-+-+-+-+-+");

            Assert.True(Clear(map, 0, 0, 5, 0));
            Assert.True(Clear(map, 5, 0, 0, 0));
        }

        [Fact]
        public void ItSeesAcrossAnEmptyRoom()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ . . .|",
                "+ + + + +",
                "|. . . .|",
                "+ + + + +",
                "|. . . .|",
                "+-+-+-+-+");

            Assert.True(Clear(map, 0, 0, 3, 2));
            Assert.True(Clear(map, 0, 2, 3, 0));
        }

        [Fact]
        public void EverySquareSeesItself()
        {
            MapLayout map = Read("+-+-+", "|@|#|", "+-+-+");

            Assert.True(Clear(map, 0, 0, 0, 0));

            // even solid rock, which is the same statement as "neither end blocks"
            Assert.True(Clear(map, 1, 0, 1, 0));
        }

        // ---- what stops a line ----

        // THE CASE THE OLD MODEL COULD NOT DRAW: one thin wall, floor on both sides of it
        [Fact]
        public void AWallOnTheLineBetweenTwoFloorSquaresStopsIt()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ .|. .|",
                "+-+-+-+-+");

            Assert.True(map.IsPassable(new Cell(1, 0)));
            Assert.True(map.IsPassable(new Cell(2, 0)));

            Assert.False(Clear(map, 0, 0, 3, 0));
            Assert.False(Clear(map, 3, 0, 0, 0));

            // and it stops the view into the very next square, not only past it
            Assert.False(Clear(map, 1, 0, 2, 0));
        }

        [Fact]
        public void RockStopsItToo()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ . # .|",
                "+-+-+-+-+");

            Assert.False(Clear(map, 0, 0, 3, 0));
        }

        [Fact]
        public void AShutDoorStopsItLikeAWall()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ .x. .|",
                "+-+-+-+-+");

            Assert.False(Clear(map, 0, 0, 3, 0));

            // opening it is the only thing that changes
            MapLayout open = map.With(Border.East(new Cell(1, 0)), Edge.None);

            Assert.True(Sight.Clear(open, new Cell(0, 0), new Cell(3, 0)));
        }

        [Fact]
        public void DifficultGroundStopsNothing()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+",
                "|@ ~ ~ ~ .|",
                "+-+-+-+-+-+");

            Assert.True(Clear(map, 0, 0, 4, 0));
        }

        // you can see the rock you are standing against, and out of the square you are in -
        // otherwise "is that pillar visible from here" has no answer at all
        [Fact]
        public void NeitherEndBlocks()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ . . #|",
                "+-+-+-+-+");

            Assert.True(Clear(map, 0, 0, 3, 0));    // the rock at the end of the room
            Assert.True(Clear(map, 3, 0, 0, 0));    // and out of it, from inside the rock
        }

        // ---- the corner rule, and its agreement with movement ----

        // four walls meeting at one corner are a seal: no line through, no piece through
        [Fact]
        public void AShutCornerStopsALineJustAsItStopsAPiece()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@|.|",
                "+-+-+",
                "|.|.|",
                "+-+-+");

            Assert.False(Clear(map, 0, 0, 1, 1));
            Assert.False(Clear(map, 1, 1, 0, 0));

            // and Route agrees, which is the point of choosing this rule
            Assert.Null(Route.Between(map, new Cell(0, 0), new Cell(1, 1), _ => false));
        }

        // one way round open is a corner, not a seal - a piece rounds it, so a line goes past it
        [Fact]
        public void ButACornerWithAWayRoundDoesNot()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@|.|",
                "+ +-+",
                "|. .|",
                "+-+-+");

            Assert.True(Clear(map, 0, 0, 1, 1));
            Assert.NotNull(Route.Between(map, new Cell(0, 0), new Cell(1, 1), _ => false));
        }

        // through a gap in a wall you see what is straight beyond it and nothing to either side,
        // which is the whole behaviour a doorway is supposed to have - and the case where a line
        // that merely counted walls between two squares would get it wrong
        [Fact]
        public void ItSeesThroughAGapButNotAtAnAngleThroughTheWall()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+",
                "|. . . . .|",
                "+-+-+ +-+-+",
                "|@ . . . .|",
                "+-+-+-+-+-+");

            Assert.True(Clear(map, 2, 1, 2, 0));    // straight up through the gap
            Assert.False(Clear(map, 0, 1, 2, 0));   // from the side, through the wall beside it
            Assert.False(Clear(map, 2, 0, 0, 1));
        }

        // ---- the two properties, checked on every pair there is ----

        // SYMMETRY. a line of sight that disagrees with itself is what gets reported as "it shot me
        // through a wall". all 784 pairs on a map with walls, a door, rock and rough on it
        [Fact]
        public void SightIsSymmetric_EverywhereOnARealMap()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+-+-+",
                "|@ . . .|. . .|",
                "+ +-+-+ + +-+ +",
                "|. .|# .x. .|.|",
                "+ + +-+-+ + + +",
                "|. ~ ~ .|. . .|",
                "+ + + + + +-+ +",
                "|. . . . . . .|",
                "+-+-+-+-+-+-+-+");

            foreach (Cell from in map.Cells)
                foreach (Cell to in map.Cells)
                    Assert.True(Sight.Clear(map, from, to) == Sight.Clear(map, to, from),
                                $"{from} sees {to} but not the other way round");
        }

        // and nothing anywhere sees through a wall into the outside world
        [Fact]
        public void NothingSeesOffTheMap()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ . .|",
                "+ + + +",
                "|. . .|",
                "+-+-+-+");

            foreach (Cell from in map.Cells)
            {
                Assert.False(Sight.Clear(map, from, new Cell(-3, 0)));
                Assert.False(Sight.Clear(map, from, new Cell(1, 9)));
            }
        }

        [Fact]
        public void ANullMapSeesNothing()
        {
            Assert.False(Sight.Clear(null, new Cell(0, 0), new Cell(1, 1)));
        }
    }
}
