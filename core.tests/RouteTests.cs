using System;
using System.Collections.Generic;
using System.Linq;
using Core.Space;
using Xunit;

namespace Core.Tests
{
    public class RouteTests
    {
        static MapLayout Read(params string[] lines)
        {
            Assert.True(MapReader.TryRead(string.Join("\n", lines), out MapLayout map, out string problem),
                        problem);

            return map;
        }

        static readonly Func<Cell, bool> Empty = _ => false;

        static IReadOnlyList<Cell> Walk(MapLayout map, Cell from, Cell to) =>
            Route.Between(map, from, to, Empty);

        // check the whole walk, not just the ends, to catch a route that slipped through a wall
        static void AssertIsAWalk(MapLayout map, IReadOnlyList<Cell> route, Cell from, Cell to)
        {
            Assert.NotNull(route);
            Assert.Equal(from, route[0]);
            Assert.Equal(to, route[route.Count - 1]);

            for (int i = 1; i < route.Count; i++)
            {
                Cell was = route[i - 1];
                Cell now = route[i];

                int dx = now.X - was.X;
                int dy = now.Y - was.Y;

                Assert.True(Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1 && dx * dx + dy * dy > 0,
                            $"step {i} is a jump, not a step");
                Assert.True(map.IsPassable(now), $"step {i} stands on {map.At(now)}");

                if (dx == 0 || dy == 0)
                {
                    Assert.True(map.CanCross(was, now), $"step {i} crossed {map.Between(was, now)}");
                    continue;
                }

                var sideways = new Cell(was.X + dx, was.Y);
                var forward = new Cell(was.X, was.Y + dy);

                Assert.True((map.CanCross(was, sideways) && map.CanCross(sideways, now))
                            || (map.CanCross(was, forward) && map.CanCross(forward, now)),
                            $"step {i} cut a corner with no way round it");
            }
        }


        [Fact]
        public void AcrossAnEmptyRoom_ItGoesStraightThere()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+-+",
                "|@ . . . . .|",
                "+ + + + + + +",
                "|. . . . . .|",
                "+-+-+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(4, 0));

            AssertIsAWalk(map, route, new Cell(0, 0), new Cell(4, 0));
            Assert.Equal(5, route.Count);
        }

        [Fact]
        public void ADiagonalIsOneStep()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ . .|",
                "+ + + +",
                "|. . .|",
                "+ + + +",
                "|. . .|",
                "+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(2, 2));

            AssertIsAWalk(map, route, new Cell(0, 0), new Cell(2, 2));
            Assert.Equal(3, route.Count);
        }

        [Fact]
        public void GoingNowhere_IsAWalkOfOneSquare()
        {
            MapLayout map = Read("+-+", "|@|", "+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(0, 0));

            Assert.Single(route);
            Assert.Equal(new Cell(0, 0), route[0]);
        }

        [Fact]
        public void BothSquaresBesideAWallCanBeStoodOn()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@|.|",
                "+-+-+");

            Assert.True(map.IsPassable(new Cell(0, 0)));
            Assert.True(map.IsPassable(new Cell(1, 0)));

            Assert.Null(Walk(map, new Cell(0, 0), new Cell(1, 0)));
        }


        [Fact]
        public void ItWalksRoundAWall()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+",
                "|@ .|. . .|",
                "+ + + + + +",
                "|. .|. . .|",
                "+ + + + + +",
                "|. . . . .|",
                "+-+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(4, 0));

            AssertIsAWalk(map, route, new Cell(0, 0), new Cell(4, 0));

            Assert.Contains(route, c => c.Y == 2);
        }

        [Fact]
        public void ASealedRoomIsUnreachable()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ .|. .|",
                "+ + +-+ +",
                "|. .|. .|",
                "+-+-+-+-+");

            Assert.Null(Walk(map, new Cell(0, 0), new Cell(3, 1)));
            Assert.False(Route.Exists(map, new Cell(0, 0), new Cell(2, 0), Empty));
        }

        [Fact]
        public void AShutDoorSealsARoom()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ .x.|",
                "+-+-+-+");

            Assert.Null(Walk(map, new Cell(0, 0), new Cell(2, 0)));

            MapLayout open = map.With(Border.East(new Cell(1, 0)), Edge.None);

            AssertIsAWalk(open, Walk(open, new Cell(0, 0), new Cell(2, 0)), new Cell(0, 0), new Cell(2, 0));
        }

        [Fact]
        public void RockIsNotSomewhereToStand()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ . #|",
                "+ + + +",
                "|. . .|",
                "+-+-+-+");

            Assert.Null(Walk(map, new Cell(0, 0), new Cell(2, 0)));
            Assert.Null(Walk(map, new Cell(0, 0), new Cell(9, 9)));
        }

        // four walls at one corner are a seal: no diagonal slip between them
        [Fact]
        public void ItDoesNotSqueezeThroughAShutCorner()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@|.|",
                "+-+-+",
                "|.|.|",
                "+-+-+");

            Assert.True(map.IsPassable(new Cell(1, 1)));
            Assert.Null(Walk(map, new Cell(0, 0), new Cell(1, 1)));
        }

        // one way round open is a corner to round, not a seal ("either L", not "both neighbours")
        [Fact]
        public void ButItRoundsACornerWithOneWayRound()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@|.|",
                "+ +-+",
                "|. .|",
                "+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(1, 1));

            AssertIsAWalk(map, route, new Cell(0, 0), new Cell(1, 1));
            Assert.Equal(2, route.Count);
        }


        [Fact]
        public void ItGoesRoundDifficultGroundItCanGoRound()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+-+",
                "|@ ~ ~ ~ . .|",
                "+ + + + + + +",
                "|. . . . . .|",
                "+-+-+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(4, 0));

            AssertIsAWalk(map, route, new Cell(0, 0), new Cell(4, 0));

            Assert.True(Route.Cost(map, route) <= 4, "it paid more than the way round");
            Assert.Contains(route, c => c.Y == 1);
        }

        [Fact]
        public void ButThroughItWhenThereIsNoWayRound()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ . .|",
                "+ + + +",
                "|~ ~ ~|",
                "+ + + +",
                "|. . .|",
                "+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(0, 2));

            AssertIsAWalk(map, route, new Cell(0, 0), new Cell(0, 2));
            Assert.Contains(route, c => map.At(c) == Tile.Rough);
        }

        [Fact]
        public void CostCountsEverySquareEnteredAndNotTheOneLeft()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ ~ . .|",
                "+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(3, 0));

            Assert.Equal(4, Route.Cost(map, route));
            Assert.Equal(0, Route.Cost(map, null));
        }


        [Fact]
        public void ItWillNotEndOnAnOccupiedSquare()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ . . .|",
                "+-+-+-+-+");

            var taken = new Cell(2, 0);

            Assert.Null(Route.Between(map, new Cell(0, 0), taken, c => c == taken));
        }

        [Fact]
        public void ItWalksRoundWhoeverIsInTheWay()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ . . .|",
                "+ + + + +",
                "|. . . .|",
                "+-+-+-+-+");

            var blocker = new Cell(1, 0);

            IReadOnlyList<Cell> route =
                Route.Between(map, new Cell(0, 0), new Cell(2, 0), c => c == blocker);

            AssertIsAWalk(map, route, new Cell(0, 0), new Cell(2, 0));
            Assert.DoesNotContain(blocker, route);
        }

        // a diagonal needs one of its two corner squares free; occupancy in both blocks it
        [Fact]
        public void TwoPiecesInTheWayCloseTheDiagonalBetweenThem()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@ .|",
                "+ + +",
                "|. .|",
                "+-+-+");

            var one = new Cell(1, 0);
            var two = new Cell(0, 1);

            IReadOnlyList<Cell> route =
                Route.Between(map, new Cell(0, 0), new Cell(1, 1), c => c == one || c == two);

            Assert.Null(route);
        }

        [Fact]
        public void ThePieceDoesNotBlockItself()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ . .|",
                "+-+-+-+");

            var here = new Cell(0, 0);

            Assert.NotNull(Route.Between(map, here, new Cell(2, 0), c => c == here));
        }

        [Fact]
        public void ItRefusesToGuessAtWhoIsStandingWhere()
        {
            MapLayout map = Read("+-+", "|@|", "+-+");

            Assert.Null(Route.Between(map, new Cell(0, 0), new Cell(0, 0), null));
            Assert.Null(Route.Between(null, new Cell(0, 0), new Cell(0, 0), Empty));
        }


        // a heap ordered on cost alone reorders equal routes between runs; the tie-break keeps clicks stable
        [Fact]
        public void TheSameClickGivesTheSameRoute()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+-+-+-+",
                "|@ . . . . . . .|",
                "+ + + + + + + + +",
                "|. .|. . .|. . .|",
                "+ + +-+-+ + + + +",
                "|. .|. . .|. . .|",
                "+ + + + + + + + +",
                "|. . . . . . . .|",
                "+-+-+-+-+-+-+-+-+");

            IReadOnlyList<Cell> first = Walk(map, new Cell(0, 0), new Cell(6, 3));

            for (int i = 0; i < 8; i++)
                Assert.Equal(first, Walk(map, new Cell(0, 0), new Cell(6, 3)));
        }


        // checked against a slow, obviously-correct flood fill so a non-admissible a* fails against it
        [Fact]
        public void EveryRouteIsAsCheapAsItCanBe()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+-+-+-+",
                "|@ . ~ ~ . . . .|",
                "+ +-+ + + +-+ + +",
                "|. .|~ ~ .|# . .|",
                "+ + +-+-+ + + + +",
                "|. .|. . .|. . .|",
                "+ + + + + + +-+ +",
                "|~ ~ . . . . . .|",
                "+-+-+-+-+-+-+-+-+");

            var from = new Cell(0, 0);
            Dictionary<Cell, int> flooded = Flood(map, from);

            foreach (Cell cell in map.Cells)
            {
                IReadOnlyList<Cell> route = Walk(map, from, cell);

                if (!flooded.TryGetValue(cell, out int cheapest))
                {
                    Assert.Null(route);
                    continue;
                }

                AssertIsAWalk(map, route, from, cell);
                Assert.Equal(cheapest, Route.Cost(map, route));
            }
        }

        static Dictionary<Cell, int> Flood(MapLayout map, Cell from)
        {
            var cost = new Dictionary<Cell, int> { [from] = 0 };
            bool moved = true;

            while (moved)
            {
                moved = false;

                foreach (Cell here in map.Cells.Where(cost.ContainsKey).ToList())
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;

                            var next = new Cell(here.X + dx, here.Y + dy);

                            if (!Reaches(map, here, next, dx, dy)) continue;

                            int through = cost[here] + map.At(next).MoveCost();

                            if (cost.TryGetValue(next, out int already) && already <= through) continue;

                            cost[next] = through;
                            moved = true;
                        }
            }

            return cost;
        }

        static bool Reaches(MapLayout map, Cell from, Cell to, int dx, int dy)
        {
            if (dx == 0 || dy == 0) return map.CanCross(from, to);

            var sideways = new Cell(from.X + dx, from.Y);
            var forward = new Cell(from.X, from.Y + dy);

            return (map.CanCross(from, sideways) && map.CanCross(sideways, to))
                || (map.CanCross(from, forward) && map.CanCross(forward, to));
        }


        [Fact]
        public void OneMoveStopsWhenTheBudgetRunsOut()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+-+-+-+",
                "|@ . . . . . . .|",
                "+-+-+-+-+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(7, 0));
            IReadOnlyList<Cell> reached = Route.Within(map, route, 5);

            Assert.Equal(new Cell(5, 0), reached[reached.Count - 1]);
            Assert.Equal(5, Route.Cost(map, reached));
        }

        [Fact]
        public void AShortEnoughRouteIsWalkedWhole()
        {
            MapLayout map = Read(
                "+-+-+-+-+",
                "|@ . . .|",
                "+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(3, 0));

            Assert.Equal(route, Route.Within(map, route, 5));
        }

        [Fact]
        public void DifficultGroundIsPaidForWholeOrNotEntered()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+",
                "|@ . ~ . .|",
                "+-+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(4, 0));

            IReadOnlyList<Cell> reached = Route.Within(map, route, 2);

            Assert.Equal(new Cell(1, 0), reached[reached.Count - 1]);

            Assert.Equal(new Cell(2, 0), Route.Within(map, route, 3)[2]);
        }

        [Fact]
        public void NoBudgetAtAll_LeavesThePieceWhereItIs()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ . .|",
                "+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(2, 0));
            IReadOnlyList<Cell> reached = Route.Within(map, route, 0);

            Assert.Single(reached);
            Assert.Equal(new Cell(0, 0), reached[0]);
        }

    }
}
