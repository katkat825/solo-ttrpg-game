using Godot;
using Game.Loot;

namespace Game.Tests
{
    // THE STACK OF CARDS THAT IS THE WHOLE INVENTORY.
    //
    // There is no bag, no grid of slots and no weight, because none of those is a thing on a table.
    // What has to hold for a stack to stay a stack rather than becoming a list is here: only one
    // card is ever face up, the stack never grows wider than a hand, and turning the one that is
    // up puts it back down.
    public class DealtTests
    {
        static Dealt Carrying(params string[] items)
        {
            var dealt = new Dealt();

            foreach (string item in items) dealt.Deal(item);

            return dealt;
        }

        [Fact]
        public void ANewStackIsEmptyAndSquare()
        {
            var dealt = new Dealt();

            Assert.Equal(0, dealt.Count);
            Assert.Equal(-1, dealt.FaceUp);
            Assert.Equal("", dealt.Reading);
        }

        [Fact]
        public void ACardDealtGoesOnTheStack()
        {
            Dealt dealt = Carrying("rope");

            Assert.Equal(1, dealt.Count);
            Assert.Equal("rope", dealt.At(0));
        }

        // two of the same thing are two cards. A stack is what you are carrying, not a set
        [Fact]
        public void TwoOfTheSameThingAreTwoCards()
        {
            Dealt dealt = Carrying("rope", "rope");

            Assert.Equal(2, dealt.Count);
        }

        [Fact]
        public void NothingIsNotACard()
        {
            var dealt = new Dealt();

            Assert.Equal(-1, dealt.Deal(null));
            Assert.Equal(-1, dealt.Deal("   "));
            Assert.Equal(0, dealt.Count);
        }

        [Fact]
        public void TurningOneOverReadsIt()
        {
            Dealt dealt = Carrying("rope", "lantern");

            Assert.True(dealt.Flip(1));
            Assert.Equal(1, dealt.FaceUp);
            Assert.Equal("lantern", dealt.Reading);
        }

        // ONLY ONE IS EVER FACE UP. A fan of open cards is a list again
        [Fact]
        public void TurningASecondOnePutsTheFirstBack()
        {
            Dealt dealt = Carrying("rope", "lantern", "chalk");

            dealt.Flip(0);
            dealt.Flip(2);

            Assert.Equal(2, dealt.FaceUp);
            Assert.Equal("chalk", dealt.Reading);
        }

        [Fact]
        public void TurningTheOneThatIsUpPutsItDown()
        {
            Dealt dealt = Carrying("rope");

            dealt.Flip(0);
            dealt.Flip(0);

            Assert.Equal(-1, dealt.FaceUp);
            Assert.Equal("", dealt.Reading);
        }

        [Fact]
        public void ACardThatIsNotThereCannotBeTurned()
        {
            Dealt dealt = Carrying("rope");

            Assert.False(dealt.Flip(-1));
            Assert.False(dealt.Flip(9));
            Assert.Equal(-1, dealt.FaceUp);
        }

        [Fact]
        public void TakingACardOutOfTheMiddleKeepsTheRightOneFaceUp()
        {
            Dealt dealt = Carrying("rope", "lantern", "chalk");

            dealt.Flip(2);
            dealt.Take(0);

            Assert.Equal(2, dealt.Count);
            Assert.Equal("chalk", dealt.Reading);
        }

        [Fact]
        public void TakingTheCardYouWereReadingSquaresTheStack()
        {
            Dealt dealt = Carrying("rope", "lantern");

            dealt.Flip(1);
            dealt.Take(1);

            Assert.Equal(-1, dealt.FaceUp);
        }


        // ---- where the cards lie ----------------------------------------------------------------

        [Fact]
        public void TheFirstCardSitsWhereTheStackIs()
        {
            Assert.Equal(Vector3.Zero, Carrying("rope").Sits(0) with { Y = 0f });
        }

        [Fact]
        public void EachCardSitsAlongFromTheOneUnderIt()
        {
            Dealt dealt = Carrying("a", "b", "c");

            Assert.Equal(Dealt.Step, dealt.Sits(1).X - dealt.Sits(0).X, 5);
            Assert.Equal(Dealt.Step, dealt.Sits(2).X - dealt.Sits(1).X, 5);
        }

        // no two card faces are coplanar, which is what stops a stack flickering
        [Fact]
        public void NoTwoCardsLieAtTheSameHeight()
        {
            Dealt dealt = Carrying("a", "b", "c", "d");

            for (int at = 1; at < dealt.Count; at++)
                Assert.True(dealt.Sits(at).Y > dealt.Sits(at - 1).Y);
        }

        [Fact]
        public void TheOneYouAreReadingComesUpOffTheStack()
        {
            Dealt dealt = Carrying("rope", "lantern");

            float lying = dealt.Sits(1).Y;

            dealt.Flip(1);

            Assert.Equal(lying + Dealt.Raised, dealt.Sits(1).Y, 5);
        }

        // A STACK THAT GROWS WITHOUT BOUND WALKS OFF THE TABLE BY THE THIRD DUNGEON
        [Fact]
        public void PastAHandfulItIsAPileRatherThanAFan()
        {
            var dealt = new Dealt();

            for (int at = 0; at < 60; at++) dealt.Deal("thing" + at);

            Assert.Equal(Dealt.Step * Dealt.MostSpread, dealt.Spread, 5);
            Assert.Equal(dealt.Sits(Dealt.MostSpread).X, dealt.Sits(59).X, 5);
        }

        [Fact]
        public void AStackIsNeverWiderThanAHand()
        {
            var dealt = new Dealt();

            for (int at = 0; at < 500; at++) dealt.Deal("thing" + at);

            Assert.True(dealt.Spread < 0.12f, $"the stack is {dealt.Spread:0.000} m across");
        }

        [Fact]
        public void ACardThatIsNotThereLiesNowhere()
        {
            Assert.Equal(Vector3.Zero, new Dealt().Sits(3));
        }

        [Fact]
        public void SweepingItTakesEverything()
        {
            Dealt dealt = Carrying("rope", "lantern");

            dealt.Flip(0);
            dealt.Sweep();

            Assert.Equal(0, dealt.Count);
            Assert.Equal(-1, dealt.FaceUp);
        }
    }
}
