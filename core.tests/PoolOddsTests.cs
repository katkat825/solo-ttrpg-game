using System.Collections.Generic;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    // holds PoolOdds to the table in CORE_RULES 6, and holds the resolver to PoolOdds
    // the second half is the one that matters: a closed form and a rule that disagree
    // about what a Snag is would each look right on its own
    public class PoolOddsTests
    {
        static Pool Of(params Die[] dice)
        {
            var pool = new Pool();

            // the keys are not what is under test, but a pool die carries one either way
            foreach (Die die in dice) pool.Add(Attr.Might.Key(), die);

            return pool;
        }

        static IEnumerable<Die> Starting => new[] { Die.D8, Die.D6, Die.D6 };

        // ---- against the documented table ----

        // CORE_RULES 6 tabulates "at least one 1" and "two or more 1s" for three pools
        // those two columns are the contract - if this drifts, either the doc or the
        // maths moved, and the doc was checked against exact enumeration
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

        // the number M9 goes looking for on the felt
        // 39.2% is the AT LEAST ONE column and the Snag tier is not it - one 1 exactly is
        // 95/288, and reading the union as the Snag rate overstates the cue by six points
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

        // bigger dice make Snags rarer - the free arc CORE_RULES 6 points out,
        // and the reason the tray has to be told which pool it is measuring
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

        // a None die is not on the felt, so it cannot show a 1
        [Fact]
        public void MissingDiceAreNotCounted()
        {
            Assert.Equal(
                PoolOdds.Snag(new[] { Die.D8, Die.D6 }),
                PoolOdds.Snag(new[] { Die.D8, Die.None, Die.D6 }),
                12);
        }

        // ---- against the resolver ----

        // the closed form describes what PoolResult.Snag counts, or it describes nothing
        // seeded, so a failure here is reproducible rather than a bad afternoon
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

            // 4 sigma - loose enough that no seed ever trips it by luck, tight enough that
            // a tier counted wrongly is nowhere near it
            Assert.InRange(PoolOdds.Drift(snags, throws, Starting), -4.0, 4.0);

            double troubleRate = (double)trouble / throws;
            Assert.Equal(PoolOdds.Trouble(Starting), troubleRate, 2);
        }

        [Fact]
        public void DriftIsZeroWithNothingThrown() =>
            Assert.Equal(0.0, PoolOdds.Drift(0, 0, Starting));
    }
}
