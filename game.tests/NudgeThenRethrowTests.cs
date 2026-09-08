using Game.Dice;
using Xunit;

namespace Game.Tests
{
    // the default IDieRecovery, and the policy M0-M3 were measured with
    //
    // CONVENTIONS says the seams inside game/ are guarded by reading them rather than by a
    // test, because core.tests must not compile files out of game/. this is one of the seams
    // F1 stopped having to take on trust: the policy is pure, so it can simply be asked
    //
    // the property that matters is TERMINATION. a cocked die runs out of nudges and then out
    // of rethrows and is finally accepted; an escaped die is thrown back with less and less
    // until the throw is nothing at all, which drops it inside the tray. neither can loop
    public class NudgeThenRethrowTests
    {
        static readonly IDieRecovery Policy = new NudgeThenRethrow(maxNudges: 2, maxRethrows: 1, maxEscapes: 3);

        static DieRecoveryStep Cocked(int nudges, int rethrows) =>
            Policy.Cocked(new CockedDie(value: 3, alignment: 0.6f, required: 0.9f, nudges, rethrows));

        // ---- a die on its edge ----

        [Theory]
        [InlineData(0, DieRecoveryAction.Nudge)]
        [InlineData(1, DieRecoveryAction.Nudge)]
        [InlineData(2, DieRecoveryAction.Rethrow)]
        public void ItTapsBeforeItThrows(int nudgesSoFar, DieRecoveryAction expected) =>
            Assert.Equal(expected, Cocked(nudgesSoFar, rethrows: 0).Action);

        // taking the nearest face is wrong, looping forever is worse
        [Fact]
        public void OutOfNudgesAndOutOfRethrows_ItTakesWhatIsShowing() =>
            Assert.Equal(DieRecoveryAction.Accept, Cocked(nudges: 2, rethrows: 1).Action);

        // ---- a die out of the tray ----

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

        // the last rethrow carries no energy at all, so the die drops from its spawn point -
        // which is inside the tray. that is what makes the walk down terminate
        [Fact]
        public void TheLastRethrowIsADrop() =>
            Assert.Equal(0f, Policy.Escaped(new EscapedDie(3, 0.4)).Energy, 5);

        [Fact]
        public void PastTheLastEscape_ItStopsThrowing() =>
            Assert.Equal(DieRecoveryAction.Accept, Policy.Escaped(new EscapedDie(4, 0.4)).Action);
    }
}
