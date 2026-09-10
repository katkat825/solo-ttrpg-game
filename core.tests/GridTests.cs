using System.Collections.Generic;
using System.Linq;
using Core.Space;
using Xunit;

namespace Core.Tests
{
    // the grid is extent plus occupancy and nothing else, so what has to hold is that the two
    // halves never disagree: a piece is in exactly one square, a square holds at most one piece,
    // and every refusal leaves the board exactly as it was.
    //
    // this is where core/ earns out on the board. "the mini never ends up between cells or off
    // the board" is B1's verify line and it is checked by eye there; here it is checked by
    // machine, which is the only way that claim survives B3's pathfinding
    public class GridTests
    {
        // a piece is anything the view cares to put on a square - the grid only ever compares
        // them by identity
        sealed class Piece
        {
            public readonly string Name;

            public Piece(string name) => Name = name;

            // deliberately wrong-headed: two pieces with the same name claim to be equal, which
            // is exactly the trap ReferenceEqualityComparer exists to avoid
            public override bool Equals(object obj) => obj is Piece other && other.Name == Name;

            public override int GetHashCode() => Name.GetHashCode();
        }

        static Grid<Piece> Board(int columns = 8, int rows = 8) => new Grid<Piece>(columns, rows);

        // ---- extent ----

        [Fact]
        public void Extent_IsWhatItWasBuiltWith()
        {
            Grid<Piece> grid = Board(8, 5);

            Assert.Equal(8, grid.Columns);
            Assert.Equal(5, grid.Rows);
            Assert.Equal(40, grid.Count);
        }

        [Fact]
        public void ContainsOnlyTheSquaresItHas()
        {
            Grid<Piece> grid = Board(8, 5);

            Assert.True(grid.Contains(new Cell(0, 0)));
            Assert.True(grid.Contains(new Cell(7, 4)));

            Assert.False(grid.Contains(new Cell(8, 4)));   // one past the last column
            Assert.False(grid.Contains(new Cell(7, 5)));   // one past the last row
            Assert.False(grid.Contains(new Cell(-1, 0)));
            Assert.False(grid.Contains(new Cell(0, -1)));
        }

        [Fact]
        public void Cells_AreEveryCellOnce_InRowMajorOrder()
        {
            List<Cell> cells = Board(3, 2).Cells.ToList();

            Assert.Equal(6, cells.Count);
            Assert.Equal(6, cells.Distinct().Count());

            Assert.Equal(new Cell(0, 0), cells[0]);
            Assert.Equal(new Cell(2, 0), cells[2]);
            Assert.Equal(new Cell(0, 1), cells[3]);
            Assert.Equal(new Cell(2, 1), cells[5]);
        }

        [Fact]
        public void AGridWithNoSquares_IsEmptyRatherThanBroken()
        {
            Grid<Piece> grid = Board(0, 0);

            Assert.Equal(0, grid.Count);
            Assert.Empty(grid.Cells);
            Assert.False(grid.Contains(new Cell(0, 0)));
            Assert.False(grid.Place(new Piece("mini"), new Cell(0, 0)));
        }

        // ---- occupancy ----

        [Fact]
        public void APlacedPiece_IsOnItsSquareAndNowhereElse()
        {
            Grid<Piece> grid = Board();
            var mini = new Piece("mini");

            Assert.True(grid.Place(mini, new Cell(3, 4)));

            Assert.Same(mini, grid.At(new Cell(3, 4)));
            Assert.True(grid.IsOccupied(new Cell(3, 4)));
            Assert.Equal(new Cell(3, 4), grid.CellOf(mini));

            Assert.Null(grid.At(new Cell(3, 5)));
            Assert.False(grid.IsOccupied(new Cell(3, 5)));
        }

        [Fact]
        public void AnEmptySquare_AndOneOffTheBoard_BothAnswerNobody()
        {
            Grid<Piece> grid = Board();

            Assert.Null(grid.At(new Cell(1, 1)));
            Assert.Null(grid.At(new Cell(99, 99)));
            Assert.Null(grid.CellOf(new Piece("never placed")));
        }

        [Fact]
        public void MovingLeavesNobodyBehind()
        {
            Grid<Piece> grid = Board();
            var mini = new Piece("mini");

            grid.Place(mini, new Cell(3, 4));
            Assert.True(grid.Move(mini, new Cell(6, 1)));

            Assert.False(grid.IsOccupied(new Cell(3, 4)));
            Assert.Same(mini, grid.At(new Cell(6, 1)));
            Assert.Equal(new Cell(6, 1), grid.CellOf(mini));
        }

        [Fact]
        public void APieceIsOnExactlyOneSquare_HoweverManyTimesItIsPlaced()
        {
            Grid<Piece> grid = Board();
            var mini = new Piece("mini");

            grid.Place(mini, new Cell(0, 0));
            grid.Place(mini, new Cell(1, 1));
            grid.Place(mini, new Cell(2, 2));

            Assert.Single(grid.Cells, grid.IsOccupied);
            Assert.Equal(new Cell(2, 2), grid.CellOf(mini));
        }

        [Fact]
        public void MovingNowhere_IsASuccessAndChangesNothing()
        {
            Grid<Piece> grid = Board();
            var mini = new Piece("mini");

            grid.Place(mini, new Cell(3, 4));

            Assert.True(grid.Move(mini, new Cell(3, 4)));
            Assert.Equal(new Cell(3, 4), grid.CellOf(mini));
            Assert.Single(grid.Cells, grid.IsOccupied);
        }

        // ---- refusals, and that they leave the board alone ----

        [Fact]
        public void OffTheBoard_IsRefused()
        {
            Grid<Piece> grid = Board();
            var mini = new Piece("mini");

            grid.Place(mini, new Cell(3, 4));

            Assert.False(grid.Place(mini, new Cell(8, 0)));
            Assert.False(grid.Move(mini, new Cell(-1, 0)));

            // still exactly where it was
            Assert.Equal(new Cell(3, 4), grid.CellOf(mini));
            Assert.Single(grid.Cells, grid.IsOccupied);
        }

        [Fact]
        public void TwoPieces_CannotShareASquare()
        {
            Grid<Piece> grid = Board();
            var first = new Piece("first");
            var second = new Piece("second");

            grid.Place(first, new Cell(2, 2));

            Assert.False(grid.Place(second, new Cell(2, 2)));
            Assert.Same(first, grid.At(new Cell(2, 2)));
            Assert.Null(grid.CellOf(second));
        }

        [Fact]
        public void ARefusedMove_DoesNotLiftThePieceOffItsSquare()
        {
            Grid<Piece> grid = Board();
            var first = new Piece("first");
            var second = new Piece("second");

            grid.Place(first, new Cell(2, 2));
            grid.Place(second, new Cell(5, 5));

            Assert.False(grid.Move(second, new Cell(2, 2)));

            Assert.Equal(new Cell(5, 5), grid.CellOf(second));
            Assert.Same(second, grid.At(new Cell(5, 5)));
            Assert.Same(first, grid.At(new Cell(2, 2)));
        }

        [Fact]
        public void MovingAPieceThatIsNotOnTheBoard_IsRefused()
        {
            Grid<Piece> grid = Board();
            var stranger = new Piece("stranger");

            Assert.False(grid.Move(stranger, new Cell(1, 1)));
            Assert.False(grid.IsOccupied(new Cell(1, 1)));

            // Place is how a piece arrives, and it works where Move would not
            Assert.True(grid.Place(stranger, new Cell(1, 1)));
        }

        [Fact]
        public void Nothing_IsNotAnOccupant()
        {
            Grid<Piece> grid = Board();

            Assert.False(grid.Place(null, new Cell(1, 1)));
            Assert.False(grid.Move(null, new Cell(1, 1)));
            Assert.False(grid.Remove(null));
            Assert.Null(grid.CellOf(null));
            Assert.False(grid.IsOccupied(new Cell(1, 1)));
        }

        // ---- identity ----

        // the Piece above claims two same-named pieces are equal. the board must not believe it:
        // a grid keyed on Equals would report the second mini standing where the first one is
        [Fact]
        public void TwoPiecesThatCallThemselvesEqual_AreStillTwoPieces()
        {
            Grid<Piece> grid = Board();
            var first = new Piece("rabble");
            var second = new Piece("rabble");

            Assert.True(grid.Place(first, new Cell(1, 1)));
            Assert.True(grid.Place(second, new Cell(4, 4)));

            Assert.Equal(new Cell(1, 1), grid.CellOf(first));
            Assert.Equal(new Cell(4, 4), grid.CellOf(second));
            Assert.Same(first, grid.At(new Cell(1, 1)));
            Assert.Same(second, grid.At(new Cell(4, 4)));
        }

        // ---- leaving ----

        [Fact]
        public void RemovingAPiece_FreesItsSquare()
        {
            Grid<Piece> grid = Board();
            var mini = new Piece("mini");

            grid.Place(mini, new Cell(3, 3));

            Assert.True(grid.Remove(mini));
            Assert.False(grid.IsOccupied(new Cell(3, 3)));
            Assert.Null(grid.CellOf(mini));

            Assert.False(grid.Remove(mini));   // and it stays gone
        }
    }
}
