using Core.Space;
using Xunit;

namespace Core.Tests
{
    // The reader is the engine/content boundary on the board, so what is guarded here is both
    // halves of it: a good map becomes exactly the map that was drawn, and a bad one is REFUSED
    // AND NAMED rather than half-loaded.
    //
    // The second half is the one worth the trouble. A map that loads with a wall missing is a room
    // with a hole in it and nothing to tell you - the failure mode this whole class exists to make
    // impossible, and the same one `check-locale.ps1` exists for on the other side of the project.
    //
    // SINCE EDGE_WALLS.md THE FORMAT IS DOUBLE RESOLUTION: 2H+1 lines of 2W+1 characters, squares
    // on the odd/odd positions and the lines between them on the rest. That is most of what these
    // cases are about - a glyph in the wrong kind of position is now a real and likely mistake, and
    // every one of them gets named.
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

        // the example out of EDGE_WALLS.md, with a hero put on it: three floor squares open to each
        // other, a shut door under the middle one, and below it floor, rock and difficult ground
        // with walls between them
        static MapLayout Example() => Read(
            "+-+-+-+",
            "|@ . .|",
            "+ +x+ +",
            "|.|#|~|",
            "+-+-+-+");

        // ---- the picture in the file is the picture on the table ----

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

            // the outside of the map, all the way round
            Assert.Equal(Edge.Wall, map.At(Border.North(new Cell(0, 0))));
            Assert.Equal(Edge.Wall, map.At(Border.West(new Cell(0, 0))));
            Assert.Equal(Edge.Wall, map.At(Border.East(new Cell(2, 0))));
            Assert.Equal(Edge.Wall, map.At(Border.South(new Cell(1, 1))));

            // the top row is open to itself
            Assert.Equal(Edge.None, map.At(Border.East(new Cell(0, 0))));
            Assert.Equal(Edge.None, map.At(Border.East(new Cell(1, 0))));

            // and the bottom row is not
            Assert.Equal(Edge.Wall, map.At(Border.East(new Cell(0, 1))));
            Assert.Equal(Edge.Wall, map.At(Border.East(new Cell(1, 1))));
        }

        // an 'x' is a door whichever way its line runs, because the POSITION already said which way
        // it faces - which is the whole reason the format is drawn this way
        [Fact]
        public void ADoorSitsOnTheLineItWasDrawnOn()
        {
            MapLayout map = Example();

            Assert.Equal(Edge.Door, map.At(Border.South(new Cell(1, 0))));
            Assert.Equal(Edge.Door, map.At(Border.North(new Cell(1, 1))));   // the same line
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

        // the first line of the file is the far side of the table, which is what makes a map
        // readable at a glance: you are looking at it from your seat
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

            // and the square under the mark is floor, so a start can never be inside a wall
            Assert.Equal(Tile.Floor, map.At(map.Start));
            Assert.True(map.IsPassable(map.Start));
        }

        // ---- what is not map ----

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

        // ';' rather than '#', because '#' is rock - a comment character that is also a glyph eats
        // the first row of every map with rock in its left column
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

        // A SPACE IS A GLYPH NOW - it means "no wall" - so a line whose east end is open ends in
        // one, and every editor in the world eats those. the format cannot depend on a setting
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

        // ---- refusals, each naming the line the author is looking at ----

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

        // the mistake the double-resolution format makes likely, and the one it is worth catching:
        // a glyph one position out from where it belongs
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

        // a '-' is a wall along a line between two ROWS; typed on a vertical line it is an
        // off-by-one, and the reader knows which kind of line it is looking at
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

        // a corner may be a '+' or a space, because a map with no junction there is still a map
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

        // ---- the legend is derived, not written twice ----

        [Fact]
        public void TheLegendNamesEveryGlyphThatParses()
        {
            string legend = MapReader.Legend;

            foreach (char glyph in new[] { '.', '~', '#', '|', '-', 'x', MapReader.StartGlyph, MapReader.CornerGlyph })
                Assert.Contains(glyph.ToString(), legend);

            // and says which of the two kinds of position each belongs to
            Assert.Contains("squares", legend);
            Assert.Contains("lines", legend);
        }

        // ---- numbered spawns: the map says where, the encounter says who (P2) ----

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

        // the hero is slot 0 and keeps its own glyph, because every map must have one
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

        // a spawn stands on floor, for the same reason the hero's start does: a spawn you can see
        // in the picture cannot drift into a wall
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

        // a map is edited as it is played - a door forced, wreckage dropped - and the spawns have
        // to survive that, because an encounter may still be placing things after the first blow
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

        // and the legend says so, derived rather than written out
        [Fact]
        public void TheLegendMentionsSpawns()
        {
            Assert.Contains(MapReader.FirstSpawnGlyph.ToString(), MapReader.Legend);
            Assert.Contains("spawn", MapReader.Legend);
        }

    }
}
