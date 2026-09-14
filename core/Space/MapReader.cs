using System.Collections.Generic;
using System.Text;

namespace Core.Space
{
    // a map, read from text - and since EDGE_WALLS.md the text is drawn at DOUBLE RESOLUTION, so
    // the walls can be on the lines where they belong.
    //
    //     +-+-+-+
    //     |. . .|
    //     + +x+ +
    //     |.|#|~|
    //     +-+-+-+
    //
    // A W x H map is (2H+1) lines of (2W+1) characters, and where a character sits is what it
    // means: odd line and odd column is a SQUARE, even line and odd column is the line ACROSS the
    // top of that square, odd line and even column is the line UP its left side, and both even is a
    // corner - scaffolding for the eye, parsed for nothing. Three floor cells open to each other,
    // a shut door under the middle one, a rock pillar and a patch of difficult ground below.
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
    // Every failure is REPORTED AND NAMED, with the line and column in the file the author is
    // looking at. A map that half-loads is a room with a wall missing and no way to know it - the
    // failure this exists to make impossible, and the reason check-maps.ps1 runs every shipped map
    // through here. The problem string is a developer diagnostic: a broken map is a content bug
    // found at load, never something a player reads.
    public static class MapReader
    {
        // a line starting with this is a note to whoever is editing the map. ';' rather than '#'
        // because '#' is rock, and a comment character that is also a glyph is a trap waiting for
        // the first map with rock in column zero. Godot's own .tscn and .cfg use ';' too
        public const char Comment = ';';

        // where the hero's piece starts. the square underneath is floor - a start you can see in
        // the map is a start that cannot drift into a wall
        public const char StartGlyph = '@';

        // a junction, drawn so the grid reads as a grid. it carries no meaning, and a space is
        // equally fine there - but nothing ELSE is, because a '-' or a '|' landing on a corner is
        // an off-by-one in a hand-drawn map and that is exactly the mistake worth catching
        public const char CornerGlyph = '+';

        // THE LEGEND, AND THE ONLY STATEMENT OF IT. The map file describes it in a comment for
        // whoever is reading the file, and a glyph in the wrong place prints this - so the copy in
        // the file is a courtesy that cannot silently become wrong, because the error message is
        // derived from here and the file's comment is not what anything parses
        static readonly (char Glyph, Tile Tile, string Name)[] Squares =
        {
            ('.', Tile.Floor, "floor"),
            ('~', Tile.Rough, "difficult ground"),
            ('#', Tile.Void, "rock - not part of the map"),
        };

        // an edge glyph says which way its line runs, which is how a '|' typed on a horizontal line
        // gets caught. 'x' is a door either way round: the position already said which way it faces
        static readonly (char Glyph, Edge Edge, bool Vertical, bool Horizontal, string Name)[] Lines =
        {
            (' ', Edge.None, true, true, "open"),
            ('|', Edge.Wall, true, false, "wall, up a line between two columns"),
            ('-', Edge.Wall, false, true, "wall, along a line between two rows"),
            ('x', Edge.Door, true, true, "door, shut"),
        };

        // derived, never listed - the same rule EngineKeys and GameKeys run on
        public static string Legend
        {
            get
            {
                var legend = new StringBuilder("squares: ");

                foreach ((char glyph, Tile _, string name) in Squares)
                    legend.Append(glyph).Append(' ').Append(name).Append(", ");

                legend.Append(StartGlyph).Append(" where the hero starts. lines: ");

                foreach ((char glyph, Edge _, bool _, bool _, string name) in Lines)
                    legend.Append(glyph == ' ' ? "space" : glyph.ToString()).Append(' ')
                          .Append(name).Append(", ");

                return legend.Append(CornerGlyph).Append(" a corner, which means nothing").ToString();
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

            int width = rows[0].Text.Length;

            if (rows.Count % 2 == 0 || rows.Count < 3)
            {
                problem = $"{rows.Count} lines of map - a map is lines of squares with a line of " +
                          "walls above, below and between them, so the count is always odd and at " +
                          "least 3";
                return false;
            }

            if (width % 2 == 0 || width < 3)
            {
                problem = $"line {rows[0].Line} is {width} characters wide - a row is squares with a " +
                          "wall position before, after and between them, so the width is always odd " +
                          "and at least 3";
                return false;
            }

            foreach ((int line, string row) in rows)
                if (row.Length > width)
                {
                    problem = $"line {line} is {row.Length} characters but line {rows[0].Line} is " +
                              $"{width} - every line of a map is the same width";
                    return false;
                }

            int columns = (width - 1) / 2;
            int mapRows = (rows.Count - 1) / 2;

            var tiles = new Tile[columns * mapRows];
            var vertical = new Edge[(columns + 1) * mapRows];
            var horizontal = new Edge[columns * (mapRows + 1)];
            Cell? start = null;

            for (int i = 0; i < rows.Count; i++)
            {
                (int line, string row) = rows[i];

                for (int j = 0; j < width; j++)
                {
                    // SHORT LINES ARE PADDED WITH OPEN, NOT REFUSED. a space is a real glyph here -
                    // "no wall" - so a map whose east edge is open ends in trailing spaces, and
                    // every editor in the world eats those. refusing would make the format depend
                    // on a setting; padding makes the file mean the same thing either way
                    char glyph = j < row.Length ? row[j] : ' ';

                    bool oddLine = i % 2 == 1;
                    bool oddColumn = j % 2 == 1;

                    if (oddLine && oddColumn)
                    {
                        if (!Square(glyph, line, j, new Cell((j - 1) / 2, (i - 1) / 2),
                                    tiles, columns, ref start, ref problem))
                            return false;
                    }
                    else if (oddLine)
                    {
                        // a line up the west side of the square to its right
                        var border = new Border(new Cell(j / 2, (i - 1) / 2), true);

                        if (!Line(glyph, line, j, border, vertical, columns, ref problem)) return false;
                    }
                    else if (oddColumn)
                    {
                        // a line along the top of the square below it
                        var border = new Border(new Cell((j - 1) / 2, i / 2), false);

                        if (!Line(glyph, line, j, border, horizontal, columns, ref problem)) return false;
                    }
                    else if (glyph != CornerGlyph && glyph != ' ')
                    {
                        problem = $"line {line} column {j + 1} is '{glyph}' where four squares meet. " +
                                  $"only '{CornerGlyph}' or a space belongs on a corner - a wall " +
                                  $"one place out is what this catches. the legend is: {Legend}";
                        return false;
                    }
                }
            }

            if (start == null)
            {
                problem = $"no '{StartGlyph}' anywhere in it - a map has to say where the hero starts";
                return false;
            }

            map = new MapLayout(columns, mapRows, tiles, start.Value, vertical, horizontal);
            return true;
        }

        static bool Square(char glyph, int line, int column, Cell cell, Tile[] tiles, int columns,
                           ref Cell? start, ref string problem)
        {
            if (glyph == StartGlyph)
            {
                if (start != null)
                {
                    problem = $"line {line} column {column + 1} is a second '{StartGlyph}' - the hero " +
                              $"already starts on {start} and cannot start twice";
                    return false;
                }

                start = cell;
                tiles[cell.Y * columns + cell.X] = Tile.Floor;
                return true;
            }

            foreach ((char g, Tile tile, string _) in Squares)
                if (g == glyph)
                {
                    tiles[cell.Y * columns + cell.X] = tile;
                    return true;
                }

            problem = $"line {line} column {column + 1} is '{glyph}' where a square goes, and that is " +
                      $"not a square. the legend is: {Legend}";
            return false;
        }

        static bool Line(char glyph, int line, int column, Border border, Edge[] into, int columns,
                         ref string problem)
        {
            foreach ((char g, Edge edge, bool vertical, bool horizontal, string _) in Lines)
            {
                if (g != glyph) continue;

                if (border.Vertical ? !vertical : !horizontal)
                {
                    problem = $"line {line} column {column + 1} is '{glyph}', which is a wall running " +
                              $"the other way - this position is a line " +
                              (border.Vertical ? "up between two columns" : "along between two rows") +
                              $". the legend is: {Legend}";
                    return false;
                }

                into[border.Vertical
                    ? border.Cell.Y * (columns + 1) + border.Cell.X
                    : border.Cell.Y * columns + border.Cell.X] = edge;

                return true;
            }

            problem = $"line {line} column {column + 1} is '{glyph}' where a wall position goes, and " +
                      $"that is not one. the legend is: {Legend}";
            return false;
        }

        // the lines that are map, carrying the line number they came from
        //
        // TRAILING WHITESPACE IS KEPT, unlike the cell format this replaced: a space is now a real
        // glyph meaning "no wall", so a trailing one at the east edge is data. What protects the
        // format from an editor that strips it is the padding above, not trimming here.
        //
        // LEADING whitespace is kept too, and is an error: it is inside the grid, where the first
        // column is a wall position, so an indented row gets named rather than quietly shifting.
        static List<(int Line, string Text)> Rows(string text)
        {
            var rows = new List<(int, string)>();
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                // a line of nothing but whitespace is a blank line, however many spaces it holds -
                // a real map line always has a corner or a wall in it
                if (line.Trim().Length == 0) continue;

                if (line.TrimStart().StartsWith(Comment.ToString())) continue;

                // kept exactly as written, trailing spaces and all
                rows.Add((i + 1, line));
            }

            return rows;
        }
    }
}
