using System;
using System.Collections.Generic;

namespace Core.Space
{
    // how a piece gets from one square to another without walking through the walls - A* over the
    // map, and the answer is the squares it steps on.
    //
    // THIS IS WHERE core/ EARNS OUT (THE_BOARD.md B3). Pathfinding is exactly the kind of fiddly
    // geometry that is miserable to check by eye - a route that cuts one corner it should not looks
    // completely fine in motion - and trivial to check with a test. It is here rather than in
    // game/ for the same reason the grid is: it is pure, it is headless, and a balance sim that
    // one day gives enemies positions needs it without an engine behind it.
    //
    // The rules it enforces, all three of them tabletop rather than clever:
    //
    //   DIAGONALS COUNT AS ONE SQUARE. What a table does, and what CORE_RULES' whole
    //   arithmetic-free spirit asks for. No 1.4s, no alternating 1-2-1.
    //
    //   NO SQUEEZING THROUGH A SHUT CORNER. A diagonal step is refused when BOTH squares beside it
    //   are blocked - two walls that touch corner to corner are a seal, not a slot. One wall beside
    //   it is fine: rounding the outside corner of a wall is what a hand does with a piece, and a
    //   rule against it would be a rule nobody at a table has ever kept. This is the same rule
    //   `Sight` applies to a line crossing that corner, deliberately - a piece must not be able to
    //   see through a gap it cannot walk through. It would never be spotted by watching either way.
    //
    //   DIFFICULT GROUND COSTS DOUBLE, so a route goes round a rubble field it can walk round and
    //   through one it cannot - which is the entire behaviour Tile.Rough exists to produce.
    public static class Route
    {
        // the eight neighbours, orthogonals first. the order is the tie-break of last resort and so
        // it is part of the contract: the same click has to give the same route every time, or the
        // piece takes a different way round the same wall on alternate presses
        static readonly (int X, int Y)[] Neighbours =
        {
            (0, -1), (1, 0), (0, 1), (-1, 0),
            (1, -1), (1, 1), (-1, 1), (-1, -1),
        };

        // the squares to step on, FROM and TO included, or null if there is no way there.
        //
        // <paramref name="occupied"/> is asked about every square except the one being left, and it
        // is REQUIRED rather than defaulted. A route that quietly walked through whoever is standing
        // in the way would look right for the whole of phase B - there is one piece on the board -
        // and be wrong the moment Phase C puts a second one down. F4's lesson: where a default would
        // decide something the caller ought to have said, make the compiler ask. Pass `_ => false`
        // to mean an empty board and mean it.
        public static IReadOnlyList<Cell> Between(MapLayout map, Cell from, Cell to, Func<Cell, bool> occupied)
        {
            if (map == null || occupied == null) return null;

            // a piece can always be where it already is, whatever it is standing on - a map edited
            // under a piece must not make it unable to move
            if (from == to) return new[] { from };

            if (!Open(map, to, occupied)) return null;

            var cameFrom = new Dictionary<Cell, Cell>();
            var best = new Dictionary<Cell, int> { [from] = 0 };

            // (estimate, remaining, x, y) - every part after the first is a tie-break, and together
            // they are total, so the queue cannot reorder equal-cost routes between runs. a heap
            // ordered on cost alone is deterministic in nothing
            var open = new PriorityQueue<Cell, (int Estimate, int Remaining, int X, int Y)>();

            open.Enqueue(from, (Estimate(from, to), Estimate(from, to), from.X, from.Y));

            while (open.TryDequeue(out Cell here, out _))
            {
                if (here == to) return Rebuild(cameFrom, here, from);

                int cost = best[here];

                foreach ((int dx, int dy) in Neighbours)
                {
                    var next = new Cell(here.X + dx, here.Y + dy);

                    if (!Open(map, next, occupied)) continue;

                    // a shut corner: both squares beside the diagonal blocked. asked of the MAP
                    // only - a piece standing beside the corner narrows the room, it does not
                    // seal it, and a queue of minis must not be able to lock a doorway shut
                    if (dx != 0 && dy != 0 &&
                        !map.IsPassable(new Cell(here.X + dx, here.Y)) &&
                        !map.IsPassable(new Cell(here.X, here.Y + dy)))
                        continue;

                    int through = cost + map.At(next).MoveCost();

                    if (best.TryGetValue(next, out int already) && already <= through) continue;

                    best[next] = through;
                    cameFrom[next] = here;

                    int remaining = Estimate(next, to);
                    open.Enqueue(next, (through + remaining, remaining, next.X, next.Y));
                }
            }

            return null;
        }

        // is there any way there at all - the same question, without building the route
        public static bool Exists(MapLayout map, Cell from, Cell to, Func<Cell, bool> occupied) =>
            Between(map, from, to, occupied) != null;

        // what a route costs to walk, counting every square entered and not the one left. null in,
        // and it is nothing - a route that does not exist has no length worth arguing about
        public static int Cost(MapLayout map, IReadOnlyList<Cell> route)
        {
            if (map == null || route == null) return 0;

            int cost = 0;

            for (int i = 1; i < route.Count; i++) cost += map.At(route[i]).MoveCost();

            return cost;
        }

        static bool Open(MapLayout map, Cell cell, Func<Cell, bool> occupied) =>
            map.IsPassable(cell) && !occupied(cell);

        // Chebyshev, because a diagonal is one square. it never overestimates - every step costs at
        // least Tiles.MinimumCost - which is what keeps A* from returning a route that merely looks
        // plausible, and difficult ground is deliberately not guessed at here
        static int Estimate(Cell from, Cell to) =>
            Math.Max(Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y)) * Tiles.MinimumCost;

        static IReadOnlyList<Cell> Rebuild(Dictionary<Cell, Cell> cameFrom, Cell last, Cell first)
        {
            var route = new List<Cell> { last };

            while (route[route.Count - 1] != first) route.Add(cameFrom[route[route.Count - 1]]);

            route.Reverse();
            return route;
        }
    }
}
