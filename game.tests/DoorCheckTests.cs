using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Core.Space;
using Game.Board;
using Game.Tray;

namespace Game.Tests
{
    // B4's one check, and the only hardcoded content in the phase. What is worth holding here is
    // not the dice - StandardResolver is tested to death in core.tests - but the two things this
    // file decides on top of them:
    //
    //   the pool is the HERO'S, built by Actor.BuildPool and no other way
    //   failure is CONTENT, not a wall: both outcomes get the hero through, and they differ
    //
    // The second is the one to protect. "The door stays shut, try again" is the repeated scene
    // CORE_RULES 0 pillar 4 forbids, and it is exactly what this would quietly decay into the
    // first time somebody made a failed check cheaper to write.
    public class DoorCheckTests
    {
        static Actor Hero() => new BuiltInArchetypes().Create(EngineIds.Barbarian);

        // the hero's own pool, thrown on the felt, showing these faces in throw order
        static TrayThrow Throw(params int[] faces) =>
            new TrayResolution(DoorCheck.PoolFor(Hero())).Resolve(faces);

        // ---- the pool ----

        [Fact]
        public void ThePoolIsTheHerosOwn()
        {
            Pool pool = DoorCheck.PoolFor(Hero());

            Assert.Equal(3, pool.Count);

            // attribute, skill, gear - in that order, as keys and never as words
            Assert.Equal("attr.might.name", pool.Dice[0].LabelKey);
            Assert.Equal("skill.blades.name", pool.Dice[1].LabelKey);
            Assert.Equal("gear.axe.name", pool.Dice[2].LabelKey);

            Assert.Equal(Die.D8, pool.Dice[0].Die);
        }

        // the whole reason the check is Might and Blades rather than Grace and Larceny: the tray
        // has three dice and cannot sit one out yet, and the barbarian is trained in exactly one
        // thing. an untrained check is a real thing the rules handle and the TRAY cannot draw
        [Fact]
        public void ItFillsTheTray()
        {
            Assert.Equal(3, DoorCheck.PoolFor(Hero()).Count);
        }

        [Fact]
        public void NoHeroMeansNoPool()
        {
            Assert.Null(DoorCheck.PoolFor(null));
        }

        // ---- what the felt decides ----

        [Fact]
        public void BeatingItTakesTheDoorCleanly()
        {
            // 5 + 4 = 9, exactly Standard, and the leftover d6 shows 2
            DoorOutcome outcome = DoorCheck.Read(Throw(5, 4, 2));

            Assert.True(outcome.Forced);
            Assert.Equal(Tile.Floor, outcome.Leaves);   // nothing left behind
            Assert.Equal(9, outcome.Total);
            Assert.Equal(0, outcome.Cost);
        }

        [Fact]
        public void OneShortOfItDoesNot()
        {
            // 4 + 4 = 8 against Standard 9
            DoorOutcome outcome = DoorCheck.Read(Throw(4, 4, 3));

            Assert.False(outcome.Forced);
            Assert.Equal(8, outcome.Total);
        }

        // FAILURE IS CONTENT, NOT A WALL. a failed check does not leave the door shut for another
        // click - it gets the hero through the hard way, and the board keeps the difference.
        //
        // since EDGE_WALLS.md the wreckage lands in the room BEYOND, because a door is a line and
        // both squares beside it are floor. it is difficult ground, and difficult ground is not a
        // wall: the way through is open either way, and one way costs more to use
        [Fact]
        public void FailingGetsThroughAnyway_AndLeavesAMess()
        {
            DoorOutcome outcome = DoorCheck.Read(Throw(4, 4, 3));

            Assert.False(outcome.Forced);

            Assert.Equal(Tile.Rough, outcome.Leaves);
            Assert.True(outcome.Leaves.IsPassable());
            Assert.Equal(2, outcome.Leaves.MoveCost());
        }

        // the door comes back at you, and how hard is the die already lying on the felt - never a
        // second, invisible roll
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

        // ---- what it costs the hero ----

        // the consequence turns up on the FELT next time: a Winded hero throws a smaller Might
        // die, which is CORE_RULES 9 doing the work rather than this file inventing a penalty
        [Fact]
        public void EnoughOfACostStepsTheHerosDiceDown()
        {
            Actor hero = Hero();
            Die before = DoorCheck.PoolFor(hero).Dice[0].Die;

            // straight past the two-thirds mark, which is where Winded lands
            hero.Damage(hero.MaxVigor);

            Assert.True(hero.HasCondition(Condition.Winded));
            Assert.True(DoorCheck.PoolFor(hero).Dice[0].Die < before);
        }
    }
}
