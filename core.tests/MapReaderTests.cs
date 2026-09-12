using Core.Space;
using Xunit;

namespace Core.Tests
{
    // The reader is the engine/content boundary on the board, so what is guarded here is both
    // halves of it: a good map becomes exactly the map that was drawn, and a bad one is REFUSED
    // AND NAMED rather than half-loaded.
    //
    // The second half is the one worth the trouble. A map that loads with a row missing is a room
    // with a wall gone and nothing to tell you - the failure mode this whole class exists to make
    // impossible, and the same one `check-locale.ps1` exists for on the other side of the project.
    public class MapReaderTests
    {
        static MapLayout Read(params string[] lines)
        {
            Assert.True(MapReader.TryRead(string.Join("\n", lines), out MapLayout map, out string problem),
                        problem);

            return map;
        }

        static string Refuse(params string[] lines)
        {
            Assert.False(MapReader.TryRead(string.Join("\n", lines), out MapLayout map, out string problem));
            Assert.Null(map);
            Assert.False(string.IsNullOrWhiteSpace(problem));

            return problem;
        }

        // ---- the picture in the file is the picture on the table ----

        [Fact]
        public void TheExtentComesFromTheFile()
        {
            MapLayout map = Read(
                "#####",
                "#.@.#",
                "#####");

            Assert.Equal(5, map.Columns);
            Assert.Equal(3, map.Rows);
            Assert.Equal(15, map.Count);
        }

        [Fact]
        public void EveryGlyphBecomesItsTile()
        {
            MapLayout map = Read(
                "#.+~",
                "@...");

            Assert.Equal(Tile.Wall, map.At(new Cell(0, 0)));
            Assert.Equal(Tile.Floor, map.At(new Cell(1, 0)));
            Assert.Equal(Tile.Door, map.At(new Cell(2, 0)));
            Assert.Equal(Tile.Rough, map.At(new Cell(3, 0)));
        }

        // the first line of the file is the far side of the table, which is what makes a map
        // readable at a glance: you are looking at it from your seat
        [Fact]
        public void TheFirstLineIsTheFarSide()
        {
            MapLayout map = Read(
                "###",
                "#.#",
                "#@#");

            Assert.Equal(new Cell(1, 2), map.Start);
            Assert.Equal(Tile.Floor, map.At(new Cell(1, 1)));
        }

        [Fact]
        public void TheHeroStartsOnTheSquareThatIsMarked()
        {
            MapLayout map = Read(
                "#####",
                "#..@#",
                "#####");

            Assert.Equal(new Cell(3, 1), map.Start);

            // and the square under the mark is floor, so a start can never be inside a wall
            Assert.Equal(Tile.Floor, map.At(map.Start));
            Assert.True(map.IsPassable(map.Start));
        }

        // ---- what is not map ----

        [Fact]
        public void CommentsAndBlankLinesAreNotSquares()
        {
            MapLayout map = Read(
                "; the cellar",
                ";",
                "",
                "###",
                "#@#",
                "   ; indented notes are notes too",
                "###",
                "");

            Assert.Equal(3, map.Columns);
            Assert.Equal(3, map.Rows);
        }

        // ';' rather than '#', because '#' is a wall - a comment character that is also a glyph
        // eats the first row of every map whose left column is wall
        [Fact]
        public void AWallInColumnZeroIsAWall()
        {
            MapLayout map = Read(
                "####",
                "#@.#",
                "####");

            Assert.Equal(Tile.Wall, map.At(new Cell(0, 0)));
            Assert.Equal(3, map.Rows);
        }

        [Fact]
        public void TrailingWhitespaceIsNotASquare()
        {
            MapLayout map = Read(
                "###   ",
                "#@#\t",
                "###");

            Assert.Equal(3, map.Columns);
        }

        [Fact]
        public void ItReadsAFileSavedOnWindows()
        {
            Assert.True(MapReader.TryRead("###\r\n#@#\r\n###\r\n", out MapLayout map, out string problem), problem);

            Assert.Equal(3, map.Columns);
            Assert.Equal(3, map.Rows);
        }

        // ---- refusals, each naming the line the author is looking at ----

        [Fact]
        public void ARaggedMapIsRefused_AndSaysWhichLine()
        {
            string problem = Refuse(
                "; a map",
                "#####",
                "#@..#",
                "#..#",
                "#####");

            Assert.Contains("line 4", problem);
            Assert.Contains("line 2", problem);
        }

        [Fact]
        public void AGlyphThatIsNotATileIsRefused_AndSaysWhereAndWhatIsAllowed()
        {
            string problem = Refuse(
                "#####",
                "#@?.#",
                "#####");

            Assert.Contains("line 2", problem);
            Assert.Contains("column 3", problem);
            Assert.Contains("'?'", problem);

            // and it prints the legend, so the answer is in the failure
            Assert.Contains("wall", problem);
            Assert.Contains("difficult ground", problem);
        }

        // a space is not a tile, so an indented row is an error rather than a map that quietly
        // slides one column sideways
        [Fact]
        public void AnIndentedRowIsRefused()
        {
            string problem = Refuse(
                "  ###",
                "  #@#",
                "  ###");

            Assert.Contains("' '", problem);
        }

        [Fact]
        public void AMapWithNoHeroIsRefused()
        {
            string problem = Refuse(
                "###",
                "#.#",
                "###");

            Assert.Contains("@", problem);
        }

        [Fact]
        public void TwoHeroesAreRefused()
        {
            string problem = Refuse(
                "####",
                "#@@#",
                "####");

            Assert.Contains("twice", problem);
        }

        [Fact]
        public void NothingAtAllIsRefused()
        {
            Assert.Contains("no map", Refuse("; all comment", "", "   "));
            Assert.Contains("no map", Refuse(""));

            Assert.False(MapReader.TryRead(null, out _, out string problem));
            Assert.Contains("no map", problem);
        }

        // ---- the legend is derived, not written twice ----

        [Fact]
        public void TheLegendNamesEveryGlyphThatParses()
        {
            string legend = MapReader.Legend;

            foreach (char glyph in new[] { '.', '#', '+', '~', MapReader.StartGlyph })
                Assert.Contains(glyph.ToString(), legend);
        }
    }
}
