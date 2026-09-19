using System;
using System.Linq;
using Content.Kits;
using Content.Sheet;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

namespace Content.Tests
{
    // THE CHECKS YOU REACH FOR YOURSELF (Phase T).
    //
    // A verb the moment offers is the author's - it arrives on a card beside the thing it is about.
    // These are the other half: the things you decide to try, that nobody offered you, and they
    // live on the character sheet because the sheet is the one object at the table that is yours.
    //
    // The load-bearing claim is that NONE OF THEM IS A NEW RULE. Each is the check primitive the
    // kit already runs, which is what makes "the guard can be intimidated at Tricky" a number in a
    // campaign file rather than engine work - and it is also why the difficulty is an argument and
    // never a field: the same intimidation is trivial in a tavern and formidable at a gate.
    public class CheckTests
    {
        [Fact]
        public void EveryOneOfThemIsTheCheckPrimitiveAndNothingNew()
        {
            foreach (Check check in Checks.All)
                Assert.Equal(Primitive.Check, check.For().Primitive);
        }

        [Fact]
        public void TheSceneSetsTheDifficulty()
        {
            Ability tavern = Check.Intimidate.For(Difficulty.Easy);
            Ability gate = Check.Intimidate.For(Difficulty.Formidable);

            Assert.Equal(Difficulty.Easy, tavern.Against);
            Assert.Equal(Difficulty.Formidable, gate.Against);

            // the same check, twice, differing only in the number the scene allowed
            Assert.Equal(tavern.Attribute, gate.Attribute);
            Assert.Equal(tavern.Skill, gate.Skill);
            Assert.Equal(tavern.Id, gate.Id);
        }

        [Fact]
        public void LeftAloneItIsAStandardCheck()
        {
            Assert.Equal(Difficulty.Standard, Check.Persuade.For().Against);
        }

        // leaning on somebody is Might, winning them over is Heart, and both are trained as Sway
        [Fact]
        public void WhichAttributeCarriesItIsWhatTellsThemApart()
        {
            Assert.Equal(Attr.Might, Check.Intimidate.Attribute());
            Assert.Equal(Attr.Heart, Check.Persuade.Attribute());
            Assert.Equal(Attr.Wits, Check.Perception.Attribute());

            Assert.Equal(Skill.Sway, Check.Intimidate.Skill());
            Assert.Equal(Skill.Sway, Check.Persuade.Skill());
            Assert.Equal(Skill.Insight, Check.Perception.Skill());
        }

        // AN AXE DOES NOT HELP YOU READ A ROOM. Counting the weapon in would make the sheet's three
        // a question about your inventory
        [Fact]
        public void NoneOfThemCountsWhatYouAreCarrying()
        {
            foreach (Check check in Checks.All) Assert.False(check.For().UseGear);
        }

        [Fact]
        public void NoneOfThemCostsAnything()
        {
            foreach (Check check in Checks.All) Assert.Equal(Cost.None, check.For().Cost);
        }

        // the pool a check throws is the pool the character already has, so a better character is
        // better at it and nothing else is
        [Fact]
        public void ThePoolIsWhatIsWrittenOnTheSheet()
        {
            Actor hero = new Actor("warden", 20, 11)
                             .With(Attr.Heart, Die.D8)
                             .With(Skill.Sway, Die.D6);

            Ability persuade = Check.Persuade.For();

            Pool pool = hero.BuildPool(persuade.Attribute, persuade.Skill);

            Assert.Equal(2, pool.Count);
            Assert.Contains(pool.Dice, d => d.Die == Die.D8);
            Assert.Contains(pool.Dice, d => d.Die == Die.D6);
        }

        [Fact]
        public void EachOneHasAKeyAndTheyObeyTheGrammar()
        {
            foreach (Check check in Checks.All)
            {
                Assert.Equal(KeyConventions.AbilityName(check.Word()), check.NameKey());
                Assert.True(KeyConventions.IsWellFormed(check.NameKey()));
                Assert.True(KeyConventions.IsWellFormed(check.DescriptionKey()));
            }

            Assert.Equal(Checks.All.Count * 2, Checks.Keys().Count());
        }

        // NONE OF THEM IS IN ANYBODY'S KIT. A class kit is what the class gives you; these are on
        // the sheet because everybody has them, and putting them in the shared kit would hand
        // every hero three more abilities in the middle of a fight
        [Fact]
        public void TheyAreNotAbilitiesAClassHandsOut()
        {
            foreach (Check check in Checks.All)
                Assert.DoesNotContain(SharedKit.All, a => a.Id == check.Word());
        }

        [Fact]
        public void EveryWordReadsBackAsItsOwnCheck()
        {
            foreach (Check check in Checks.All)
            {
                Assert.True(Checks.TryWord(check.Word(), out Check read));
                Assert.Equal(check, read);
            }

            Assert.False(Checks.TryWord("pickpocket", out _));
            Assert.False(Checks.TryWord(null, out _));
        }

        [Fact]
        public void TheThreeAreDerivedFromTheEnum_NeverListed()
        {
            Assert.Equal(Enum.GetValues<Check>().Length, Checks.All.Count);
            Assert.Equal(Enum.GetValues<Check>().Length, Checks.Words.Count);
        }
    }
}
