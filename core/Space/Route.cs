using System;
using System.Collections.Generic;

namespace Core.Space
{
    // diagonals count as one square; a diagonal needs one of the two ways round the corner open; difficult ground costs double
    public static class Route
    {
        // orthogonals first - the order is the last-resort tie-break, so the same click gives the same route
        static readonly (int X, int Y)[] Neighbours =
        {
            (0, -1), (1, 0), (0, 1), (-1, 0),
            (1, -1), (1, 1), (-1, 1), (-1, -1),
        };

        // occupied is required, not defaulted - a route through an occupant looks right with one piece and breaks with two
        public static IReadOnlyList<Cell> Between(MapLayout map, Cell from, Cell to, Func<Cell, bool> occupied)
        {
            if (map == null || occupied == null) return null;

            if (from == to) return new[] { from };

            if (!Standable(map, to, occupied)) return null;

            var cameFrom = new Dictionary<Cell, Cell>();
            var best = new Dictionary<Cell, int> { [from] = 0 };

            // (estimate, remaining, x, y) - the tail is a total tie-break, so equal-cost routes don't reorder between runs
            var open = new PriorityQueue<Cell, (int Estimate, int Remaining, int X, int Y)>();

            open.Enqueue(from, (Estimate(from, to), Estimate(from, to), from.X, from.Y));

            while (open.TryDequeue(out Cell here, out _))
            {
                if (here == to) return Rebuild(cameFrom, here, from);

                int cost = best[here];

                foreach ((int dx, int dy) in Neighbours)
                {
                    var next = new Cell(here.X + dx, here.Y + dy);

                    if (!CanStep(map, here, next, dx, dy, occupied)) continue;

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

        static bool CanStep(MapLayout map, Cell from, Cell to, int dx, int dy, Func<Cell, bool> occupied)
        {
            if (dx == 0 || dy == 0) return Crosses(map, from, to, occupied);

            var sideways = new Cell(from.X + dx, from.Y);
            var forward = new Cell(from.X, from.Y + dy);

            return (Crosses(map, from, sideways, occupied) && Crosses(map, sideways, to, occupied))
                || (Crosses(map, from, forward, occupied) && Crosses(map, forward, to, occupied));
        }

        static bool Crosses(MapLayout map, Cell from, Cell to, Func<Cell, bool> occupied) =>
            map.CanCross(from, to) && !occupied(to);

        public static bool Exists(MapLayout map, Cell from, Cell to, Func<Cell, bool> occupied) =>
            Between(map, from, to, occupied) != null;

        public static int Cost(MapLayout map, IReadOnlyList<Cell> route)
        {
            if (map == null || route == null) return 0;

            int cost = 0;

            for (int i = 1; i < route.Count; i++) cost += map.At(route[i]).MoveCost();

            return cost;
        }

        public static IReadOnlyList<Cell> Within(MapLayout map, IReadOnlyList<Cell> route, int budget)
        {
            if (map == null || route == null || route.Count == 0) return route;

            var reached = new List<Cell> { route[0] };

            int spent = 0;

            for (int i = 1; i < route.Count; i++)
            {
                int step = map.At(route[i]).MoveCost();

                if (spent + step > budget) break;

                spent += step;
                reached.Add(route[i]);
            }

            return reached;
        }

        static bool Standable(MapLayout map, Cell cell, Func<Cell, bool> occupied) =>
            map.IsPassable(cell) && !occupied(cell);

        // Chebyshev, because a diagonal is one square - never overestimates, so A* stays admissible
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
