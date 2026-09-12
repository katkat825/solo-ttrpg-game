using System;
using System.Collections.Generic;
using System.Linq;
using Core.Space;
using Xunit;

namespace Core.Tests
{
    // Every case here is a map you can read. That is the point of drawing them as text: a
    // pathfinding test written as coordinates is a test nobody can check, and the bugs in this
    // kind of code - a cut corner, a route that ignores difficult ground, a tie broken differently
    // on alternate runs - are exactly the ones that look completely fine in motion.
    public class RouteTests
    {
        static MapLayout Read(params string[] lines)
        {
            Assert.True(MapReader.TryRead(string.Join("\n", lines), out MapLayout map, out string problem),
                        problem);

            return map;
        }

        // an empty board: nobody is standing anywhere, said out loud because Route makes you say it
        static readonly Func<Cell, bool> Empty = _ => false;

        static IReadOnlyList<Cell> Walk(MapLayout map, Cell from, Cell to) =>
            Route.Between(map, from, to, Empty);

        // every step is to a neighbouring square, and every square is one a piece may stand on
        static void AssertIsAWalk(MapLayout map, IReadOnlyList<Cell> route, Cell from, Cell to)
        {
            Assert.NotNull(route);
            Assert.Equal(from, route[0]);
            Assert.Equal(to, route[route.Count - 1]);

            for (int i = 1; i < route.Count; i++)
            {
                int dx = Math.Abs(route[i].X - route[i - 1].X);
                int dy = Math.Abs(route[i].Y - route[i - 1].Y);

                Assert.True(dx <= 1 && dy <= 1 && dx + dy > 0, $"step {i} is a jump, not a step");
                Assert.True(map.IsPassable(route[i]), $"step {i} stands on {map.At(route[i])}");
            }
        }

        // ---- the ordinary cases ----

        [Fact]
        public void AcrossAnEmptyRoom_ItGoesStraightThere()
        {
            MapLayout map = Read(
                "#######",
                "#@....#",
                "#.....#",
                "#######");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(5, 1));

            AssertIsAWalk(map, route, new Cell(1, 1), new Cell(5, 1));
            Assert.Equal(5, route.Count);   // four steps, both ends included
        }

        // a diagonal is one square, the way it is at a table
        [Fact]
        public void ADiagonalIsOneStep()
        {
            MapLayout map = Read(
                "#####",
                "#@..#",
                "#...#",
                "#...#",
                "#####");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(3, 3));

            AssertIsAWalk(map, route, new Cell(1, 1), new Cell(3, 3));
            Assert.Equal(3, route.Count);   // two diagonal steps, not four
        }

        [Fact]
        public void GoingNowhere_IsAWalkOfOneSquare()
        {
            MapLayout map = Read("###", "#@#", "###");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(1, 1));

            Assert.Single(route);
            Assert.Equal(new Cell(1, 1), route[0]);
        }

        // ---- walls ----

        [Fact]
        public void ItWalksRoundAWall()
        {
            //  a wall across the middle with one way past it at the bottom
            MapLayout map = Read(
                "#########",
                "#@..#...#",
                "#...#...#",
                "#...#...#",
                "#.......#",
                "#########");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(7, 1));

            AssertIsAWalk(map, route, new Cell(1, 1), new Cell(7, 1));

            // it cannot have gone through the wall
            Assert.DoesNotContain(route, c => c.X == 4 && c.Y <= 3);

            // and it went round the bottom, which is the only way past
            Assert.Contains(new Cell(4, 4), route);
        }

        [Fact]
        public void ASealedRoomIsUnreachable()
        {
            MapLayout map = Read(
                "########",
                "#@..####",
                "#...#..#",
                "#...#..#",
                "########");

            Assert.Null(Walk(map, new Cell(1, 1), new Cell(6, 2)));
            Assert.False(Route.Exists(map, new Cell(1, 1), new Cell(6, 2), Empty));
        }

        [Fact]
        public void AShutDoorSealsARoom()
        {
            MapLayout map = Read(
                "######",
                "#@.#.#",
                "#..+.#",
                "######");

            Assert.Null(Walk(map, new Cell(1, 1), new Cell(4, 1)));
        }

        [Fact]
        public void AWallIsNotSomewhereToStand()
        {
            MapLayout map = Read(
                "#####",
                "#@.##",
                "#...#",
                "#####");

            Assert.Null(Walk(map, new Cell(1, 1), new Cell(3, 1)));   // a wall
            Assert.Null(Walk(map, new Cell(1, 1), new Cell(0, 0)));   // the outer wall
            Assert.Null(Walk(map, new Cell(1, 1), new Cell(9, 9)));   // off the map entirely
        }

        // THE RULE THAT WOULD NEVER BE SPOTTED BY WATCHING. two walls that touch at a corner make
        // a seal, not a slot - a piece cannot slip between them diagonally
        [Fact]
        public void ItDoesNotSqueezeThroughAShutCorner()
        {
            MapLayout map = Read(
                "#####",
                "#@#.#",
                "##..#",
                "#...#",
                "#####");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(3, 1));

            // (1,1) to (2,2) is diagonal past the corner of the walls at (2,1) and (1,2):
            // sealed, so the only way out of that pocket is nowhere
            Assert.Null(route);
        }

        // but ONE wall beside a diagonal is a corner to round, not a seal - which is what a hand
        // does with a piece, and the reason the rule above is "both" rather than "either"
        [Fact]
        public void ButItRoundsTheCornerOfASingleWall()
        {
            MapLayout map = Read(
                "#####",
                "#@#.#",
                "#...#",
                "#...#",
                "#####");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(3, 1));

            AssertIsAWalk(map, route, new Cell(1, 1), new Cell(3, 1));

            // down past the pillar and back up: two diagonal steps, not four orthogonal ones
            Assert.Equal(3, route.Count);
        }

        // ---- difficult ground ----

        [Fact]
        public void ItGoesRoundDifficultGroundItCanGoRound()
        {
            //  a band of rubble across the room, with clear floor along the bottom
            MapLayout map = Read(
                "#######",
                "#@~~~.#",
                "#~~~~.#",
                "#.....#",
                "#######");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(5, 1));

            AssertIsAWalk(map, route, new Cell(1, 1), new Cell(5, 1));

            // the straight line is four squares of rubble at 2 apiece; round the bottom is longer
            // in steps and cheaper to walk
            Assert.True(Route.Cost(map, route) <= 6, "it paid more than the way round");
            Assert.Contains(route, c => c.Y == 3);
        }

        [Fact]
        public void ButThroughItWhenThereIsNoWayRound()
        {
            MapLayout map = Read(
                "#####",
                "#@..#",
                "#~~~#",
                "#...#",
                "#####");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(1, 3));

            AssertIsAWalk(map, route, new Cell(1, 1), new Cell(1, 3));
            Assert.Contains(route, c => map.At(c) == Tile.Rough);
        }

        [Fact]
        public void CostCountsEverySquareEnteredAndNotTheOneLeft()
        {
            MapLayout map = Read(
                "######",
                "#@~..#",
                "######");

            IReadOnlyList<Cell> route = Walk(map, new Cell(1, 1), new Cell(4, 1));

            // rough, floor, floor - the square it started on is free
            Assert.Equal(4, Route.Cost(map, route));
            Assert.Equal(0, Route.Cost(map, null));
        }

        // ---- other pieces ----

        [Fact]
        public void ItWillNotEndOnAnOccupiedSquare()
        {
            MapLayout map = Read(
                "#####",
                "#@..#",
                "#####");

            var taken = new Cell(3, 1);

            Assert.Null(Route.Between(map, new Cell(1, 1), taken, c => c == taken));
        }

        [Fact]
        public void ItWalksRoundWhoeverIsInTheWay()
        {
            MapLayout map = Read(
                "#####",
                "#@..#",
                "#...#",
                "#####");

            var blocker = new Cell(2, 1);

            IReadOnlyList<Cell> route =
                Route.Between(map, new Cell(1, 1), new Cell(3, 1), c => c == blocker);

            AssertIsAWalk(map, route, new Cell(1, 1), new Cell(3, 1));
            Assert.DoesNotContain(blocker, route);
        }

        // the square being left is never asked about - the piece standing there is the piece moving
        [Fact]
        public void ThePieceDoesNotBlockItself()
        {
            MapLayout map = Read(
                "#####",
                "#@..#",
                "#####");

            var here = new Cell(1, 1);

            Assert.NotNull(Route.Between(map, here, new Cell(3, 1), c => c == here));
        }

        [Fact]
        public void ItRefusesToGuessAtWhoIsStandingWhere()
        {
            MapLayout map = Read("###", "#@#", "###");

            // the compiler asks for the predicate; this is what happens if something hands it null
            Assert.Null(Route.Between(map, new Cell(1, 1), new Cell(1, 1), null));
            Assert.Null(Route.Between(null, new Cell(1, 1), new Cell(1, 1), Empty));
        }

        // ---- the same click gives the same route ----

        // a heap ordered on cost alone reorders equal routes between runs, and the piece takes a
        // different way round the same wall on alternate presses. nothing about that looks like a
        // bug; it just feels haunted
        [Fact]
        public void TheSameClickGivesTheSameRoute()
        {
            MapLayout map = Read(
                "##########",
                "#@.......#",
                "#..####..#",
                "#..#..#..#",
                "#........#",
                "##########");

            IReadOnlyList<Cell> first = Walk(map, new Cell(1, 1), new Cell(8, 4));

            for (int i = 0; i < 8; i++)
                Assert.Equal(first, Walk(map, new Cell(1, 1), new Cell(8, 4)));
        }

        // ---- it finds the cheapest way, not merely a way ----

        // checked against a flood fill, which is slow, obviously correct, and knows nothing about
        // heuristics or heaps - so an A* that quietly stops being admissible has something to fail
        // against. every reachable square on a map with rubble and pillars in it
        [Fact]
        public void EveryRouteIsAsCheapAsItCanBe()
        {
            MapLayout map = Read(
                "##########",
                "#@..~~...#",
                "#.##~~.#.#",
                "#..#...#.#",
                "#~~#.###.#",
                "#........#",
                "##########");

            var from = new Cell(1, 1);
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

        // Dijkstra by hand, with the same movement rules and none of the cleverness
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

                            if (!map.IsPassable(next)) continue;

                            if (dx != 0 && dy != 0 &&
                                !map.IsPassable(new Cell(here.X + dx, here.Y)) &&
                                !map.IsPassable(new Cell(here.X, here.Y + dy)))
                                continue;

                            int through = cost[here] + map.At(next).MoveCost();

                            if (cost.TryGetValue(next, out int already) && already <= through) continue;

                            cost[next] = through;
                            moved = true;
                        }
            }

            return cost;
        }
    }
}
