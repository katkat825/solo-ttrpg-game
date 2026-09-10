using Godot;
using Core.Space;
using Game.Board;

namespace Game.Tests
{
    // BoardMetrics is the one place that answers "where is that square", and it is the board's
    // half of what TrayBounds does for the tray. Two kinds of thing are guarded here:
    //
    //   a square's centre and the square under a point must be the SAME mapping, both ways round
    //   both must hold for ANY board, not just the 8 x 8 of 60 mm the game ships with
    //
    // The second is the point. A mapping that happens to be right for one extent and wrong for
    // every other reads as correct right up until B2 loads a map of a different size - and B2 is
    // the milestone after next.
    //
    // Godot-free in the sense game.tests means: Vector3 is a managed struct, and nothing here
    // touches a Node or a Resource.
    public class BoardMetricsTests
    {
        static BoardMetrics Board(int columns = 8, int rows = 8, float cell = 0.06f) =>
            new BoardMetrics(columns, rows, cell);

        // ---- the board the game ships with ----

        [Fact]
        public void Shipped_IsTheBoardTheExportsDefaultTo()
        {
            BoardMetrics b = BoardMetrics.Shipped;

            Assert.Equal(8, b.Columns);
            Assert.Equal(8, b.Rows);
            Assert.Equal(0.06f, b.CellSize, 4);

            // 480 mm square, which is the tray's 640 x 490 read as a battle map beside it
            Assert.Equal(0.48f, b.Width, 4);
            Assert.Equal(0.48f, b.Depth, 4);
        }

        [Fact]
        public void ABoardIsAsBigAsItsSquaresMakeIt()
        {
            BoardMetrics b = Board(10, 4, 0.05f);

            Assert.Equal(0.50f, b.Width, 4);
            Assert.Equal(0.20f, b.Depth, 4);
            Assert.Equal(0.25f, b.HalfWidth, 4);
            Assert.Equal(0.10f, b.HalfDepth, 4);
        }

        // ---- centred on its own origin ----

        // the property that lets the board be stood anywhere on the table: the node's position IS
        // the middle of the map, so nothing downstream has to know where a corner is
        [Fact]
        public void TheBoardIsCentredOnItsOwnOrigin()
        {
            BoardMetrics b = Board(8, 8);

            Vector3 first = b.Centre(new Cell(0, 0));
            Vector3 last = b.Centre(new Cell(7, 7));

            Assert.Equal(0f, (first.X + last.X), 5);
            Assert.Equal(0f, (first.Z + last.Z), 5);
        }

        [Fact]
        public void ASquaresCentreIsInTheMiddleOfIt()
        {
            BoardMetrics b = Board(8, 8);

            // half a square in from the corner of a 0.48 board
            Assert.Equal(-0.21f, b.Centre(new Cell(0, 0)).X, 4);
            Assert.Equal(-0.21f, b.Centre(new Cell(0, 0)).Z, 4);
            Assert.Equal(0.21f, b.Centre(new Cell(7, 7)).X, 4);
            Assert.Equal(0.21f, b.Centre(new Cell(7, 7)).Z, 4);
        }

        // a piece standing on a square has its feet on the felt, so the mat's thickness never
        // reaches anything that places one
        [Fact]
        public void EverySquareIsOnTheSurface()
        {
            BoardMetrics b = Board(8, 8);

            foreach (Cell cell in new Grid<object>(b.Columns, b.Rows).Cells)
                Assert.Equal(0f, b.Centre(cell).Y, 6);
        }

        [Fact]
        public void NeighbouringSquares_AreOneCellApart()
        {
            BoardMetrics b = Board(8, 8, 0.06f);

            Vector3 here = b.Centre(new Cell(3, 4));

            Assert.Equal(0.06f, b.Centre(new Cell(4, 4)).X - here.X, 5);
            Assert.Equal(0.06f, b.Centre(new Cell(3, 5)).Z - here.Z, 5);
        }

        // the axes must not be interchangeable, and on a square board a swap is invisible - so
        // ask an oblong one
        [Fact]
        public void XRunsAcrossAndYRunsAway()
        {
            BoardMetrics b = Board(10, 4, 0.05f);

            Vector3 alongX = b.Centre(new Cell(9, 0)) - b.Centre(new Cell(0, 0));
            Vector3 alongY = b.Centre(new Cell(0, 3)) - b.Centre(new Cell(0, 0));

            Assert.Equal(0.45f, alongX.X, 4);
            Assert.Equal(0f, alongX.Z, 5);

            Assert.Equal(0f, alongY.X, 5);
            Assert.Equal(0.15f, alongY.Z, 4);
        }

        // ---- and back again ----

        [Fact]
        public void EverySquaresCentre_IsInThatSquare()
        {
            foreach (BoardMetrics b in new[] { Board(8, 8), Board(10, 4, 0.05f), Board(3, 17, 0.11f) })
                foreach (Cell cell in new Grid<object>(b.Columns, b.Rows).Cells)
                    Assert.Equal(cell, b.At(b.Centre(cell)));
        }

        // a click lands anywhere in a square, not on its middle, so the corners are what matter
        [Fact]
        public void AnywhereInASquare_IsThatSquare()
        {
            BoardMetrics b = Board(8, 8);

            Vector3 centre = b.Centre(new Cell(2, 5));
            float almost = b.CellSize * 0.499f;

            Assert.Equal(new Cell(2, 5), b.At(centre + new Vector3(almost, 0f, almost)));
            Assert.Equal(new Cell(2, 5), b.At(centre + new Vector3(-almost, 0f, -almost)));
            Assert.Equal(new Cell(2, 5), b.At(centre + new Vector3(almost, 0f, -almost)));
        }

        [Fact]
        public void JustOverTheLine_IsTheNextSquare()
        {
            BoardMetrics b = Board(8, 8);

            Vector3 centre = b.Centre(new Cell(2, 5));
            float over = b.CellSize * 0.501f;

            Assert.Equal(new Cell(3, 5), b.At(centre + new Vector3(over, 0f, 0f)));
            Assert.Equal(new Cell(1, 5), b.At(centre + new Vector3(-over, 0f, 0f)));
            Assert.Equal(new Cell(2, 6), b.At(centre + new Vector3(0f, 0f, over)));
            Assert.Equal(new Cell(2, 4), b.At(centre + new Vector3(0f, 0f, -over)));
        }

        // height is choreography - a piece lifted off the felt is still over the square it left
        [Fact]
        public void HeightIsIgnored()
        {
            BoardMetrics b = Board(8, 8);

            Vector3 centre = b.Centre(new Cell(6, 1));

            Assert.Equal(new Cell(6, 1), b.At(centre + new Vector3(0f, 0.5f, 0f)));
            Assert.Equal(new Cell(6, 1), b.At(centre + new Vector3(0f, -0.5f, 0f)));
        }

        // OFF THE BOARD MUST COME BACK OFF THE BOARD. clamping to the nearest square would make
        // the far edge of the map a magnet the width of the table; Grid.Contains is what refuses
        // these, and it can only do that if they arrive as squares that do not exist
        [Fact]
        public void PastTheEdge_IsNotClampedToTheEdge()
        {
            BoardMetrics b = Board(8, 8);
            var grid = new Grid<object>(b.Columns, b.Rows);

            Cell past = b.At(new Vector3(b.HalfWidth + 0.001f, 0f, 0f));
            Cell before = b.At(new Vector3(-b.HalfWidth - 0.001f, 0f, 0f));
            Cell miles = b.At(new Vector3(3f, 0f, -2f));

            Assert.Equal(8, past.X);
            Assert.Equal(-1, before.X);

            Assert.False(grid.Contains(past));
            Assert.False(grid.Contains(before));
            Assert.False(grid.Contains(miles));
        }

        [Fact]
        public void TheOutsideCornersAreTheLastSquares()
        {
            BoardMetrics b = Board(8, 8);
            float inside = 0.0001f;

            Assert.Equal(new Cell(0, 0), b.At(new Vector3(-b.HalfWidth + inside, 0f, -b.HalfDepth + inside)));
            Assert.Equal(new Cell(7, 7), b.At(new Vector3(b.HalfWidth - inside, 0f, b.HalfDepth - inside)));
        }

        // ---- boards that make no sense ----

        [Fact]
        public void ABoardAPieceCouldStandOn_IsUsable()
        {
            Assert.True(BoardMetrics.Shipped.IsUsable);
            Assert.True(Board(1, 1, 0.01f).IsUsable);

            Assert.False(Board(0, 8).IsUsable);
            Assert.False(Board(8, 0).IsUsable);
            Assert.False(Board(8, 8, 0f).IsUsable);
            Assert.False(Board(8, 8, -0.06f).IsUsable);
            Assert.False(Board(-4, 8).IsUsable);
        }
    }
}
