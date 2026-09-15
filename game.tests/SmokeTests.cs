using Core.Characters;
using Game.Board;

namespace Game.Tests
{
    // if this stops compiling, game/ has fallen out of the solution and every test here went quiet
    public class SmokeTests
    {
        [Fact]
        public void AGodotFreeHelperInGame_IsReachableFromATestProject() =>
            Assert.Equal(3, DoorCheck.PoolFor(
                new BuiltInArchetypes().Create(EngineIds.Barbarian)).Count);
    }
}
