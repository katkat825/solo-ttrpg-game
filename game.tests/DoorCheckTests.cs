using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Space;
using Game.Board;
using Game.Tray;

namespace Game.Tests
{
    public class DoorCheckTests
    {
        static Actor Hero() => new BuiltInArchetypes().Create(EngineIds.Barbarian);

        static TrayThrow Throw(params int[] faces) =>
            new TrayResolution(DoorCheck.PoolFor(Hero())).Resolve(faces);


        [Fact]
        public void ThePoolIsTheHerosOwn()
        {
            Pool pool = DoorCheck.PoolFor(Hero());

            Assert.Equal(3, pool.Count);

            Assert.Equal("attr.might.name", pool.Dice[0].LabelKey);
            Assert.Equal("skill.blades.name", pool.Dice[1].LabelKey);
            Assert.Equal("gear.axe.name", pool.Dice[2].LabelKey);

            Assert.Equal(Die.D8, pool.Dice[0].Die);
        }

        [Fact]
        public void ItFillsTheTray()
        {
            Assert.Equal(3, DoorCheck.PoolFor(Hero()).Count);
        }

        // an empty pool, not null: both hero actions degrade the same way instead of one returning null
        [Fact]
        public void NoHeroMeansAnEmptyPool()
        {
            Assert.Equal(0, DoorCheck.PoolFor(null).Count);
        }


        [Fact]
        public void BeatingItTakesTheDoorCleanly()
        {
            DoorOutcome outcome = DoorCheck.Read(Throw(5, 4, 2));

            Assert.True(outcome.Forced);
            Assert.Equal(Tile.Floor, outcome.Leaves);
            Assert.Equal(9, outcome.Total);
            Assert.Equal(0, outcome.Cost);
        }

        [Fact]
        public void OneShortOfItDoesNot()
        {
            DoorOutcome outcome = DoorCheck.Read(Throw(4, 4, 3));

            Assert.False(outcome.Forced);
            Assert.Equal(8, outcome.Total);
        }

        // failure is content not a wall: a failed check gets the hero through the hard way, leaving difficult ground
        [Fact]
        public void FailingGetsThroughAnyway_AndLeavesAMess()
        {
            DoorOutcome outcome = DoorCheck.Read(Throw(4, 4, 3));

            Assert.False(outcome.Forced);

            Assert.Equal(Tile.Rough, outcome.Leaves);
            Assert.True(outcome.Leaves.IsPassable());
            Assert.Equal(2, outcome.Leaves.MoveCost());
        }

        // the comeback's strength is the die already on the felt, never a second hidden roll
        [Fact]
        public void ItCostsTheImpactDieThatIsShowing()
        {
            TrayThrow thrown = Throw(4, 4, 3);

            Assert.Equal(3, thrown.ImpactValue);
            Assert.Equal(3, DoorCheck.Read(thrown).Cost);
        }

        [Fact]
        public void ACleanBreakCostsNothing()
        {
            Assert.Equal(0, DoorCheck.Read(Throw(6, 5, 1)).Cost);
        }

        [Fact]
        public void NoThrowIsNoOutcome()
        {
            DoorOutcome outcome = DoorCheck.Read(null);

            Assert.False(outcome.Forced);
            Assert.Equal(Tile.Floor, outcome.Leaves);
            Assert.Equal(0, outcome.Cost);
        }


        [Fact]
        public void EnoughOfACostStepsTheHerosDiceDown()
        {
            Actor hero = Hero();
            Die before = DoorCheck.PoolFor(hero).Dice[0].Die;

            hero.Damage(hero.MaxVigor);

            Assert.True(hero.HasCondition(Condition.Winded));
            Assert.True(DoorCheck.PoolFor(hero).Dice[0].Die < before);
        }
    }
}
