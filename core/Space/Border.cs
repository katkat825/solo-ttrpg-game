using System;

namespace Core.Space
{
    // canonical: a line is always named by the cell to its east (vertical) or south (horizontal), so both sides agree
    public readonly struct Border : IEquatable<Border>
    {
        public Cell Cell { get; }

        public bool Vertical { get; }

        public Border(Cell cell, bool vertical)
        {
            Cell = cell;
            Vertical = vertical;
        }

        public static Border West(Cell cell) => new Border(cell, true);

        public static Border East(Cell cell) => new Border(new Cell(cell.X + 1, cell.Y), true);

        public static Border North(Cell cell) => new Border(cell, false);

        public static Border South(Cell cell) => new Border(new Cell(cell.X, cell.Y + 1), false);

        // false when the two aren't orthogonally adjacent - diagonals share a corner, not a line
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

        public Cell Across => Vertical
            ? new Cell(Cell.X - 1, Cell.Y)
            : new Cell(Cell.X, Cell.Y - 1);

        public bool Equals(Border other) => Cell == other.Cell && Vertical == other.Vertical;

        public override bool Equals(object obj) => obj is Border other && Equals(other);

        public override int GetHashCode() => unchecked((Cell.GetHashCode() * 397) ^ (Vertical ? 1 : 0));

        public static bool operator ==(Border a, Border b) => a.Equals(b);

        public static bool operator !=(Border a, Border b) => !a.Equals(b);

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            Vertical ? $"|{Cell.X}, {Cell.Y}" : $"-{Cell.X}, {Cell.Y}";
    }
}
