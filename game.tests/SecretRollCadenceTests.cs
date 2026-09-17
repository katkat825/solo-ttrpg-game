using Core.Dice;
using Game.Dm;
using Xunit;

namespace Game.Tests
{
    // HOW OFTEN THE DM REACHES BEHIND THE SCREEN.
    //
    // The eye check of 2026-09-16 found the shipped cadence was a nervous tic - which is the exact
    // failure SecretRoll's own comment warned about, and nothing was holding it to the warning.
    // These are that hold. A rattle is a device: you hear it, you do not know what it meant, and
    // the not knowing is the whole effect. A sound that happens every fifteen seconds is furniture.
    public sealed class SecretRollCadenceTests
    {
        // one second of frames at 60 Hz
        const double Frame = 1.0 / 60.0;

        static double SecondsBetween(SecretRoll roll, int rattles)
        {
            double elapsed = 0.0;
            int seen = 0;

            // a generous ceiling; a cadence that needs longer than this has already failed
            while (seen < rattles && elapsed < 60 * 60)
            {
                elapsed += Frame;

                if (roll.ForNothing(Frame)) seen++;
            }

            return seen == 0 ? double.PositiveInfinity : elapsed / seen;
        }

        [Fact]
        public void TheShippedCadenceIsAboutAMinuteApart()
        {
            var roll = new SecretRoll(new SeededRng(4242));

            Assert.InRange(roll.Every, 45.0, 120.0);
        }

        [Fact]
        public void ItActuallyRattlesAboutThatOften()
        {
            var roll = new SecretRoll(new SeededRng(4242));

            double between = SecondsBetween(roll, 40);

            // the draw is random, so this is a band and not a number; what it refuses is the old
            // behaviour, which averaged a rattle every fifteen seconds
            Assert.InRange(between, 45.0, 120.0);
        }

        // the rest is the floor BETWEEN two rattles, not a silence before the first: a session
        // that opens with the DM already behind the screen is allowed to open with a rattle
        [Fact]
        public void TheRestIsAFloorNoDrawCanGetUnder()
        {
            var roll = new SecretRoll(new SeededRng(7), rest: 20.0, chance: 1.0);

            while (!roll.ForNothing(Frame)) { }

            double elapsed = 0.0;

            while (!roll.ForNothing(Frame)) elapsed += Frame;

            // even at a certainty per second, nothing fires inside the rest
            Assert.True(elapsed >= 20.0 - Frame, $"it rattled after {elapsed:0.00}s of a 20s rest");
        }

        [Fact]
        public void ARealHiddenRollPushesTheNextIdleOneAway()
        {
            var roll = new SecretRoll(new SeededRng(7), rest: 20.0, chance: 1.0);

            // wait the rest out, then say a real roll happened
            for (double t = 0; t < 25.0; t += Frame) roll.ForNothing(Frame);

            roll.Meant();

            double elapsed = 0.0;

            while (!roll.ForNothing(Frame)) elapsed += Frame;

            Assert.True(elapsed >= 20.0 - Frame,
                        "an idle rattle stacked on top of a real one, which is two sounds where " +
                        "the device needs one");
        }

        // a table that wants no tic at all is a legitimate setting, not a broken one
        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void AChanceOfNothingMeansTheDmNeverRollsForNothing(double chance)
        {
            var roll = new SecretRoll(new SeededRng(4242), rest: 1.0, chance: chance);

            for (double t = 0; t < 600.0; t += Frame)
                Assert.False(roll.ForNothing(Frame));

            Assert.Equal(double.PositiveInfinity, roll.Every);
        }

        [Fact]
        public void TheFrameRateDoesNotChangeTheCadence()
        {
            double slow = SecondsBetween(new SecretRoll(new SeededRng(4242)), 30);

            var fast = new SecretRoll(new SeededRng(4242));
            double elapsed = 0.0;
            int seen = 0;
            const double Tick = 1.0 / 144.0;

            while (seen < 30 && elapsed < 60 * 60)
            {
                elapsed += Tick;

                if (fast.ForNothing(Tick)) seen++;
            }

            double quick = elapsed / seen;

            // a 144 Hz DM must not twitch more than a 60 Hz one; the rate is per second by design
            Assert.InRange(quick, slow * 0.6, slow * 1.6);
        }
    }
}
