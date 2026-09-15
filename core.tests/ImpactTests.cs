using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Core.Tests
{
    public class ImpactTests
    {
        [Fact]
        public void Impact_Explodes_OnMaximum()
        {
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
            var r = new StandardResolver(new ScriptedRng(4));

            int dmg = r.RollImpact(Die.D4, explodes: true);

            Assert.True(dmg > 0);
            Assert.True(dmg <= 4 * 25);
        }
    }
}
