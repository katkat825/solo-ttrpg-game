using System.Collections.Generic;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    public class PoolOddsTests
    {
        static Pool Of(params Die[] dice)
        {
            var pool = new Pool();

            foreach (Die die in dice) pool.Add(Attr.Might.Key(), die);

            return pool;
        }

        static IEnumerable<Die> Starting => new[] { Die.D8, Die.D6, Die.D6 };


        // these two columns are the contract, checked against exact enumeration
        [Theory]
        [InlineData(8, 6, 6, 0.392, 0.062)]
        [InlineData(10, 8, 6, 0.344, 0.046)]
        [InlineData(12, 10, 8, 0.278, 0.029)]
        public void MatchesTheDocumentedRates(int a, int b, int c, double anyOne, double trouble)
        {
            var pool = Of((Die)a, (Die)b, (Die)c);

            Assert.Equal(anyOne, PoolOdds.AnyOne(pool), 3);
            Assert.Equal(trouble, PoolOdds.Trouble(pool), 3);
        }

        // snag is exactly one 1 (95/288), not the "at least one" 39.2%, which overstates by six points
        [Fact]
        public void SnagIsExactlyOneOne_NotTheUnion()
        {
            Assert.Equal(95.0 / 288.0, PoolOdds.Snag(Starting), 6);
            Assert.Equal(0.330, PoolOdds.Snag(Starting), 3);

            Assert.Equal(
                PoolOdds.AnyOne(Starting),
                PoolOdds.Snag(Starting) + PoolOdds.Trouble(Starting),
                12);
        }

        [Fact]
        public void BiggerDiceSnagLess()
        {
            Assert.True(PoolOdds.Snag(new[] { Die.D12, Die.D12, Die.D12 })
                      < PoolOdds.Snag(new[] { Die.D4, Die.D4, Die.D4 }));
        }

        [Fact]
        public void AnEmptyPoolCannotSnag()
        {
            Assert.Equal(0.0, PoolOdds.Snag(new Pool()));
            Assert.Equal(0.0, PoolOdds.Trouble(new Pool()));
        }

        [Fact]
        public void MissingDiceAreNotCounted()
        {
            Assert.Equal(
                PoolOdds.Snag(new[] { Die.D8, Die.D6 }),
                PoolOdds.Snag(new[] { Die.D8, Die.None, Die.D6 }),
                12);
        }


        [Fact]
        public void TheResolverSnagsAtTheRateThisPredicts()
        {
            const int throws = 200_000;

            var pool = Of(Die.D8, Die.D6, Die.D6);
            var resolver = new StandardResolver(new SeededRng(4242));

            int snags = 0;
            int trouble = 0;

            for (int i = 0; i < throws; i++)
            {
                PoolResult r = resolver.Resolve(pool);

                if (r.Snag) snags++;
                if (r.Trouble) trouble++;
            }

            // 4 sigma: no seed trips it by luck, but a mis-counted tier is nowhere near
            Assert.InRange(PoolOdds.Drift(snags, throws, Starting), -4.0, 4.0);

            double troubleRate = (double)trouble / throws;
            Assert.Equal(PoolOdds.Trouble(Starting), troubleRate, 2);
        }

        [Fact]
        public void DriftIsZeroWithNothingThrown() =>
            Assert.Equal(0.0, PoolOdds.Drift(0, 0, Starting));
    }
}
