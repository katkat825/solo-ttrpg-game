using System;

namespace Core.Space
{
    // integer grid coordinates - Y is the second axis, not height; the view decides the world plane
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

        // debug only, never localized - keep it off the screen
        public override string ToString() => $"({X}, {Y})";
    }
}
