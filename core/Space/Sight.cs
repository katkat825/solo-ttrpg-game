namespace Core.Space
{
    // can one square see another - a straight line between two centres, and what it passes through.
    //
    // Nothing looks at this yet. It is built in B3 because it is geometry and because combat leans
    // on it hard the moment it exists: a ranged attack needs to know it has a shot, cover needs to
    // know the line is partly blocked, and a DM needs to know what the room can see. Building it
    // beside the pathfinding means both fiddly halves of the same geometry get tested at once,
    // against cases drawn on paper, rather than one of them being written in a hurry inside
    // COMBAT_LOOP.md with a fight on top of it.
    //
    // SINCE EDGE_WALLS.md IT IS THE LINES THAT STOP IT. The walk steps from square to square and
    // each step CROSSES a line; a wall or a shut door on that line blocks the view. A square can
    // still stop it too - solid rock is opaque - but a thin wall between two floor squares is a
    // line, and it always was at a table.
    //
    // TWO PROPERTIES, AND THEY ARE THE WHOLE SPECIFICATION:
    //
    //   SYMMETRY. If A can see B then B can see A. A line of sight that disagrees with itself is
    //   the bug players report as "it shot me through a wall", and it is the reason this traces one
    //   segment between two centres rather than casting a ray outward from an eye: the segment is
    //   the same segment whichever end it is drawn from, and a line between two squares is the same
    //   line from both. A test walks every pair on a map and holds it to this.
    //
    //   NEITHER END BLOCKS. You can see the rock you are standing against, and out of the square
    //   you are in. Only what is BETWEEN can stop a line, which also makes "is that pillar visible
    //   from here" a question with an answer. The LINES are not exempt: a wall between you and the
    //   next square blocks the view into it, even though the square itself is see-through.
    //
    // The corner rule is the one judgement call, and it is `Route`'s: where the line passes exactly
    // through the point four squares meet, it goes through if EITHER way round the corner is clear
    // the whole way. That is the same rule a piece walks by, so a piece can never see through a gap
    // it could not walk through, and can see down the diagonal slot between two walls that only
    // touch at a corner. Getting these two to agree is worth more than either answer on its own.
    public static class Sight
    {
        public static bool Clear(MapLayout map, Cell from, Cell to)
        {
            if (map == null) return false;

            if (from == to) return true;

            int dx = to.X - from.X;
            int dy = to.Y - from.Y;

            int nx = dx < 0 ? -dx : dx;
            int ny = dy < 0 ? -dy : dy;

            int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0;
            int sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;

            int x = from.X;
            int y = from.Y;

            // how far along each axis the walk has come, as halves of a square: the next crossing
            // sideways is at (1 + 2*across) / 2nx of the way, and upward at (1 + 2*along) / 2ny.
            // comparing them by cross-multiplication keeps the whole thing in integers, which is
            // what makes it EXACTLY symmetric - a float would decide a corner one way from one end
            // and the other way from the other, on a rounding bit
            int across = 0;
            int along = 0;

            while (across < nx || along < ny)
            {
                var here = new Cell(x, y);
                long decision = (long)(1 + 2 * across) * ny - (long)(1 + 2 * along) * nx;

                if (decision == 0)
                {
                    // straight through the point where four squares meet. it goes through if either
                    // way round is clear - Route's rule, in sight's terms
                    var corner = new Cell(x + sx, y + sy);
                    var sideways = new Cell(x + sx, y);
                    var forward = new Cell(x, y + sy);

                    bool round = (Steps(map, here, sideways, to) && Steps(map, sideways, corner, to))
                              || (Steps(map, here, forward, to) && Steps(map, forward, corner, to));

                    if (!round) return false;

                    x += sx;
                    y += sy;
                    across++;
                    along++;
                }
                else if (decision < 0)
                {
                    if (!Steps(map, here, new Cell(x + sx, y), to)) return false;

                    x += sx;
                    across++;
                }
                else
                {
                    if (!Steps(map, here, new Cell(x, y + sy), to)) return false;

                    y += sy;
                    along++;
                }

                if (new Cell(x, y) == to) break;
            }

            return true;
        }

        // one square to the next: nothing on the line between them, and the square arrived at is
        // see-through - unless it is the far end of the whole line, which never blocks
        static bool Steps(MapLayout map, Cell from, Cell to, Cell target) =>
            map.CanSee(from, to) && (to == target || map.IsTransparent(to));
    }
}
