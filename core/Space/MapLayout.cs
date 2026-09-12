using System.Collections.Generic;

namespace Core.Space
{
    // one map: how big it is, what every square is made of, and where the hero starts.
    //
    // THE MAP IS DATA AND THIS IS WHAT DATA BECOMES. `MapReader` builds it out of a text file;
    // nothing constructs one out of code except a test. That is the whole point of B2 - changing
    // the map means editing a file, never a `.tscn` and never a `.cs` - and it is the first place
    // ARCHITECTURE.md's engine/content boundary shows up on the board.
    //
    // IMMUTABLE, AND THAT IS THE INTERESTING PART. A map is content; a door standing open is
    // runtime state. Keeping the layout read-only means a saved game carries what has changed
    // rather than a copy of the map, and reloading a campaign cannot quietly inherit last
    // session's doors. B4 opens a door and will hold that over the top of this, not inside it.
    //
    // It holds no occupancy either - `Grid` does. Two structures over one extent, built from the
    // same numbers by the one caller that read the file, because who is standing where changes
    // every second and what the room is made of does not change at all.
    public sealed class MapLayout
    {
        public int Columns { get; }

        public int Rows { get; }

        // where the hero's piece starts, which the file marks with a glyph rather than stating in
        // coordinates - a spawn you can SEE in the map is a spawn that cannot drift into a wall
        //
        // one, not a list. Phase C puts enemies on the board and that is when this grows a spawn
        // per side; inventing the general case now would be a list with one entry and a comment
        // apologising for it
        public Cell Start { get; }

        // row-major, and private: everything outside asks At(), so no caller can index it with a
        // cell the map does not have
        readonly Tile[] _tiles;

        public MapLayout(int columns, int rows, Tile[] tiles, Cell start)
        {
            Columns = columns < 0 ? 0 : columns;
            Rows = rows < 0 ? 0 : rows;
            Start = start;

            _tiles = tiles != null && tiles.Length == Columns * Rows ? tiles : new Tile[Columns * Rows];
        }

        public int Count => Columns * Rows;

        public bool Contains(Cell cell) =>
            cell.X >= 0 && cell.X < Columns && cell.Y >= 0 && cell.Y < Rows;

        // OFF THE MAP IS Void, NOT AN EXCEPTION AND NOT A WALL. Route and Sight walk cells that may
        // step outside, and both want one answer for "there is nothing there" - Void is impassable
        // and opaque, so neither needs a bounds check of its own and neither can be told a wall
        // exists where the map simply stops
        public Tile At(Cell cell) => Contains(cell) ? _tiles[cell.Y * Columns + cell.X] : Tile.Void;

        public bool IsPassable(Cell cell) => At(cell).IsPassable();

        public bool IsTransparent(Cell cell) => At(cell).IsTransparent();

        // row-major, the same order Grid.Cells uses, so a view can walk both together
        public IEnumerable<Cell> Cells
        {
            get
            {
                for (int y = 0; y < Rows; y++)
                    for (int x = 0; x < Columns; x++)
                        yield return new Cell(x, y);
            }
        }

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() => $"{Columns} x {Rows} map, hero starts on {Start}";
    }
}
