using System.Collections.Generic;
using System.Text;

namespace Core.Space
{
    // double resolution: odd line + odd column is a square, an even coordinate is the line beside it, both even a corner
    public static class MapReader
    {
        // ';' not '#' - '#' is rock, and a comment char that's also a glyph is a trap
        public const char Comment = ';';

        public const char StartGlyph = '@';

        public const char FirstSpawnGlyph = '1';

        public const char LastSpawnGlyph = '9';

        public static bool IsSpawn(char glyph) => glyph >= FirstSpawnGlyph && glyph <= LastSpawnGlyph;

        // a corner means nothing - but a '-' or '|' landing here is an off-by-one worth catching
        public const char CornerGlyph = '+';

        static readonly (char Glyph, Tile Tile, string Name)[] Squares =
        {
            ('.', Tile.Floor, "floor"),
            ('~', Tile.Rough, "difficult ground"),
            ('#', Tile.Void, "rock - not part of the map"),
        };

        // an edge glyph says which way its line runs, so a '|' on a horizontal line gets caught
        static readonly (char Glyph, Edge Edge, bool Vertical, bool Horizontal, string Name)[] Lines =
        {
            (' ', Edge.None, true, true, "open"),
            ('|', Edge.Wall, true, false, "wall, up a line between two columns"),
            ('-', Edge.Wall, false, true, "wall, along a line between two rows"),
            ('x', Edge.Door, true, true, "door, shut"),
        };

        public static string Legend
        {
            get
            {
                var legend = new StringBuilder("squares: ");

                foreach ((char glyph, Tile _, string name) in Squares)
                    legend.Append(glyph).Append(' ').Append(name).Append(", ");

                legend.Append(StartGlyph).Append(" where the hero starts, ")
                      .Append(FirstSpawnGlyph).Append('-').Append(LastSpawnGlyph)
                      .Append(" a numbered spawn. lines: ");

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
            var spawns = new Dictionary<int, Cell>();
            Cell? start = null;

            for (int i = 0; i < rows.Count; i++)
            {
                (int line, string row) = rows[i];

                for (int j = 0; j < width; j++)
                {
                    // pad short lines with open - editors eat trailing spaces, and a space here means no wall
                    char glyph = j < row.Length ? row[j] : ' ';

                    bool oddLine = i % 2 == 1;
                    bool oddColumn = j % 2 == 1;

                    if (oddLine && oddColumn)
                    {
                        if (!Square(glyph, line, j, new Cell((j - 1) / 2, (i - 1) / 2),
                                    tiles, columns, spawns, ref start, ref problem))
                            return false;
                    }
                    else if (oddLine)
                    {
                        var border = new Border(new Cell(j / 2, (i - 1) / 2), true);

                        if (!Line(glyph, line, j, border, vertical, columns, ref problem)) return false;
                    }
                    else if (oddColumn)
                    {
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

            map = new MapLayout(columns, mapRows, tiles, start.Value, vertical, horizontal, spawns);
            return true;
        }

        static bool Square(char glyph, int line, int column, Cell cell, Tile[] tiles, int columns,
                           Dictionary<int, Cell> spawns, ref Cell? start, ref string problem)
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

            if (IsSpawn(glyph))
            {
                int slot = glyph - '0';

                if (spawns.TryGetValue(slot, out Cell already))
                {
                    problem = $"line {line} column {column + 1} is a second '{glyph}' - spawn {slot} " +
                              $"is already {already} and a slot holds one thing";
                    return false;
                }

                spawns[slot] = cell;
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

        static List<(int Line, string Text)> Rows(string text)
        {
            var rows = new List<(int, string)>();
            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.Trim().Length == 0) continue;

                if (line.TrimStart().StartsWith(Comment.ToString())) continue;

                rows.Add((i + 1, line));
            }

            return rows;
        }
    }
}
