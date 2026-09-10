using System;

namespace Core.Space
{
    // one square of a grid, by integer coordinates
    //
    // TWO DIMENSIONS, AND NO OPINION ABOUT WHICH TWO. X and Y are the grid's own axes; Y is the
    // second one and NOT height. Core.Space is the model - which world plane a grid lies in, and
    // which way up, is the view's business, exactly as TrayBounds knows the tray's shape and
    // nothing about where the tray stands. game/Board maps a Cell to a point on the felt.
    //
    // a struct because a cell is a coordinate rather than a thing: two Cell(3, 4)s are the same
    // square, and everything downstream - occupancy, pathfinding in B3 - wants to compare and
    // hash them rather than track their identity
    public readonly struct Cell : IEquatable<Cell>
    {
        public int X { get; }

        public int Y { get; }

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(Cell other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Cell other && Equals(other);

        public override int GetHashCode() => unchecked((X * 397) ^ Y);

        public static bool operator ==(Cell a, Cell b) => a.Equals(b);

        public static bool operator !=(Cell a, Cell b) => !a.Equals(b);

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() => $"({X}, {Y})";
    }
}
