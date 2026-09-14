using System.Collections.Generic;

namespace Core.Space
{
    // one map: how big it is, what every square is made of, what stands on the lines between them,
    // and where the hero starts.
    //
    // THE MAP IS DATA AND THIS IS WHAT DATA BECOMES. `MapReader` builds it out of a text file;
    // nothing constructs one out of code except a test. That is the whole point of B2 - changing
    // the map means editing a file, never a `.tscn` and never a `.cs` - and it is the first place
    // ARCHITECTURE.md's engine/content boundary shows up on the board.
    //
    // TWO LAYERS OVER ONE EXTENT (EDGE_WALLS.md). Cells say what a square IS - floor, difficult
    // ground, solid rock. Edges say what stands on the LINE between two squares - nothing, a wall,
    // a shut door. A wall is not a square any more, which is the rework this file exists in the
    // middle of: at a table the wall is drawn on the grid line and the squares on both sides are
    // ordinary floor you can stand on.
    //
    // IMMUTABLE, AND THAT IS THE INTERESTING PART. A map is content; a door standing open is
    // runtime state. Keeping the layout read-only means a saved game carries what has changed
    // rather than a copy of the map, and reloading a campaign cannot quietly inherit last
    // session's doors. B4 opens a door by taking a new layout with that one edge changed.
    //
    // It holds no occupancy either - `Grid` does. Three structures over one extent, built from the
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

        // the lines. vertical is (Columns + 1) x Rows - one more column of lines than of squares,
        // because the map's own west and east boundaries are lines too - and horizontal is
        // Columns x (Rows + 1) for the same reason
        readonly Edge[] _vertical;

        readonly Edge[] _horizontal;

        public MapLayout(int columns, int rows, Tile[] tiles, Cell start,
                         Edge[] vertical = null, Edge[] horizontal = null)
        {
            Columns = columns < 0 ? 0 : columns;
            Rows = rows < 0 ? 0 : rows;
            Start = start;

            _tiles = Sized(tiles, Columns * Rows);
            _vertical = Sized(vertical, (Columns + 1) * Rows);
            _horizontal = Sized(horizontal, Columns * (Rows + 1));
        }

        // a layer that is the wrong size is a caller with a bug, and a half-filled map is the
        // failure MapReader exists to make impossible - so it is dropped for an empty one of the
        // right size rather than indexed into and trusted
        static T[] Sized<T>(T[] layer, int wanted) =>
            layer != null && layer.Length == wanted ? layer : new T[wanted];

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

        // ---- the lines ----

        public bool Contains(Border border) => border.Vertical
            ? border.Cell.X >= 0 && border.Cell.X <= Columns &&
              border.Cell.Y >= 0 && border.Cell.Y < Rows
            : border.Cell.X >= 0 && border.Cell.X < Columns &&
              border.Cell.Y >= 0 && border.Cell.Y <= Rows;

        // a line the map does not have is None rather than Wall, and deliberately: it is a line
        // out in the Void beyond the map, and the VOID is what stops anything going that way. A
        // wall invented out there would be a second reason for the same refusal, and the day they
        // disagreed the map would be lying
        public Edge At(Border border)
        {
            if (!Contains(border)) return Edge.None;

            return border.Vertical
                ? _vertical[border.Cell.Y * (Columns + 1) + border.Cell.X]
                : _horizontal[border.Cell.Y * Columns + border.Cell.X];
        }

        // what stands on the line between two squares - and Wall for two squares that are not side
        // by side, because there is no line there and no crossing to allow
        public Edge Between(Cell a, Cell b) =>
            Border.Between(a, b, out Border border) ? At(border) : Edge.Wall;

        // THE ONE QUESTION A ROUTE ASKS: may a piece step from a to b. Orthogonally adjacent, the
        // square it is entering is one that can be stood on, and nothing on the line between
        public bool CanCross(Cell a, Cell b) =>
            Border.Between(a, b, out Border border) && At(border).IsOpen() && IsPassable(b);

        // and the one a line of sight asks. the CELL is not checked here: "neither end blocks" is
        // Sight's rule and only Sight knows which cells are its ends
        public bool CanSee(Cell a, Cell b) =>
            Border.Between(a, b, out Border border) && At(border).IsTransparent();

        // every line on the map, vertical first, in the order a view would draw them. (W+1)*H plus
        // W*(H+1) of them, and almost all are None - a caller that wants the walls filters
        public IEnumerable<Border> Borders
        {
            get
            {
                for (int y = 0; y < Rows; y++)
                    for (int x = 0; x <= Columns; x++)
                        yield return new Border(new Cell(x, y), true);

                for (int y = 0; y <= Rows; y++)
                    for (int x = 0; x < Columns; x++)
                        yield return new Border(new Cell(x, y), false);
            }
        }

        // ---- one thing changed ----

        // the same map with one square changed - a frame collapsed into rubble. A NEW MAP, because
        // this one is content and content does not change.
        //
        // B4 is what needed it and the shape is what matters beyond B4: what a session accumulates
        // is a list of squares and lines that are no longer what the file said, which is exactly
        // what a save has to write down and exactly what reloading a campaign has to not inherit.
        // Copying the whole layout to change one square costs nothing at map sizes - a room is a
        // few hundred bytes - and buys a model where nobody can quietly edit the dungeon.
        public MapLayout With(Cell cell, Tile tile)
        {
            if (!Contains(cell) || At(cell) == tile) return this;

            var changed = (Tile[])_tiles.Clone();
            changed[cell.Y * Columns + cell.X] = tile;

            return new MapLayout(Columns, Rows, changed, Start, _vertical, _horizontal);
        }

        // and the same map with one LINE changed, which is how a door opens
        public MapLayout With(Border border, Edge edge)
        {
            if (!Contains(border) || At(border) == edge) return this;

            var vertical = _vertical;
            var horizontal = _horizontal;

            if (border.Vertical)
            {
                vertical = (Edge[])_vertical.Clone();
                vertical[border.Cell.Y * (Columns + 1) + border.Cell.X] = edge;
            }
            else
            {
                horizontal = (Edge[])_horizontal.Clone();
                horizontal[border.Cell.Y * Columns + border.Cell.X] = edge;
            }

            return new MapLayout(Columns, Rows, _tiles, Start, vertical, horizontal);
        }

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
        public override string ToString()
        {
            int walls = 0;
            int doors = 0;

            foreach (Border border in Borders)
            {
                if (At(border) == Edge.Wall) walls++;
                else if (At(border) == Edge.Door) doors++;
            }

            return $"{Columns} x {Rows} map, {walls} walls and {doors} doors on the lines, " +
                   $"hero starts on {Start}";
        }
    }
}
