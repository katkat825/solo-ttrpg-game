using System.Collections.Generic;
using Core.Space;
using Xunit;

namespace Core.Tests
{
    // a Cell is a coordinate, not a thing, so what has to be true of it is that two of the same
    // square are interchangeable everywhere - as a dictionary key especially, which is how the
    // grid answers "who is standing here" and how B3's pathfinding will keep its frontier
    public class CellTests
    {
        [Fact]
        public void SameCoordinates_AreTheSameCell()
        {
            Assert.Equal(new Cell(3, 4), new Cell(3, 4));
            Assert.True(new Cell(3, 4) == new Cell(3, 4));
            Assert.False(new Cell(3, 4) != new Cell(3, 4));
        }

        [Fact]
        public void TheAxesAreNotInterchangeable()
        {
            Assert.NotEqual(new Cell(3, 4), new Cell(4, 3));
            Assert.True(new Cell(3, 4) != new Cell(4, 3));
        }

        [Fact]
        public void SameCell_HashesTheSame()
        {
            Assert.Equal(new Cell(3, 4).GetHashCode(), new Cell(3, 4).GetHashCode());
        }

        // the property that actually matters - a hash that collided for a whole board would
        // still pass the test above and turn every lookup into a scan
        [Fact]
        public void ABoardsWorthOfCells_HashDistinctly()
        {
            var hashes = new HashSet<int>();

            for (int x = 0; x < 16; x++)
                for (int y = 0; y < 16; y++)
                    hashes.Add(new Cell(x, y).GetHashCode());

            Assert.Equal(16 * 16, hashes.Count);
        }

        [Fact]
        public void NegativeCoordinates_AreOrdinaryCells()
        {
            // off the board is the grid's judgement, not the cell's - a Cell is just a pair of
            // numbers, and B3's line of sight will step through cells outside the map
            Assert.Equal(new Cell(-1, -1), new Cell(-1, -1));
            Assert.NotEqual(new Cell(-1, -1), new Cell(1, 1));
        }

        [Fact]
        public void CellsWorkAsDictionaryKeys()
        {
            var by = new Dictionary<Cell, string> { [new Cell(2, 5)] = "here" };

            Assert.True(by.ContainsKey(new Cell(2, 5)));
            Assert.False(by.ContainsKey(new Cell(5, 2)));
        }
    }
}
