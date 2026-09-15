using System.Collections.Generic;
using Core.Space;
using Xunit;

namespace Core.Tests
{
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

        // a hash that collided for a whole board would still pass the equality test but scan every lookup
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
