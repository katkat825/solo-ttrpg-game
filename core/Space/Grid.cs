using System.Collections.Generic;

namespace Core.Space
{
    // how big the board is and who is standing where - the whole of B0's spatial model
    //
    // pure and Godot-free like everything in core/, which is what makes the board testable
    // headless and simulatable later. CONVENTIONS.md's membership test is "pure, headless,
    // Godot-free", not "is it literally a rule" - the same reason Localization and Statistics
    // are here. game/Board reads this and draws it, the way the tray draws a PoolResult.
    //
    // GENERIC IN ITS OCCUPANT ON PURPOSE. Core.Space is a bottom-level namespace beside Dice and
    // Statistics, so it cannot name an Actor without dependencies pointing the wrong way, and it
    // has no business knowing what stands on a square anyway. game/ puts its minis on one; a
    // future balance sim puts Actors on one; neither needs the other to exist.
    //
    // WHAT THIS IS NOT: it holds no terrain. B2's map - which square is floor and which is wall -
    // is a separate structure over the same extent, because the layout is CONTENT read from a
    // data file and occupancy is runtime state. Keeping them apart is what stops a saved game
    // from having to carry a copy of the map.
    public sealed class Grid<TOccupant> where TOccupant : class
    {
        public int Columns { get; }

        public int Rows { get; }

        // occupancy both ways round: where is this piece, and who is on this square. one write
        // path keeps them from disagreeing, which is the whole reason they aren't two fields on
        // whatever owns the board
        //
        // reference identity, not Equals: two pieces that consider themselves equal are still two
        // pieces, and a board that quietly merged them would be a bug nobody could see
        readonly Dictionary<Cell, TOccupant> _byCell = new Dictionary<Cell, TOccupant>();

        readonly Dictionary<TOccupant, Cell> _byOccupant =
            new Dictionary<TOccupant, Cell>(ReferenceEqualityComparer.Instance);

        public Grid(int columns, int rows)
        {
            Columns = columns < 0 ? 0 : columns;
            Rows = rows < 0 ? 0 : rows;
        }

        public int Count => Columns * Rows;

        // row-major, and the order is part of the contract: the view builds its tiles from this
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

        // null for an empty square and for one that isn't on the board - "nobody there" is the
        // honest answer to both, and a caller that cares which asks Contains
        public TOccupant At(Cell cell) =>
            _byCell.TryGetValue(cell, out TOccupant occupant) ? occupant : null;

        public bool IsOccupied(Cell cell) => _byCell.ContainsKey(cell);

        // null when this piece isn't on the board at all
        public Cell? CellOf(TOccupant occupant) =>
            occupant != null && _byOccupant.TryGetValue(occupant, out Cell cell) ? cell : null;

        // false rather than an exception: a refused move is an ordinary thing that happens - B3
        // has a click on a wall doing it - and the caller shows it on the board
        public bool Place(TOccupant occupant, Cell cell)
        {
            if (occupant == null || !Contains(cell)) return false;

            // already there is a success, not a no-op to argue about
            if (_byCell.TryGetValue(cell, out TOccupant sitting))
                return ReferenceEquals(sitting, occupant);

            if (_byOccupant.TryGetValue(occupant, out Cell was)) _byCell.Remove(was);

            _byCell[cell] = occupant;
            _byOccupant[occupant] = cell;
            return true;
        }

        // Place, but only for a piece already standing somewhere. the distinction matters to the
        // view: moving is animated, placing is how a piece arrives
        public bool Move(TOccupant occupant, Cell cell) =>
            occupant != null && _byOccupant.ContainsKey(occupant) && Place(occupant, cell);

        public bool Remove(TOccupant occupant)
        {
            if (occupant == null || !_byOccupant.TryGetValue(occupant, out Cell cell)) return false;

            _byOccupant.Remove(occupant);
            _byCell.Remove(cell);
            return true;
        }

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            $"{Columns} x {Rows} grid, {_byOccupant.Count} of {Count} squares occupied";
    }
}
