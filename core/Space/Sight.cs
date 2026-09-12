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
    // TWO PROPERTIES, AND THEY ARE THE WHOLE SPECIFICATION:
    //
    //   SYMMETRY. If A can see B then B can see A. A line of sight that disagrees with itself is
    //   the bug players report as "it shot me through a wall", and it is the reason this traces one
    //   segment between two centres rather than casting a ray outward from an eye: the segment is
    //   the same segment whichever end it is drawn from. A test walks every pair on a map and holds
    //   it to this.
    //
    //   NEITHER END BLOCKS. You can see the wall you are standing next to, and you can see out of a
    //   doorway you are standing in. Only what is BETWEEN can stop a line, which also makes "is
    //   that wall visible from here" a question with an answer.
    //
    // The corner rule is the one judgement call. Where the line crosses exactly through the point
    // where four squares meet, it touches two of them, and the line is stopped only if BOTH are
    // opaque. That matches the movement rule - Route will not cut a corner where two walls touch -
    // so a piece cannot see through a gap it cannot walk through, and can see through the diagonal
    // slot between two walls that only touch at one corner. Getting these two rules to agree is
    // worth more than either answer is on its own.
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
                long decision = (long)(1 + 2 * across) * ny - (long)(1 + 2 * along) * nx;

                if (decision == 0)
                {
                    // straight through the point where four squares meet. the line touches both of
                    // the two it passes between, and is stopped only if they are both opaque
                    if (!map.IsTransparent(new Cell(x + sx, y)) &&
                        !map.IsTransparent(new Cell(x, y + sy)))
                        return false;

                    x += sx;
                    y += sy;
                    across++;
                    along++;
                }
                else if (decision < 0)
                {
                    x += sx;
                    across++;
                }
                else
                {
                    y += sy;
                    along++;
                }

                var here = new Cell(x, y);

                if (here == to) break;

                if (!map.IsTransparent(here)) return false;
            }

            return true;
        }
    }
}
