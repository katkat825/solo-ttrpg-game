using Core.Space;
using Xunit;

namespace Core.Tests
{
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

        static MapLayout Example() => Read(
            "+-+-+-+",
            "|@ . .|",
            "+ +x+ +",
            "|.|#|~|",
            "+-+-+-+");


        [Fact]
        public void TheExtentIsHalfTheCharacters()
        {
            MapLayout map = Example();

            Assert.Equal(3, map.Columns);
            Assert.Equal(2, map.Rows);
            Assert.Equal(6, map.Count);
        }

        [Fact]
        public void EverySquareGlyphBecomesItsTile()
        {
            MapLayout map = Example();

            Assert.Equal(Tile.Floor, map.At(new Cell(0, 0)));
            Assert.Equal(Tile.Floor, map.At(new Cell(0, 1)));
            Assert.Equal(Tile.Void, map.At(new Cell(1, 1)));
            Assert.Equal(Tile.Rough, map.At(new Cell(2, 1)));
        }

        [Fact]
        public void EveryLineGlyphBecomesItsEdge()
        {
            MapLayout map = Example();

            Assert.Equal(Edge.Wall, map.At(Border.North(new Cell(0, 0))));
            Assert.Equal(Edge.Wall, map.At(Border.West(new Cell(0, 0))));
            Assert.Equal(Edge.Wall, map.At(Border.East(new Cell(2, 0))));
            Assert.Equal(Edge.Wall, map.At(Border.South(new Cell(1, 1))));

            Assert.Equal(Edge.None, map.At(Border.East(new Cell(0, 0))));
            Assert.Equal(Edge.None, map.At(Border.East(new Cell(1, 0))));

            Assert.Equal(Edge.Wall, map.At(Border.East(new Cell(0, 1))));
            Assert.Equal(Edge.Wall, map.At(Border.East(new Cell(1, 1))));
        }

        [Fact]
        public void ADoorSitsOnTheLineItWasDrawnOn()
        {
            MapLayout map = Example();

            Assert.Equal(Edge.Door, map.At(Border.South(new Cell(1, 0))));
            Assert.Equal(Edge.Door, map.At(Border.North(new Cell(1, 1))));
        }

        [Fact]
        public void ADoorRunsTheOtherWayWhenItIsDrawnOnAVerticalLine()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@x.|",
                "+-+-+");

            Assert.Equal(Edge.Door, map.At(Border.East(new Cell(0, 0))));
        }

        // the file's first line is the far side of the table, read from your seat
        [Fact]
        public void TheFirstLineIsTheFarSide()
        {
            MapLayout map = Read(
                "+-+",
                "|.|",
                "+ +",
                "|@|",
                "+-+");

            Assert.Equal(new Cell(0, 1), map.Start);
            Assert.Equal(Tile.Floor, map.At(new Cell(0, 0)));
        }

        [Fact]
        public void TheHeroStartsOnTheSquareThatIsMarked()
        {
            MapLayout map = Example();

            Assert.Equal(new Cell(0, 0), map.Start);

            Assert.Equal(Tile.Floor, map.At(map.Start));
            Assert.True(map.IsPassable(map.Start));
        }


        [Fact]
        public void CommentsAndBlankLinesAreNotPartOfTheGrid()
        {
            MapLayout map = Read(
                "; the cellar",
                ";",
                "",
                "+-+-+",
                "|@ .|",
                "+-+-+",
                "   ; indented notes are notes too",
                "");

            Assert.Equal(2, map.Columns);
            Assert.Equal(1, map.Rows);
        }

        // comment char is ';' not '#': '#' is rock and would eat maps with rock in the left column
        [Fact]
        public void RockInColumnZeroIsRock()
        {
            MapLayout map = Read(
                "+-+-+",
                "|#|@|",
                "+-+-+");

            Assert.Equal(Tile.Void, map.At(new Cell(0, 0)));
            Assert.Equal(1, map.Rows);
        }

        // a trailing space is a glyph ("no wall"); the format must not depend on editors keeping it
        [Fact]
        public void ALineCutShortByAnEditorIsPaddedWithOpenLines()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@ .",
                "+-+-+");

            Assert.Equal(2, map.Columns);
            Assert.Equal(Edge.None, map.At(Border.East(new Cell(1, 0))));
        }

        [Fact]
        public void ItReadsAFileSavedOnWindows()
        {
            Assert.True(MapReader.TryRead("+-+-+\r\n|@ .|\r\n+-+-+\r\n", out MapLayout map, out string problem),
                        problem);

            Assert.Equal(2, map.Columns);
            Assert.Equal(1, map.Rows);
        }


        [Fact]
        public void AnEvenNumberOfLinesIsRefused()
        {
            string problem = Refuse(
                "+-+-+",
                "|@ .|",
                "+-+-+",
                "|. .|");

            Assert.Contains("odd", problem);
        }

        [Fact]
        public void AnEvenWidthIsRefused()
        {
            string problem = Refuse(
                "+-+-",
                "|@ .",
                "+-+-");

            Assert.Contains("odd", problem);
            Assert.Contains("line 1", problem);
        }

        [Fact]
        public void ALineWithOneCharacterTooManyIsRefused_AndSaysWhichLine()
        {
            string problem = Refuse(
                "+-+-+",
                "|@ .||",
                "+-+-+");

            Assert.Contains("line 2", problem);
            Assert.Contains("line 1", problem);
        }

        [Fact]
        public void ASquareGlyphOnALineIsRefused()
        {
            string problem = Refuse(
                "+-+-+",
                "|@..|",
                "+-+-+");

            Assert.Contains("line 2", problem);
            Assert.Contains("column 3", problem);
            Assert.Contains("wall position", problem);
        }

        [Fact]
        public void AWallGlyphOnASquareIsRefused()
        {
            string problem = Refuse(
                "+-+-+",
                "|@ ||",
                "+-+-+");

            Assert.Contains("line 2", problem);
            Assert.Contains("column 4", problem);
            Assert.Contains("square", problem);
        }

        [Fact]
        public void AWallRunningTheWrongWayIsRefused()
        {
            string problem = Refuse(
                "+-+-+",
                "|@-.|",
                "+-+-+");

            Assert.Contains("the other way", problem);
            Assert.Contains("column 3", problem);
        }

        [Fact]
        public void AWallOnACornerIsRefused()
        {
            string problem = Refuse(
                "+-+-+",
                "|@ .|",
                "+-|-+");

            Assert.Contains("corner", problem);
            Assert.Contains("line 3", problem);
        }

        [Fact]
        public void ASpaceOnACornerIsFine()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@ .|",
                " -   ",
                "|. .|",
                "+-+-+");

            Assert.Equal(Edge.Wall, map.At(Border.South(new Cell(0, 0))));
        }

        [Fact]
        public void AnIndentedRowIsRefused()
        {
            string problem = Refuse(
                "  +-+-+",
                "  |@ .|",
                "  +-+-+");

            Assert.Contains("column", problem);
        }

        [Fact]
        public void AMapWithNoHeroIsRefused()
        {
            string problem = Refuse(
                "+-+-+",
                "|. .|",
                "+-+-+");

            Assert.Contains("@", problem);
        }

        [Fact]
        public void TwoHeroesAreRefused()
        {
            string problem = Refuse(
                "+-+-+",
                "|@ @|",
                "+-+-+");

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

        [Fact]
        public void AMapTooSmallToHaveASquareInItIsRefused()
        {
            Assert.Contains("at least 3", Refuse("+", "|"));
        }


        [Fact]
        public void TheLegendNamesEveryGlyphThatParses()
        {
            string legend = MapReader.Legend;

            foreach (char glyph in new[] { '.', '~', '#', '|', '-', 'x', MapReader.StartGlyph, MapReader.CornerGlyph })
                Assert.Contains(glyph.ToString(), legend);

            Assert.Contains("squares", legend);
            Assert.Contains("lines", legend);
        }


        [Fact]
        public void NumberedSquaresAreSpawnSlots()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ 1 2|",
                "+-+-+-+");

            Assert.Equal(new Cell(0, 0), map.Start);
            Assert.Equal(2, map.Spawns.Count);
            Assert.Equal(new Cell(1, 0), map.SpawnAt(1));
            Assert.Equal(new Cell(2, 0), map.SpawnAt(2));
        }

        [Fact]
        public void TheHeroIsSlotZero()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@ 1|",
                "+-+-+");

            Assert.Equal(map.Start, map.SpawnAt(MapLayout.HeroSlot));
            Assert.DoesNotContain(MapLayout.HeroSlot, map.Spawns.Keys);
        }

        [Fact]
        public void ASpawnStandsOnFloor()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@ 3|",
                "+-+-+");

            Assert.Equal(Tile.Floor, map.At(new Cell(1, 0)));
            Assert.True(map.IsPassable(new Cell(1, 0)));
        }

        [Fact]
        public void ASlotUsedTwiceIsRefused()
        {
            string problem = Refuse(
                "+-+-+-+",
                "|@ 1 1|",
                "+-+-+-+");

            Assert.Contains("spawn 1", problem);
        }

        [Fact]
        public void ASlotTheMapDoesNotHaveIsNothing()
        {
            MapLayout map = Read(
                "+-+-+",
                "|@ 1|",
                "+-+-+");

            Assert.Null(map.SpawnAt(7));
        }

        [Fact]
        public void SpawnsSurviveTheMapBeingChanged()
        {
            MapLayout map = Read(
                "+-+-+-+",
                "|@ 1 .|",
                "+-+-+-+");

            MapLayout after = map.With(new Cell(2, 0), Tile.Rough)
                                 .With(Border.East(new Cell(0, 0)), Edge.Wall);

            Assert.Equal(new Cell(1, 0), after.SpawnAt(1));
        }

        [Fact]
        public void TheLegendMentionsSpawns()
        {
            Assert.Contains(MapReader.FirstSpawnGlyph.ToString(), MapReader.Legend);
            Assert.Contains("spawn", MapReader.Legend);
        }

    }
}
