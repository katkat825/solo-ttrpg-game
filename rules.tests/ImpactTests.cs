using Rules.Dice;
using Rules.Resolution;
using Xunit;

namespace Rules.Tests
{
    // covers the Impact die
    // it explodes on a maximum, and does not when switched off
    // plus the guard that stops an all-maximum rng chaining forever
    public class ImpactTests
    {
        [Fact]
        public void Impact_Explodes_OnMaximum()
        {
            // d6 rolling 6, 6, 3 -> 15
            var r = new StandardResolver(new ScriptedRng(6, 6, 3));

            Assert.Equal(15, r.RollImpact(Die.D6, explodes: true));
        }

        [Fact]
        public void Impact_DoesNotExplode_WhenDisabled()
        {
            var r = new StandardResolver(new ScriptedRng(6, 6, 3));

            Assert.Equal(6, r.RollImpact(Die.D6, explodes: false));
        }

        [Fact]
        public void Impact_ExplosionChain_IsGuarded()
        {
            // an RNG that only ever returns the maximum must still terminate
            var r = new StandardResolver(new ScriptedRng(4));

            int dmg = r.RollImpact(Die.D4, explodes: true);

            Assert.True(dmg > 0);
            Assert.True(dmg <= 4 * 25);
        }
    }
}
