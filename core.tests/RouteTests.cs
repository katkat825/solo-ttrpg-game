using System;
using System.Collections.Generic;
using System.Linq;
using Core.Space;
using Xunit;

namespace Core.Tests
{
    // Every case here is a map you can read. That is the point of drawing them as text: a
    // pathfinding test written as coordinates is a test nobody can check, and the bugs in this
    // kind of code - a route that slips through a corner it should not, one that ignores difficult
    // ground, one that breaks a tie differently on alternate runs - are exactly the ones that look
    // completely fine in motion.
    //
    // SINCE EDGE_WALLS.md THE WALLS ARE ON THE LINES, so the maps below are drawn at double
    // resolution and the squares beside a wall are ordinary floor. That is most of the change here:
    // the cases are the same cases, and what used to be a column of wall squares is now a column of
    // `|` on the line between two columns of floor.
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

        // every step is one a piece could actually take: to a neighbouring square, onto ground it
        // can stand on, and either straight across an open line or diagonally round a corner with
        // at least one way round open. checking the WALK rather than just the ends is what catches
        // a route that went through a wall and arrived looking plausible
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

        // ---- the ordinary cases ----

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
            Assert.Equal(5, route.Count);   // four steps, both ends included
        }

        // a diagonal is one square, the way it is at a table
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
            Assert.Equal(3, route.Count);   // two diagonal steps, not four
        }

        [Fact]
        public void GoingNowhere_IsAWalkOfOneSquare()
        {
            MapLayout map = Read("+-+", "|@|", "+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(0, 0));

            Assert.Single(route);
            Assert.Equal(new Cell(0, 0), route[0]);
        }

        // THE POINT OF THE WHOLE REWORK, as a test: a wall is a line, and the squares on both sides
        // of it are floor a piece can stand on. under the old model one of them was the wall
        [Fact]
        public void BothSquaresBesideAWallCanBeStoodOn()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@|.|",
                "+-+-+");

            Assert.True(map.IsPassable(new Cell(0, 0)));
            Assert.True(map.IsPassable(new Cell(1, 0)));

            // and there is still no way from one to the other
            Assert.Null(Walk(map, new Cell(0, 0), new Cell(1, 0)));
        }

        // ---- walls ----

        [Fact]
        public void ItWalksRoundAWall()
        {
            //  a wall up the middle, stopping one row short of the bottom
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

            // it had to come down to the row the wall does not reach
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

            // and opening it is the only thing that changes
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

            Assert.Null(Walk(map, new Cell(0, 0), new Cell(2, 0)));   // the rock
            Assert.Null(Walk(map, new Cell(0, 0), new Cell(9, 9)));   // off the map entirely
        }

        // THE RULE THAT WOULD NEVER BE SPOTTED BY WATCHING. four walls meeting at one corner are a
        // seal, not a slot - a piece cannot slip between them diagonally
        [Fact]
        public void ItDoesNotSqueezeThroughAShutCorner()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@|.|",
                "+-+-+",
                "|.|.|",
                "+-+-+");

            // every square is floor and every one is walled off from the others
            Assert.True(map.IsPassable(new Cell(1, 1)));
            Assert.Null(Walk(map, new Cell(0, 0), new Cell(1, 1)));
        }

        // but ONE way round open is a corner to round, not a seal - which is what a hand does with
        // a piece, and the reason the rule is "either L" rather than "both neighbours"
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
            Assert.Equal(2, route.Count);   // one diagonal step, round the end of the wall
        }

        // ---- difficult ground ----

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

            // the straight line is three squares of rubble at 2 apiece and a floor; round the
            // bottom is the same number of steps and cheaper to walk
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

            // rough, floor, floor - the square it started on is free
            Assert.Equal(4, Route.Cost(map, route));
            Assert.Equal(0, Route.Cost(map, null));
        }

        // ---- other pieces ----

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

        // a diagonal is a shortcut through one of the two ways round, so somebody standing in BOTH
        // of them closes it - which is what "occupancy blocks the intermediate squares" means
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

        // the square being left is never asked about - the piece standing there is the piece moving
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

            // the compiler asks for the predicate; this is what happens if something hands it null
            Assert.Null(Route.Between(map, new Cell(0, 0), new Cell(0, 0), null));
            Assert.Null(Route.Between(null, new Cell(0, 0), new Cell(0, 0), Empty));
        }

        // ---- the same click gives the same route ----

        // a heap ordered on cost alone reorders equal routes between runs, and the piece takes a
        // different way round the same wall on alternate presses. nothing about that looks like a
        // bug; it just feels haunted
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

        // ---- it finds the cheapest way, not merely a way ----

        // checked against a flood fill, which is slow, obviously correct, and knows nothing about
        // heuristics or heaps - so an A* that quietly stops being admissible has something to fail
        // against. every reachable square on a map with rubble, rock and walls on the lines
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

        // ---- as far as one move reaches (COMBAT_LOOP.md C2) ----

        [Fact]
        public void OneMoveStopsWhenTheBudgetRunsOut()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+-+-+-+",
                "|@ . . . . . . .|",
                "+-+-+-+-+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(7, 0));
            IReadOnlyList<Cell> reached = Route.Within(map, route, 5);

            // the square it left is free, so five squares of budget is five squares walked
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

        // whole squares only. a piece does not stop halfway into rubble because it could not
        // afford the second half of the step
        [Fact]
        public void DifficultGroundIsPaidForWholeOrNotEntered()
        {
            MapLayout map = Read(
                "+-+-+-+-+-+",
                "|@ . ~ . .|",
                "+-+-+-+-+-+");

            IReadOnlyList<Cell> route = Walk(map, new Cell(0, 0), new Cell(4, 0));

            // one floor at 1, then rubble at 2 - a budget of 2 buys the floor and stops
            IReadOnlyList<Cell> reached = Route.Within(map, route, 2);

            Assert.Equal(new Cell(1, 0), reached[reached.Count - 1]);

            // three buys the rubble as well
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
