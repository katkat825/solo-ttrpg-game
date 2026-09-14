using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Items;
using Content.Monsters;
using Content.Schema;
using Core.Characters;
using Core.Dice;
using Core.Resolution;
using Xunit;

namespace Content.Tests
{
    // gear and what it was carrying (CONTENT_PIPELINE.md P1)
    //
    // "Getting better means bigger rocks" (CORE_RULES.md pillar 3) becomes a content statement
    // here, and the two things worth pinning are the two that would be silent if they were wrong:
    // that a bigger die in a file really is a bigger die on the felt, and that a seeded run drops
    // the same thing twice.
    public class ItemTests
    {
        const string File = "axe.json";

        static Read<Gear> Parse(string json) => ItemReader.Parse(json, File);

        // ---- a piece of gear ----

        [Fact]
        public void AWeaponIsADieAndTheChecksItIsFor()
        {
            Read<Gear> read = Parse(@"{ ""id"": ""cold_iron_axe"", ""die"": ""d8"",
                                        ""supports"": ""blades"", ""inflicts"": [ ""reeling"" ] }");

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            Gear axe = read.Value;

            Assert.Equal("cold_iron_axe", axe.Id);
            Assert.Equal(Die.D8, axe.Die);
            Assert.Equal(Skill.Blades, axe.Supports);
            Assert.Equal(new[] { Condition.Reeling }, axe.Inflicts);
            Assert.Equal("gear.cold_iron_axe.name", axe.NameKey);
        }

        // ONE SCHEMA FOR EVERY KIND OF GEAR. A thing with a Defense number is armour, and nobody
        // had to declare a category for it to be one
        [Fact]
        public void AndArmourIsANumberOnDefense()
        {
            Read<Gear> read = Parse(@"{ ""id"": ""scale_coat"", ""defense"": 1 }");

            Assert.True(read.Ok);
            Assert.Equal(Die.None, read.Value.Die);
            Assert.Equal(1, read.Value.Defense);
        }

        [Fact]
        public void AndAThingWithBothIsAShield()
        {
            Read<Gear> read = Parse(@"{ ""id"": ""buckler"", ""die"": ""d4"", ""defense"": 1 }");

            Assert.True(read.Ok);
            Assert.Equal(Die.D4, read.Value.Die);
            Assert.Equal(1, read.Value.Defense);
        }

        // gear with no skill named is for any check - a lantern, a rope, and everything the engine
        // ships, which is why nothing in the built-in roster moved when Supports was read
        [Fact]
        public void GearThatNamesNoSkillIsForEveryCheck()
        {
            Gear rope = Parse(@"{ ""id"": ""rope"", ""die"": ""d6"" }").Value;

            Assert.Equal(Skill.None, rope.Supports);
            Assert.True(rope.Helps(Skill.Stealth));
            Assert.True(rope.Helps(Skill.None));
        }

        [Fact]
        public void AndGearThatNamesOneSitsOutTheRest()
        {
            Gear axe = Parse(@"{ ""id"": ""axe"", ""die"": ""d6"", ""supports"": ""blades"" }").Value;

            Assert.True(axe.Helps(Skill.Blades));
            Assert.False(axe.Helps(Skill.Stealth));
        }

        // ---- and what it does to an actor ----

        // PILLAR 3, MADE MECHANICAL: a bigger die in a file is a bigger die on the felt, and that
        // is the whole of what a better weapon is
        [Fact]
        public void ABiggerDieInAFileIsABiggerDieInThePool()
        {
            Actor hero = new BuiltInArchetypes().Create(EngineIds.Barbarian);

            Assert.Equal(Die.D6, hero.BuildPool(Attr.Might, Skill.Blades).Dice[2].Die);

            hero.Wielding(Parse(@"{ ""id"": ""great_axe"", ""die"": ""d10"" }").Value);

            Pool better = hero.BuildPool(Attr.Might, Skill.Blades);

            Assert.Equal(3, better.Count);
            Assert.Equal(Die.D10, better.Dice[2].Die);
            Assert.Equal("gear.great_axe.name", better.Dice[2].LabelKey);
        }

        [Fact]
        public void AndAWeaponForTheWrongCheckIsNotInThePoolAtAll()
        {
            Actor hero = new BuiltInArchetypes().Create(EngineIds.Barbarian);

            hero.Wielding(Parse(@"{ ""id"": ""axe"", ""die"": ""d6"", ""supports"": ""blades"" }").Value);

            // Might + Blades + the axe, against Might + Brawl and no axe. The Barbarian is trained
            // in both, so the only thing that moved is whether the weapon is for the check
            Assert.Equal(3, hero.BuildPool(Attr.Might, Skill.Blades).Count);
            Assert.Equal(2, hero.BuildPool(Attr.Might, Skill.Brawl).Count);
        }

        // Defense is the scalpel (SIMULATION.md section 3), so armour is one number and taking it
        // off gives the statblock's own back exactly
        [Fact]
        public void ArmourMovesDefense_AndTakingItOffGivesItBack()
        {
            Actor hero = new BuiltInArchetypes().Create(EngineIds.Barbarian);
            int bare = hero.Defense;

            hero.Wearing(Parse(@"{ ""id"": ""scale_coat"", ""defense"": 2 }").Value);

            Assert.Equal(bare + 2, hero.Defense);
            Assert.Equal(bare, hero.BaseDefense);

            hero.Wearing(null);

            Assert.Equal(bare, hero.Defense);
        }

        // ---- every way an item can be wrong ----

        [Fact]
        public void AnItemThatDoesNothingIsRefused()
        {
            Read<Gear> read = Parse(@"{ ""id"": ""pebble"" }");

            Assert.False(read.Ok);
            Assert.Contains("changes nothing", read.Problems.Single().What);
        }

        // ARMOUR IS THE STRONGEST THING A CAMPAIGN CAN HAND OUT and a schema that let somebody
        // turn the game off silently would be a schema that helped them do it
        [Fact]
        public void ArmourBeyondWhatDefenseCanTakeIsRefused()
        {
            Read<Gear> read = Parse(@"{ ""id"": ""godplate"", ""defense"": 9 }");

            Assert.False(read.Ok);
            Assert.Equal("defense", read.Problems.Single().Where);
            Assert.Contains(ItemReader.MostArmourCanDo.ToString(), read.Problems.Single().What);
        }

        [Fact]
        public void AConditionNobodyHasIsRefused()
        {
            Read<Gear> read = Parse(@"{ ""id"": ""axe"", ""die"": ""d6"", ""inflicts"": [ ""cursed"" ] }");

            Assert.False(read.Ok);
            Assert.Equal("inflicts[0]", read.Problems.Single().Where);
            Assert.Contains("winded", read.Problems.Single().What);
        }

        [Fact]
        public void ASkillNobodyHasIsRefused()
        {
            Read<Gear> read = Parse(@"{ ""id"": ""axe"", ""die"": ""d6"", ""supports"": ""axery"" }");

            Assert.False(read.Ok);
            Assert.Equal("supports", read.Problems.Single().Where);
        }

        [Fact]
        public void AFieldNobodyKnowsIsRefused()
        {
            Read<Gear> read = Parse(@"{ ""id"": ""axe"", ""die"": ""d6"", ""damage"": 4 }");

            Assert.False(read.Ok);
            Assert.Equal("damage", read.Problems.Single().Where);
        }

        // ---- what it was carrying ----

        static LootTable Loot(string json)
        {
            var problems = new List<ContentProblem>();

            using JsonDocument document = JsonDocument.Parse(json);

            LootTable table = LootTable.Parse(document.RootElement, File, "loot", problems);

            Assert.Empty(problems);

            return table;
        }

        [Fact]
        public void ALootTableIsAWeightedList()
        {
            LootTable table = Loot(@"[ { ""item"": ""axe"", ""weight"": 1 },
                                       { ""item"": ""coat"", ""weight"": 3 },
                                       { ""weight"": 6 } ]");

            Assert.Equal(10, table.Total);
            Assert.Equal(0.1, table.ChanceOf("axe"), 3);
            Assert.Equal(0.3, table.ChanceOf("coat"), 3);
        }

        // an entry with no item is "nothing this time", which is how a table says one-in-ten
        // without a second concept for drop chance
        [Fact]
        public void AnEntryWithNoItemIsNothingThisTime()
        {
            LootTable table = Loot(@"[ { ""item"": ""axe"", ""weight"": 1 }, { ""weight"": 9 } ]");

            var drawn = new List<string>();

            for (int seed = 0; seed < 40; seed++) drawn.Add(table.Draw(new SeededRng(seed)));

            Assert.Contains(null, drawn);
            Assert.Contains("axe", drawn);
        }

        // THE WHOLE POINT: a seeded run drops the same thing. Every source of chance in this game
        // goes through IRng, and a drop rolled off anything else would be the first thing to break
        // "seeded runs are reproducible" (CONVENTIONS.md 6)
        [Fact]
        public void AReseededRunDropsTheSameThing()
        {
            LootTable table = Loot(@"[ { ""item"": ""axe"", ""weight"": 1 },
                                       { ""item"": ""coat"", ""weight"": 1 },
                                       { ""item"": ""ring"", ""weight"": 1 },
                                       { ""weight"": 1 } ]");

            string[] first = Draws(table, 4242);
            string[] again = Draws(table, 4242);
            string[] other = Draws(table, 99);

            Assert.Equal(first, again);
            Assert.NotEqual(first, other);
        }

        static string[] Draws(LootTable table, int seed)
        {
            var rng = new SeededRng(seed);

            return Enumerable.Range(0, 20).Select(_ => table.Draw(rng)).ToArray();
        }

        [Fact]
        public void EveryWeightIsDrawable()
        {
            LootTable table = Loot(@"[ { ""item"": ""a"", ""weight"": 1 },
                                       { ""item"": ""b"", ""weight"": 1 },
                                       { ""item"": ""c"", ""weight"": 1 } ]");

            var seen = new HashSet<string>();
            var rng = new SeededRng(7);

            for (int i = 0; i < 200; i++) seen.Add(table.Draw(rng));

            Assert.Equal(new[] { "a", "b", "c" }, seen.OrderBy(s => s));
        }

        [Fact]
        public void NoLootDrawsNothing()
        {
            Assert.Null(LootTable.Nothing.Draw(new SeededRng(1)));
            Assert.True(LootTable.Nothing.IsEmpty);
        }

        [Fact]
        public void AWeightOfZeroIsRefused()
        {
            var problems = new List<ContentProblem>();

            using JsonDocument document = JsonDocument.Parse(@"[ { ""item"": ""axe"", ""weight"": 0 } ]");

            LootTable.Parse(document.RootElement, File, "loot", problems);

            Assert.Equal("loot[0].weight", problems.Single().Where);
        }

        // and a monster carries one, read out of its own file
        [Fact]
        public void AMonsterCarriesItsOwnTable()
        {
            Read<Statblock> read = StatblockReader.Parse(@"{
                ""id"": ""ghoul"", ""vigor"": 8, ""defense"": 11,
                ""loot"": [ { ""item"": ""claw_necklace"", ""weight"": 1 }, { ""weight"": 4 } ]
            }", "ghoul.json", "ashfall");

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));
            Assert.Equal(0.2, read.Value.Loot.ChanceOf("claw_necklace"), 3);
        }

        // ---- a folder of items ----

        [Fact]
        public void TwoCataloguesFoldTogether_AndASharedIdIsNamed()
        {
            var first = new ItemCatalogue();
            var second = new ItemCatalogue();

            Assert.Empty(first.Ids);
            Assert.Empty(first.Absorb(second));
            Assert.Empty(first.Absorb(null));
        }
    }
}
