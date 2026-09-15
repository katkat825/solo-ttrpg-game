using System.Collections.Generic;

namespace Core.Space
{
    // generic in its occupant - Core.Space can't name an Actor; game/ and the sim each pick their own
    public sealed class Grid<TOccupant> where TOccupant : class
    {
        public int Columns { get; }

        public int Rows { get; }

        // occupancy both ways, kept in step by a single write path
        // reference identity, not Equals - two equal-looking pieces are still two pieces
        readonly Dictionary<Cell, TOccupant> _byCell = new Dictionary<Cell, TOccupant>();

        readonly Dictionary<TOccupant, Cell> _byOccupant =
            new Dictionary<TOccupant, Cell>(ReferenceEqualityComparer.Instance);

        public Grid(int columns, int rows)
        {
            Columns = columns < 0 ? 0 : columns;
            Rows = rows < 0 ? 0 : rows;
        }

        public int Count => Columns * Rows;

        public IEnumerable<Cell> Cells
        {
            get
            {
                for (int y = 0; y < Rows; y++)
                    for (int x = 0; x < Columns; x++)
                        yield return new Cell(x, y);
            }
        }

        public bool Contains(Cell cell) =>
            cell.X >= 0 && cell.X < Columns && cell.Y >= 0 && cell.Y < Rows;

        public TOccupant At(Cell cell) =>
            _byCell.TryGetValue(cell, out TOccupant occupant) ? occupant : null;

        public bool IsOccupied(Cell cell) => _byCell.ContainsKey(cell);

        public Cell? CellOf(TOccupant occupant) =>
            occupant != null && _byOccupant.TryGetValue(occupant, out Cell cell) ? cell : null;

        public bool Place(TOccupant occupant, Cell cell)
        {
            if (occupant == null || !Contains(cell)) return false;

            if (_byCell.TryGetValue(cell, out TOccupant sitting))
                return ReferenceEquals(sitting, occupant);

            if (_byOccupant.TryGetValue(occupant, out Cell was)) _byCell.Remove(was);

            _byCell[cell] = occupant;
            _byOccupant[occupant] = cell;
            return true;
        }

        public bool Move(TOccupant occupant, Cell cell) =>
            occupant != null && _byOccupant.ContainsKey(occupant) && Place(occupant, cell);

        public bool Remove(TOccupant occupant)
        {
            if (occupant == null || !_byOccupant.TryGetValue(occupant, out Cell cell)) return false;

            _byOccupant.Remove(occupant);
            _byCell.Remove(cell);
            return true;
        }

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{Columns} x {Rows} grid, {_byOccupant.Count} of {Count} squares occupied";
    }
}
