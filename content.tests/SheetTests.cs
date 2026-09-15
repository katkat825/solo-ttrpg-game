using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Classes;
using Content.Items;
using Content.Saves;
using Content.Schema;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;

namespace Content.Tests
{
    // R0. The sheet IS the character - not a view of one - so the test of it is that changing what
    // is written changes the dice that get thrown, and that nothing else anywhere holds a copy.
    public class SheetTests
    {
        static ClassRoster Classes()
        {
            string folder = Path.Combine(Path.GetTempPath(), "sheet_" + Path.GetRandomFileName());

            Directory.CreateDirectory(Path.Combine(folder, "classes"));

            File.WriteAllText(Path.Combine(folder, "classes", "warden.json"), @"{
                ""id"": ""warden"",
                ""vigor"": 20,
                ""defense"": 11,
                ""attributes"": { ""might"": ""d8"", ""grace"": ""d6"", ""wits"": ""d6"", ""heart"": ""d6"" },
                ""skills"": { ""blades"": ""d6"" },
                ""companion"": ""wolf"",
                ""growth"": [ { ""id"": ""shield_wall"", ""attribute"": ""might"" } ]
            }");

            ClassRoster roster = ClassRoster.Read(Path.Combine(folder, "classes"), "hearthguard");

            Assert.Empty(roster.Problems);

            return roster;
        }

        static SheetOptions Options()
        {
            string folder = Path.Combine(Path.GetTempPath(), "opts_" + Path.GetRandomFileName());

            Directory.CreateDirectory(Path.Combine(folder, "races"));
            Directory.CreateDirectory(Path.Combine(folder, "backgrounds"));

            File.WriteAllText(Path.Combine(folder, "races", "hillfolk.json"),
                              @"{ ""id"": ""hillfolk"", ""nudges"": { ""might"": 1, ""grace"": -1 } }");

            File.WriteAllText(Path.Combine(folder, "backgrounds", "gate_watch.json"),
                              @"{ ""id"": ""gate_watch"", ""skills"": { ""insight"": ""d4"" } }");

            SheetOptions options = SheetOptions.Read(folder, "hearthguard");

            Assert.Empty(options.Problems);

            return options;
        }

        static CharacterSheet Filled() => new CharacterSheet
        {
            Name = "Aeth",
            ClassId = "hearthguard.warden",
            RaceId = "hearthguard.hillfolk",
            BackgroundId = "hearthguard.gate_watch",
        };


        [Fact]
        public void The_hero_is_assembled_from_what_is_written_on_the_sheet()
        {
            Actor hero = Filled().Assemble(Classes(), null, Options());

            Assert.Equal("hearthguard.warden", hero.Id);
            Assert.Equal(20, hero.MaxVigor);

            // d8 from the class, one step up from hillfolk
            Assert.Equal(Die.D10, hero.Attribute(Attr.Might));

            // d6 from the class, one step down from hillfolk
            Assert.Equal(Die.D4, hero.Attribute(Attr.Grace));

            // untrained by the class, trained by the background
            Assert.Equal(Die.D4, hero.SkillDie(Skill.Insight));
        }

        // R0's verify list: change a starting attribute on the sheet and the dice change
        [Fact]
        public void Changing_the_race_on_the_sheet_changes_the_pool_thrown_on_the_tray()
        {
            ClassRoster classes = Classes();
            SheetOptions options = Options();

            CharacterSheet sheet = Filled();

            Core.Resolution.Pool with = sheet.Assemble(classes, null, options)
                                             .BuildPool(Attr.Might, Skill.Blades);

            sheet.RaceId = "";

            Core.Resolution.Pool without = sheet.Assemble(classes, null, options)
                                                .BuildPool(Attr.Might, Skill.Blades);

            Assert.Equal(Die.D10, with.Dice[0].Die);
            Assert.Equal(Die.D8, without.Dice[0].Die);
        }

        [Fact]
        public void A_nudge_is_steps_so_two_cards_and_a_condition_land_in_any_order()
        {
            ClassRoster classes = Classes();
            SheetOptions options = Options();

            Actor first = Filled().Assemble(classes, null, options);
            first.ApplyCondition(Condition.Reeling);

            Actor second = Filled().Assemble(classes, null, options);

            // the Condition applied before the race's nudge, by taking it off and putting it back
            TraitCard race = options.Of("hearthguard.hillfolk");
            race.TakeFrom(second);
            second.ApplyCondition(Condition.Reeling);
            race.ApplyTo(second);

            Assert.Equal(first.Attribute(Attr.Might), second.Attribute(Attr.Might));
            Assert.Equal(first.Attribute(Attr.Grace), second.Attribute(Attr.Grace));
        }

        [Fact]
        public void A_background_never_untrains_a_skill_the_class_already_had()
        {
            string folder = Path.Combine(Path.GetTempPath(), "opts_" + Path.GetRandomFileName());

            Directory.CreateDirectory(Path.Combine(folder, "backgrounds"));

            File.WriteAllText(Path.Combine(folder, "backgrounds", "raw.json"),
                              @"{ ""id"": ""raw"", ""skills"": { ""blades"": ""d4"" } }");

            SheetOptions options = SheetOptions.Read(folder, "hearthguard");

            var sheet = new CharacterSheet
            {
                ClassId = "hearthguard.warden",
                BackgroundId = "hearthguard.raw",
            };

            // the class trains Blades d6; a d4 background must not make it worse
            Assert.Equal(Die.D6, sheet.Assemble(Classes(), null, options).SkillDie(Skill.Blades));
        }

        [Fact]
        public void A_sheet_with_no_class_names_nobody_and_says_so()
        {
            var blank = new CharacterSheet();

            Assert.True(blank.IsBlank);
            Assert.Throws<System.InvalidOperationException>(() => blank.Assemble(Classes()));
        }

        [Fact]
        public void Growth_is_written_as_steps_and_never_as_the_dice_they_bought()
        {
            CharacterSheet sheet = Filled();

            Assert.True(sheet.Grew("shield_wall"));
            Assert.False(sheet.Grew("shield_wall"));

            Assert.Equal(new[] { "shield_wall" }, sheet.Growth);

            // d8 class, +1 hillfolk, +1 the step
            Assert.Equal(Die.D12, sheet.Assemble(Classes(), null, Options()).Attribute(Attr.Might));
        }


        // ---- what play writes on it --------------------------------------------------------

        [Fact]
        public void What_happened_in_play_is_written_back_onto_the_sheet()
        {
            ClassRoster classes = Classes();
            SheetOptions options = Options();

            CharacterSheet sheet = Filled();
            Actor hero = sheet.Assemble(classes, null, options);

            hero.Damage(6);
            hero.SpendNerve(2);
            hero.ApplyCondition(Condition.Winded);

            sheet.Wrote(hero);

            Assert.Equal(hero.Vigor, sheet.Vigor);
            Assert.Equal(hero.Nerve, sheet.Nerve);
            Assert.Equal(new[] { Condition.Winded }, sheet.Conditions);

            // and reading it back gives the same character, not the fresh one
            Actor again = sheet.Assemble(classes, null, options);

            Assert.Equal(hero.Vigor, again.Vigor);
            Assert.Equal(hero.Nerve, again.Nerve);
            Assert.Contains(Condition.Winded, again.Conditions);
            Assert.Equal(hero.Attribute(Attr.Might), again.Attribute(Attr.Might));
        }

        // R2: the record of play IS the artifact, so the paper gets visibly lived in
        [Fact]
        public void The_sheet_accumulates_erasures_as_things_are_rubbed_out_and_rewritten()
        {
            ClassRoster classes = Classes();

            CharacterSheet sheet = Filled();
            Actor hero = sheet.Assemble(classes);

            Assert.Equal(0, sheet.Erasures);

            // filling a blank is writing, not erasing: the sheet held no Vigor to rub out
            hero.Damage(3);
            sheet.Wrote(hero);

            Assert.Equal(0, sheet.Erasures);

            // the second time, there was a number there and it had to come off
            hero.Damage(3);
            sheet.Wrote(hero);

            int after = sheet.Erasures;

            Assert.True(after > 0);

            // and nothing changed this time, so nothing was rubbed out
            sheet.Wrote(hero);

            Assert.Equal(after, sheet.Erasures);
        }

        [Fact]
        public void A_camp_written_back_puts_the_nerve_pips_back_up()
        {
            ClassRoster classes = Classes();

            CharacterSheet sheet = Filled();
            Actor hero = sheet.Assemble(classes);

            hero.SpendNerve(3);
            sheet.Wrote(hero);

            Assert.Equal(0, sheet.Nerve);

            Rest.Camp(hero);
            sheet.Wrote(hero);

            Assert.Equal(Nerve.StartOfDay, sheet.Nerve);
        }


        // ---- the file ----------------------------------------------------------------------

        [Fact]
        public void A_sheet_round_trips_through_a_save()
        {
            CharacterSheet sheet = Filled();

            sheet.Grew("shield_wall");
            sheet.Conditions.Add(Condition.Winded);
            sheet.Satchel.Add("damp_charm");
            sheet.Vigor = 14;
            sheet.Nerve = 1;
            sheet.Erasures = 7;

            var save = new SaveGame { Campaign = "greyhollow", Sheet = sheet };

            Read<SaveGame> read = SaveReader.Parse(SaveWriter.Write(save), "save.json");

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));

            CharacterSheet back = read.Value.Sheet;

            Assert.Equal("Aeth", back.Name);
            Assert.Equal("hearthguard.warden", back.ClassId);
            Assert.Equal("hearthguard.hillfolk", back.RaceId);
            Assert.Equal("hearthguard.gate_watch", back.BackgroundId);
            Assert.Equal(new[] { "shield_wall" }, back.Growth);
            Assert.Equal(new[] { Condition.Winded }, back.Conditions);
            Assert.Equal(new[] { "damp_charm" }, back.Satchel);
            Assert.Equal(14, back.Vigor);
            Assert.Equal(1, back.Nerve);
            Assert.Equal(7, back.Erasures);
        }

        // P6's rule, still true with a new field in the file
        [Fact]
        public void A_save_written_before_there_were_sheets_still_reads()
        {
            const string old = @"{
                ""format"": 1,
                ""campaign"": ""greyhollow"",
                ""encounter"": ""the_stair"",
                ""round"": 2,
                ""hero"": { ""id"": ""barbarian"", ""vigor"": 9, ""nerve"": 2 }
            }";

            Read<SaveGame> read = SaveReader.Parse(old, "old.json");

            Assert.True(read.Ok, string.Join("; ", read.Problems.Select(p => p.ToString())));
            Assert.Null(read.Value.Sheet);
            Assert.False(read.Value.HasASheet);
            Assert.Equal("barbarian", read.Value.Hero.Id);
        }

        [Fact]
        public void A_sheet_in_a_save_that_names_no_class_is_refused_and_named()
        {
            const string broken = @"{
                ""format"": 1,
                ""campaign"": ""greyhollow"",
                ""sheet"": { ""name"": ""Aeth"", ""race"": ""hearthguard.hillfolk"" }
            }";

            Read<SaveGame> read = SaveReader.Parse(broken, "broken.json");

            Assert.Null(read.Value.Sheet);
            Assert.Contains(read.Problems, p => p.Where == "sheet.class");
        }


        // ---- the cards ---------------------------------------------------------------------

        [Fact]
        public void Every_key_a_sheet_option_emits_obeys_the_grammar()
        {
            foreach (string key in Options().Keys())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));

            Assert.Equal("class.hearthguard.hillfolk.name",
                         Options().Of("hearthguard.hillfolk").NameKey);
        }

        [Fact]
        public void A_nudge_bigger_than_a_nudge_is_refused()
        {
            Read<TraitCard> read = SheetReader.Parse(
                @"{ ""id"": ""titan"", ""nudges"": { ""might"": 4 } }", "titan.json",
                "hearthguard", Blank.Race);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("is a class"));
        }

        [Fact]
        public void A_nudge_of_nothing_is_refused_rather_than_ignored()
        {
            Read<TraitCard> read = SheetReader.Parse(
                @"{ ""id"": ""plain"", ""nudges"": { ""might"": 0 } }", "plain.json",
                "hearthguard", Blank.Race);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("does nothing"));
        }

        [Fact]
        public void A_card_with_vigor_on_it_is_refused_because_that_is_a_class()
        {
            Read<TraitCard> read = SheetReader.Parse(
                @"{ ""id"": ""tough"", ""vigor"": 25 }", "tough.json", "hearthguard", Blank.Race);

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.Where == "vigor");
        }

        [Fact]
        public void A_race_and_a_background_with_the_same_id_would_share_a_key_and_are_refused()
        {
            string folder = Path.Combine(Path.GetTempPath(), "clash_" + Path.GetRandomFileName());

            Directory.CreateDirectory(Path.Combine(folder, "races"));
            Directory.CreateDirectory(Path.Combine(folder, "backgrounds"));

            File.WriteAllText(Path.Combine(folder, "races", "fen.json"),
                              @"{ ""id"": ""fen"", ""nudges"": { ""wits"": 1 } }");
            File.WriteAllText(Path.Combine(folder, "backgrounds", "fen.json"),
                              @"{ ""id"": ""fen"", ""nudges"": { ""heart"": 1 } }");

            SheetOptions options = SheetOptions.Read(folder, "hearthguard");

            Assert.Contains(options.Problems, p => p.What.Contains("never be seen"));
        }

        [Fact]
        public void A_race_sharing_an_id_with_a_class_is_refused_at_the_pack_level()
        {
            string folder = Path.Combine(Path.GetTempPath(), "clash2_" + Path.GetRandomFileName());

            Directory.CreateDirectory(Path.Combine(folder, "races"));

            File.WriteAllText(Path.Combine(folder, "races", "warden.json"),
                              @"{ ""id"": ""warden"", ""nudges"": { ""wits"": 1 } }");

            SheetOptions options = SheetOptions.Read(folder, "hearthguard");
            var problems = new List<ContentProblem>();

            options.MustNotCollideWith(Classes(), problems);

            Assert.Contains(problems, p => p.What.Contains("both a race and a class"));
        }

        [Fact]
        public void A_pack_with_no_sheet_folder_offers_nothing_and_has_no_problems()
        {
            SheetOptions options = SheetOptions.Read(null, "hearthguard");

            Assert.Empty(options.Problems);
            Assert.Equal(0, options.Count);
            Assert.Empty(options.Keys());
        }
    }
}
