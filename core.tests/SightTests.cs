using Core.Space;
using Xunit;

namespace Core.Tests
{
    // Line of sight has no picture in B3 - nothing draws it, and combat is what will lean on it -
    // so tests are the ONLY thing holding it up. That is exactly why it is worth building now and
    // not inside COMBAT_LOOP.md with a fight on top of it: every case here is a map you can read,
    // and the two properties that matter are checked exhaustively rather than by example.
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
                "########",
                "#@.....#",
                "########");

            Assert.True(Clear(map, 1, 1, 6, 1));
            Assert.True(Clear(map, 6, 1, 1, 1));
        }

        [Fact]
        public void ItSeesAcrossAnEmptyRoom()
        {
            MapLayout map = Read(
                "######",
                "#@...#",
                "#....#",
                "#....#",
                "######");

            Assert.True(Clear(map, 1, 1, 4, 3));
            Assert.True(Clear(map, 1, 3, 4, 1));
        }

        [Fact]
        public void EverySquareSeesItself()
        {
            MapLayout map = Read("###", "#@#", "###");

            Assert.True(Clear(map, 1, 1, 1, 1));

            // even a wall, which is the same statement as "neither end blocks"
            Assert.True(Clear(map, 0, 0, 0, 0));
        }

        // ---- what stops a line ----

        [Fact]
        public void AWallBetweenStopsIt()
        {
            MapLayout map = Read(
                "#######",
                "#@.#..#",
                "#######");

            Assert.False(Clear(map, 1, 1, 5, 1));
            Assert.False(Clear(map, 5, 1, 1, 1));
        }

        [Fact]
        public void AShutDoorStopsItLikeAWall()
        {
            MapLayout map = Read(
                "#######",
                "#@.+..#",
                "#######");

            Assert.False(Clear(map, 1, 1, 5, 1));
        }

        [Fact]
        public void DifficultGroundStopsNothing()
        {
            MapLayout map = Read(
                "#######",
                "#@~~~.#",
                "#######");

            Assert.True(Clear(map, 1, 1, 5, 1));
        }

        // you can see the wall you are looking at, and out of the doorway you are standing in -
        // otherwise "is that wall visible from here" has no answer at all
        [Fact]
        public void NeitherEndBlocks()
        {
            MapLayout map = Read(
                "#####",
                "#@..#",
                "#####");

            Assert.True(Clear(map, 1, 1, 4, 1));    // the wall at the end of the room
            Assert.True(Clear(map, 0, 1, 3, 1));    // and out of it, from inside the wall
        }

        // ---- the corner rule, and its agreement with movement ----

        // two walls touching corner to corner are a seal: no line through, no piece through
        [Fact]
        public void AShutCornerStopsALineJustAsItStopsAPiece()
        {
            MapLayout map = Read(
                "#####",
                "#@#.#",
                "##..#",
                "#...#",
                "#####");

            Assert.False(Clear(map, 1, 1, 2, 2));
            Assert.False(Clear(map, 2, 2, 1, 1));

            // and Route agrees, which is the point of choosing this rule
            Assert.Null(Route.Between(map, new Cell(1, 1), new Cell(2, 2), _ => false));
        }

        // one wall beside the diagonal is a corner, not a seal - a piece rounds it, so a line goes
        // past it
        [Fact]
        public void ButASingleCornerDoesNot()
        {
            MapLayout map = Read(
                "#####",
                "#@#.#",
                "#...#",
                "#...#",
                "#####");

            Assert.True(Clear(map, 1, 1, 2, 2));
            Assert.NotNull(Route.Between(map, new Cell(1, 1), new Cell(2, 2), _ => false));
        }

        // through a gap in a wall you see what is straight beyond it and nothing to either side,
        // which is the whole behaviour a doorway is supposed to have - and the case where a line
        // that merely counted walls between two squares would get it wrong
        [Fact]
        public void ItSeesThroughAGapButNotAtAnAngleThroughTheWall()
        {
            MapLayout map = Read(
                "#######",
                "#.....#",
                "###.###",
                "#@....#",
                "#######");

            Assert.True(Clear(map, 3, 3, 3, 1));    // straight up through the gap
            Assert.False(Clear(map, 1, 3, 3, 1));   // from the side, through the wall beside it
            Assert.False(Clear(map, 3, 1, 1, 3));
        }

        // ---- the two properties, checked on every pair there is ----

        // SYMMETRY. a line of sight that disagrees with itself is what gets reported as "it shot me
        // through a wall". all 4,900 pairs on a map with pillars, a shut door and a diagonal in it
        [Fact]
        public void SightIsSymmetric_EverywhereOnARealMap()
        {
            MapLayout map = Read(
                "##########",
                "#@..#....#",
                "#.###..#.#",
                "#....~.#.#",
                "#.#+##.#.#",
                "#.#....#.#",
                "##########");

            foreach (Cell from in map.Cells)
                foreach (Cell to in map.Cells)
                    Assert.True(Sight.Clear(map, from, to) == Sight.Clear(map, to, from),
                                $"{from} sees {to} but not the other way round");
        }

        // and nothing anywhere sees through a solid wall into the outside world
        [Fact]
        public void NothingSeesOffTheMap()
        {
            MapLayout map = Read(
                "#####",
                "#@..#",
                "#...#",
                "#####");

            foreach (Cell from in map.Cells)
            {
                if (!map.IsTransparent(from)) continue;

                Assert.False(Sight.Clear(map, from, new Cell(-3, 1)));
                Assert.False(Sight.Clear(map, from, new Cell(2, 9)));
            }
        }

        [Fact]
        public void ANullMapSeesNothing()
        {
            Assert.False(Sight.Clear(null, new Cell(0, 0), new Cell(1, 1)));
        }
    }
}
