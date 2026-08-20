using Core.Dice;
using Xunit;

namespace Core.Tests
{
    // covers the die enum itself
    // stepping up and down, clamped at d4 and d12
    // and RecordingRng, which every reproducible debugging session leans on
    public class DieTests
    {
        [Fact]
        public void StepDown_ClampsAtD4()
        {
            Assert.Equal(Die.D10, Die.D12.StepDown());
            Assert.Equal(Die.D6, Die.D8.StepDown());
            Assert.Equal(Die.D4, Die.D4.StepDown());
        }

        [Fact]
        public void StepUp_ClampsAtD12()
        {
            Assert.Equal(Die.D6, Die.D4.StepUp());
            Assert.Equal(Die.D12, Die.D12.StepUp());
        }

        [Fact]
        public void Sides_MatchesEnumValue()
        {
            Assert.Equal(8, Die.D8.Sides());
            Assert.Equal(12, Die.D12.Sides());
        }

        [Fact]
        public void None_IsNotReal()
        {
            Assert.False(Die.None.IsReal());
            Assert.True(Die.D4.IsReal());
        }

        [Fact]
        public void RecordingRng_CapturesEveryRoll()
        {
            var rng = new RecordingRng(new ScriptedRng(3, 5));

            rng.Roll(6);
            rng.Roll(8);

            Assert.Equal(2, rng.Rolls.Count);
            Assert.Equal((6, 3), rng.Rolls[0]);
            Assert.Equal((8, 5), rng.Rolls[1]);
        }
    }
}
