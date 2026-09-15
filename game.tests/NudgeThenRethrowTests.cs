using Game.Dice;
using Xunit;

namespace Game.Tests
{
    // the property is termination: a cocked die runs out of nudges then rethrows; an escaped one is thrown weaker until it drops
    public class NudgeThenRethrowTests
    {
        static readonly IDieRecovery Policy = new NudgeThenRethrow(maxNudges: 2, maxRethrows: 1, maxEscapes: 3);

        static DieRecoveryStep Cocked(int nudges, int rethrows) =>
            Policy.Cocked(new CockedDie(value: 3, alignment: 0.6f, required: 0.9f, nudges, rethrows));


        [Theory]
        [InlineData(0, DieRecoveryAction.Nudge)]
        [InlineData(1, DieRecoveryAction.Nudge)]
        [InlineData(2, DieRecoveryAction.Rethrow)]
        public void ItTapsBeforeItThrows(int nudgesSoFar, DieRecoveryAction expected) =>
            Assert.Equal(expected, Cocked(nudgesSoFar, rethrows: 0).Action);

        [Fact]
        public void OutOfNudgesAndOutOfRethrows_ItTakesWhatIsShowing() =>
            Assert.Equal(DieRecoveryAction.Accept, Cocked(nudges: 2, rethrows: 1).Action);


        [Theory]
        [InlineData(1, 1 - 1 / 3f)]
        [InlineData(2, 1 - 2 / 3f)]
        [InlineData(3, 0f)]
        public void EachEscapeComesBackWithLess(int escapes, float energy)
        {
            DieRecoveryStep step = Policy.Escaped(new EscapedDie(escapes, flightSeconds: 0.4));

            Assert.Equal(DieRecoveryAction.Rethrow, step.Action);
            Assert.Equal(energy, step.Energy, 5);
        }

        // the last rethrow carries no energy, so the die drops inside the tray, which ends the walk
        [Fact]
        public void TheLastRethrowIsADrop() =>
            Assert.Equal(0f, Policy.Escaped(new EscapedDie(3, 0.4)).Energy, 5);

        [Fact]
        public void PastTheLastEscape_ItStopsThrowing() =>
            Assert.Equal(DieRecoveryAction.Accept, Policy.Escaped(new EscapedDie(4, 0.4)).Action);
    }
}
