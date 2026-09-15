using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Classes;
using Content.Saves;
using Content.Sheet;
using Core.Characters;
using Core.Combat;
using Core.Localization;
using Game.Room;
using Game.Sheet;

namespace Game.Tests
{
    // Phase R's Godot-free half: what a blank on the sheet offers, what the room's objects replace,
    // and what a box on the shelf is. The nodes that draw any of it need an engine and are the eye
    // check's; these are the decisions underneath them.
    public class RoomTests
    {
        static Filling Offers() => new Filling(
            new[] { Card("warden"), Card("archer") },
            new[]
            {
                new TraitCard("hearthguard.hillfolk", Blank.Race),
                new TraitCard("hearthguard.fenborn", Blank.Race),
                new TraitCard("hearthguard.gate_watch", Blank.Background),
            });

        static ClassCard Card(string id) =>
            new ClassCard("hearthguard." + id, 20, 11,
                          new Dictionary<Attr, Core.Dice.Die> { [Attr.Might] = Core.Dice.Die.D8 },
                          null);


        // ---- filling in the sheet ----------------------------------------------------------

        [Fact]
        public void A_blank_cycles_through_what_is_installed_when_you_touch_it()
        {
            Filling offers = Offers();
            var sheet = new CharacterSheet();

            Assert.Equal("hearthguard.fenborn", offers.Next(sheet, Line.Race));
            Assert.Equal("hearthguard.hillfolk", offers.Next(sheet, Line.Race));

            // and past the end it comes back to blank, so nothing is ever unpickable
            Assert.Equal("", offers.Next(sheet, Line.Race));
            Assert.Equal("hearthguard.fenborn", offers.Next(sheet, Line.Race));
        }

        // a sheet with no class is not a character, so that one line never cycles back through blank
        [Fact]
        public void The_class_line_never_comes_back_round_to_empty()
        {
            Filling offers = Offers();
            var sheet = new CharacterSheet();

            for (int touch = 0; touch < 10; touch++)
            {
                offers.Next(sheet, Line.Class);

                Assert.NotEqual("", sheet.ClassId);
            }
        }

        [Fact]
        public void Touching_backwards_walks_the_same_list_the_other_way()
        {
            Filling offers = Offers();
            var sheet = new CharacterSheet();

            offers.Next(sheet, Line.Race);
            offers.Next(sheet, Line.Race);

            Assert.Equal("hearthguard.fenborn", offers.Back(sheet, Line.Race));
        }

        [Fact]
        public void The_offers_are_sorted_so_a_dropdown_does_not_reorder_itself_between_machines()
        {
            Assert.Equal(new[] { "hearthguard.fenborn", "hearthguard.hillfolk" },
                         Offers().Offers(Line.Race));
        }

        [Fact]
        public void Only_the_lines_with_lists_behind_them_are_dropdowns()
        {
            Assert.True(Line.Class.IsADropdown());
            Assert.True(Line.Race.IsADropdown());
            Assert.True(Line.Background.IsADropdown());

            // written in by hand, because a real sheet has a blank there and not a list
            Assert.False(Line.Name.IsADropdown());
            Assert.False(Line.Appearance.IsADropdown());
        }

        [Fact]
        public void A_sheet_is_finished_when_it_names_a_class_that_is_installed()
        {
            Filling offers = Offers();
            var sheet = new CharacterSheet();

            Assert.False(offers.Finished(sheet));

            sheet.ClassId = "hearthguard.warden";

            Assert.True(offers.Finished(sheet));
        }

        // a pack was unsubscribed and the sheet still says what it said; named, never silently blanked
        [Fact]
        public void A_sheet_naming_something_that_is_no_longer_installed_says_which()
        {
            Filling offers = Offers();

            var sheet = new CharacterSheet
            {
                ClassId = "hearthguard.warden",
                RaceId = "somebody_elses.moonkin",
            };

            Assert.False(offers.Finished(sheet));
            Assert.Equal(new[] { "somebody_elses.moonkin" }, offers.Missing(sheet));
        }

        [Fact]
        public void A_line_with_nothing_installed_stays_blank_rather_than_throwing()
        {
            var nothing = new Filling(null, null);
            var sheet = new CharacterSheet();

            Assert.Equal("", nothing.Next(sheet, Line.Race));
            Assert.False(nothing.CanFill(Line.Class));
            Assert.Empty(nothing.Dropdowns);
        }


        // ---- the words on the paper ---------------------------------------------------------

        [Fact]
        public void Every_key_the_sheet_and_the_room_emit_obeys_the_grammar()
        {
            Assert.NotEmpty(SheetKeys.All());

            foreach (string key in SheetKeys.All())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));
        }

        [Fact]
        public void Every_line_of_the_sheet_has_a_label_and_every_object_a_name()
        {
            var keys = SheetKeys.All().ToList();

            foreach (Line line in Enum.GetValues<Line>())
                Assert.Contains(SheetKeys.Label(line), keys);

            foreach (Prop prop in Enum.GetValues<Prop>())
                Assert.Contains(Props.NameKey(prop), keys);
        }

        [Fact]
        public void The_two_lines_that_count_numbers_in_are_the_ones_that_say_so()
        {
            Assert.True(SheetKeys.TakesAnArgument(SheetKeys.Nerve));
            Assert.True(SheetKeys.TakesAnArgument(SheetKeys.Vigor));
            Assert.False(SheetKeys.TakesAnArgument(SheetKeys.Title));
            Assert.False(SheetKeys.TakesAnArgument(Props.NameKey(Prop.Door)));
        }

        [Fact]
        public void The_sheet_prints_as_many_nerve_pips_as_nerve_can_reach()
        {
            Assert.Equal(Nerve.Cap, Game.Sheet.Sheet.Pips);
        }


        // ---- the objects ----------------------------------------------------------------------

        // THE_TABLE.md section 6's table, one member per line, and each says what it deleted
        [Fact]
        public void Every_object_in_the_room_replaces_something_that_would_have_been_a_menu()
        {
            foreach (Prop prop in Enum.GetValues<Prop>())
                Assert.False(string.IsNullOrWhiteSpace(prop.Replaces()),
                             $"{prop} does not say what it replaces");
        }

        [Fact]
        public void The_door_is_the_only_object_that_asks_before_it_acts()
        {
            Assert.True(Prop.Door.Confirms());

            foreach (Prop prop in Enum.GetValues<Prop>().Where(p => p != Prop.Door))
                Assert.False(prop.Confirms(), $"{prop} asks, and nothing but the door is irreversible");
        }

        [Fact]
        public void A_props_word_round_trips_so_a_scene_can_name_one()
        {
            foreach (Prop prop in Enum.GetValues<Prop>())
            {
                Assert.True(Props.TryWord(prop.Word(), out Prop back));
                Assert.Equal(prop, back);
            }

            Assert.False(Props.TryWord("inventory_panel", out _));
        }

        // the rule the room check enforces, kept beside the list it enforces against
        [Fact]
        public void No_object_in_the_room_is_named_after_the_menu_it_replaces()
        {
            foreach (Prop prop in Enum.GetValues<Prop>())
                foreach (string forbidden in Game.Diagnostics.RoomCheck.Forbidden)
                    Assert.DoesNotContain(forbidden, prop.Word(), StringComparison.Ordinal);
        }


        // ---- the shelf ------------------------------------------------------------------------

        static string Shelf(params (string File, string Json)[] saves)
        {
            string folder = Path.Combine(Path.GetTempPath(), "shelf_" + Path.GetRandomFileName());

            Directory.CreateDirectory(folder);

            foreach ((string file, string json) in saves)
                File.WriteAllText(Path.Combine(folder, file), json);

            return folder;
        }

        const string Done = @"{ ""format"": 1, ""campaign"": ""greyhollow"",
            ""chapter"": ""the_hollow"", ""round"": 0,
            ""sheet"": { ""name"": ""Aeth"", ""class"": ""hearthguard.warden"" } }";

        const string MidFight = @"{ ""format"": 1, ""campaign"": ""ashfall"",
            ""encounter"": ""ash_yard"", ""round"": 3,
            ""sheet"": { ""name"": ""Bree"", ""class"": ""hearthguard.warden"" } }";

        [Fact]
        public void A_box_on_the_shelf_is_the_save_file_and_says_whose_game_it_is()
        {
            SaveShelf shelf = SaveShelf.Read(Shelf(("a.json", Done)));

            Assert.Empty(shelf.Problems);
            Assert.Equal(1, shelf.Count);

            Box box = shelf.Boxes[0];

            Assert.Equal("greyhollow", box.Campaign);
            Assert.Equal("the_hollow", box.Chapter);
            Assert.Equal("Aeth", box.Whose);
            Assert.True(box.Finished);
        }

        [Fact]
        public void A_box_with_a_fight_still_in_the_air_has_not_earned_its_trophy()
        {
            SaveShelf shelf = SaveShelf.Read(Shelf(("b.json", MidFight)));

            Assert.False(shelf.Boxes[0].Finished);
        }

        // the same isolation boundary a campaign folder gets: one bad file fails alone and named
        [Fact]
        public void One_broken_save_is_one_broken_box_and_not_a_shelf_that_will_not_open()
        {
            SaveShelf shelf = SaveShelf.Read(Shelf(
                ("good.json", Done),
                ("bad.json", "{ this is not json")));

            Assert.Equal(1, shelf.Count);
            Assert.NotEmpty(shelf.Problems);
            Assert.Contains(shelf.Problems, p => p.File == "bad.json");
        }

        [Fact]
        public void A_save_this_build_half_understands_still_goes_on_the_shelf_with_a_smudge()
        {
            SaveShelf shelf = SaveShelf.Read(Shelf(("later.json",
                @"{ ""format"": 1, ""campaign"": ""greyhollow"", ""weather"": ""rain"" }")));

            Assert.Equal(1, shelf.Count);
            Assert.True(shelf.Boxes[0].Smudges > 0);
        }

        [Fact]
        public void A_box_whose_campaign_is_not_installed_cannot_be_taken_down_and_played()
        {
            Box box = SaveShelf.Read(Shelf(("a.json", Done))).Boxes[0];

            Assert.True(box.Playable(id => id == "greyhollow"));
            Assert.False(box.Playable(id => false));
        }

        [Fact]
        public void An_empty_room_has_an_empty_shelf_rather_than_a_problem()
        {
            SaveShelf shelf = SaveShelf.Read(null);

            Assert.Equal(0, shelf.Count);
            Assert.Empty(shelf.Problems);
        }
    }
}
