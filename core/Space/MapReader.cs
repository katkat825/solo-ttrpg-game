using System.Collections.Generic;
using System.Text;

namespace Core.Space
{
    // a map, read from text. one character per square, and the picture in the file is the picture
    // on the table.
    //
    // WHY NOT JSON. ARCHITECTURE.md 7 says hand-write the data files until it hurts, and then
    // predicts exactly where it will hurt first: "grid maps in hand-written JSON are genuinely
    // miserable within about an hour". They are - a JSON grid is a wall of quoted numbers where a
    // typo is invisible and a room cannot be seen at all. That prediction is an argument against
    // JSON FOR MAPS, not an argument for building the editor sooner, so this takes the other way
    // out: the oldest map format there is, where the room is drawn in the file. Moving a wall is
    // one character, the diff shows the room, and the editor stays a Phase P luxury instead of a
    // Phase B blocker. Everything else - monsters, items, encounters - stays JSON as planned.
    //
    // TEXT IN, NOT A PATH IN. core/ never opens a file: in an exported game a map lives inside a
    // .pck and only Godot's FileAccess can reach it, so the caller reads the bytes and this parses
    // them. That is what keeps the parser testable headless, and it is the same seam the whole
    // engine already runs on.
    //
    // Every failure is REPORTED AND NAMED, with the line number in the file the author is looking
    // at. A map that half-loads is a room with a wall missing and no way to know it - the failure
    // this exists to make impossible. The problem string is a developer diagnostic: a broken map
    // is a content bug found at load, never something a player reads.
    public static class MapReader
    {
        // a line starting with this is a note to whoever is editing the map. ';' rather than '#'
        // because '#' is a wall, and a comment character that is also a glyph is a trap waiting
        // for the first map with a wall in column zero. Godot's own .tscn and .cfg use ';' too
        public const char Comment = ';';

        // where the hero's piece starts. the square underneath is floor - a start you can see in
        // the map is a start that cannot drift into a wall
        public const char StartGlyph = '@';

        // THE LEGEND, AND THE ONLY STATEMENT OF IT. The map file describes it in a comment for
        // whoever is reading the file, and an unknown glyph prints this - so the copy in the file
        // is a courtesy that cannot silently become wrong, because the error message is derived
        // from here and the file's comment is not what anything parses
        static readonly (char Glyph, Tile Tile, string Name)[] Table =
        {
            ('.', Tile.Floor, "floor"),
            ('#', Tile.Wall, "wall"),
            ('+', Tile.Door, "door, shut"),
            ('~', Tile.Rough, "difficult ground"),
        };

        // derived, never listed - the same rule EngineKeys and GameKeys run on
        public static string Legend
        {
            get
            {
                var legend = new StringBuilder();

                foreach ((char glyph, Tile _, string name) in Table)
                    legend.Append(glyph).Append(' ').Append(name).Append(", ");

                return legend.Append(StartGlyph).Append(" where the hero starts").ToString();
            }
        }

        public static bool TryRead(string text, out MapLayout map, out string problem)
        {
            map = null;
            problem = null;

            // the line number in the FILE, not in the grid - the author is looking at the file
            List<(int Line, string Text)> rows = Rows(text ?? "");

            if (rows.Count == 0)
            {
                problem = "no map in it - every line was blank or a comment";
                return false;
            }

            int columns = rows[0].Text.Length;

            foreach ((int line, string row) in rows)
                if (row.Length != columns)
                {
                    problem = $"line {line} is {row.Length} squares wide but line {rows[0].Line} " +
                              $"is {columns} - every row of a map has to be the same length";
                    return false;
                }

            var tiles = new Tile[columns * rows.Count];
            Cell? start = null;

            for (int y = 0; y < rows.Count; y++)
            {
                (int line, string row) = rows[y];

                for (int x = 0; x < columns; x++)
                {
                    char glyph = row[x];

                    if (glyph == StartGlyph)
                    {
                        if (start != null)
                        {
                            problem = $"line {line} column {x + 1} is a second '{StartGlyph}' - " +
                                      $"the hero already starts on {start} and cannot start twice";
                            return false;
                        }

                        start = new Cell(x, y);
                        tiles[y * columns + x] = Tile.Floor;
                        continue;
                    }

                    if (!Lookup(glyph, out Tile tile))
                    {
                        problem = $"line {line} column {x + 1} is '{glyph}', which is not a tile. " +
                                  $"the legend is: {Legend}";
                        return false;
                    }

                    tiles[y * columns + x] = tile;
                }
            }

            if (start == null)
            {
                problem = $"no '{StartGlyph}' anywhere in it - a map has to say where the hero starts";
                return false;
            }

            map = new MapLayout(columns, rows.Count, tiles, start.Value);
            return true;
        }

        static bool Lookup(char glyph, out Tile tile)
        {
            foreach ((char g, Tile t, string _) in Table)
                if (g == glyph)
                {
                    tile = t;
                    return true;
                }

            tile = Tile.Void;
            return false;
        }

        // the lines that are map, carrying the line number they came from
        //
        // trailing whitespace goes, because no glyph is a space and an editor that strips it - or
        // does not - must not change what the map means. LEADING whitespace stays: it is inside
        // the grid, where a space is not a tile, so an indented row is an error that gets named
        // rather than a map that quietly shifts one column left
        static List<(int Line, string Text)> Rows(string text)
        {
            var rows = new List<(int, string)>();
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();

                if (line.Length == 0 || line.TrimStart().StartsWith(Comment.ToString())) continue;

                rows.Add((i + 1, line));
            }

            return rows;
        }
    }
}
