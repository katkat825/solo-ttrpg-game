using System;

namespace Core.Space
{
    // WHICH line - one edge of the grid, named once and the same way from both sides.
    //
    // `Edge` says what stands on a line; this says which line it is. They are apart for the reason
    // `Cell` and `Tile` are apart: a place is not a thing, and half the code here only ever cares
    // about the place (a view drawing every wall) while the other half only cares about the thing
    // (a route asking whether it can cross).
    //
    // CANONICAL BY CONSTRUCTION, which is the whole reason this is a type rather than a pair of
    // cells. Every line has two cells beside it and would otherwise have two names, so a wall put
    // up from one side could be looked for from the other and not found. Here a line is always
    // named by the cell to its EAST (vertical) or to its SOUTH (horizontal) - `Cell` - with the
    // other one `Across`. Both directions of `Between` land on the same Border, and the map can
    // index one array.
    //
    // A W x H map has (W+1) x H vertical borders and W x (H+1) horizontal ones, so the outer
    // boundary is nameable too: the north border of row 0, the west border of column 0. Those
    // carry walls like any other line, which is how a map's outside edge is built when the author
    // does not want a ring of solid rock.
    public readonly struct Border : IEquatable<Border>
    {
        // the cell east of a vertical line, or south of a horizontal one. it may be one past the
        // last column or row - that is the map's outer boundary, and it is a real line
        public Cell Cell { get; }

        // true: the line runs north-south, up the west side of Cell
        // false: it runs east-west, along the north side of Cell
        public bool Vertical { get; }

        public Border(Cell cell, bool vertical)
        {
            Cell = cell;
            Vertical = vertical;
        }

        // the four sides of a square, each landing on the canonical name of that line
        public static Border West(Cell cell) => new Border(cell, true);

        public static Border East(Cell cell) => new Border(new Cell(cell.X + 1, cell.Y), true);

        public static Border North(Cell cell) => new Border(cell, false);

        public static Border South(Cell cell) => new Border(new Cell(cell.X, cell.Y + 1), false);

        // the line between two squares, or false when they are not orthogonally side by side -
        // diagonals share a CORNER rather than a line, and nothing can stand on a corner
        public static bool Between(Cell a, Cell b, out Border border)
        {
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;

            if (dx == 0 && (dy == 1 || dy == -1))
            {
                border = dy == 1 ? South(a) : North(a);
                return true;
            }

            if (dy == 0 && (dx == 1 || dx == -1))
            {
                border = dx == 1 ? East(a) : West(a);
                return true;
            }

            border = default;
            return false;
        }

        // the square on the other side of the line from Cell - west of a vertical one, north of a
        // horizontal one. it may be off the map, and that is not an error: the outside of a
        // boundary wall is Void, and Void is where a map stops
        public Cell Across => Vertical
            ? new Cell(Cell.X - 1, Cell.Y)
            : new Cell(Cell.X, Cell.Y - 1);

        public bool Equals(Border other) => Cell == other.Cell && Vertical == other.Vertical;

        public override bool Equals(object obj) => obj is Border other && Equals(other);

        public override int GetHashCode() => unchecked((Cell.GetHashCode() * 397) ^ (Vertical ? 1 : 0));

        public static bool operator ==(Border a, Border b) => a.Equals(b);

        public static bool operator !=(Border a, Border b) => !a.Equals(b);

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            Vertical ? $"|{Cell.X}, {Cell.Y}" : $"-{Cell.X}, {Cell.Y}";
    }
}
