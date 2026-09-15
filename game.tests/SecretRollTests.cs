using Core.Dice;
using Game.Dm;
using Xunit;

namespace Game.Tests
{
    public sealed class SecretRollTests
    {
        const double Frame = 1.0 / 60.0;

        [Fact]
        public void ItSaysNothingUntilTheRestIsServed()
        {
            // two rattles must not stack; the silence between them is what makes one mean anything
            var roll = new SecretRoll(new SeededRng(4242));

            Fire(roll, seconds: 200.0);

            int inTheRest = 0;

            for (double t = 0.0; t < SecretRoll.RestSeconds * 0.9; t += Frame)
                if (roll.ForNothing(Frame)) inTheRest++;

            Assert.Equal(0, inTheRest);
        }

        // measure the rate, not the constant: a frame-rate bug would break the pace silently
        [Fact]
        public void AnIdleTableRattlesOccasionally()
        {
            var roll = new SecretRoll(new SeededRng(99));

            const double Minutes = 30.0;

            int rattles = 0;

            for (double t = 0.0; t < Minutes * 60.0; t += Frame)
                if (roll.ForNothing(Frame)) rattles++;

            double perMinute = rattles / Minutes;

            Assert.InRange(perMinute, 0.5, 6.0);
        }

        [Fact]
        public void TheSameSeedRattlesAtTheSameMoments()
        {
            Assert.Equal(Moments(4242), Moments(4242));
            Assert.NotEqual(Moments(4242), Moments(1));
        }

        static string Moments(int seed)
        {
            var roll = new SecretRoll(new SeededRng(seed));
            var at = new System.Text.StringBuilder();

            for (int frame = 0; frame < 60 * 600; frame++)
                if (roll.ForNothing(Frame)) at.Append(frame).Append(',');

            return at.ToString();
        }

        // a pre-swing roll must always fire, or its absence becomes the tell
        [Fact]
        public void AMeantRollRestartsTheRest()
        {
            var roll = new SecretRoll(new SeededRng(7));

            Fire(roll, seconds: 200.0);

            roll.Meant();

            int straightAfter = 0;

            for (double t = 0.0; t < SecretRoll.RestSeconds * 0.9; t += Frame)
                if (roll.ForNothing(Frame)) straightAfter++;

            Assert.Equal(0, straightAfter);
        }

        static void Fire(SecretRoll roll, double seconds)
        {
            for (double t = 0.0; t < seconds; t += Frame)
                if (roll.ForNothing(Frame)) return;
        }
    }
}
