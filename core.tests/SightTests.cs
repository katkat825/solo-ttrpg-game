using Core.Space;
using Xunit;

namespace Core.Tests
{
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

            Assert.True(Clear(map, 1, 0, 1, 0));
        }


        // one thin wall with floor on both sides, the case the old cell model could not draw
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

        // you can see out of your own square and the rock you stand against, or visibility has no origin
        [Fact]
        public void NeitherEndBlocks()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ . . #|",
                "+-+-+-+-+");

            Assert.True(Clear(map, 0, 0, 3, 0));
            Assert.True(Clear(map, 3, 0, 0, 0));
        }


        // four walls at a corner are a seal for sight too, agreeing with route
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

            Assert.Null(Route.Between(map, new Cell(0, 0), new Cell(1, 1), _ => false));
        }

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

        // through a gap you see straight ahead only; counting walls between squares would get this wrong
        [Fact]
        public void ItSeesThroughAGapButNotAtAnAngleThroughTheWall()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+",
                "|. . . . .|",
                "+-+-+ +-+-+",
                "|@ . . . .|",
                "+-+-+-+-+-+");

            Assert.True(Clear(map, 2, 1, 2, 0));
            Assert.False(Clear(map, 0, 1, 2, 0));
            Assert.False(Clear(map, 2, 0, 0, 1));
        }


        // symmetry, checked on all 784 pairs: an asymmetric line of sight is "it shot me through a wall"
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
