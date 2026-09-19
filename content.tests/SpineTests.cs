using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Companions;
using Content.Dialogue;
using Content.Schema;
using Core.Localization;

namespace Content.Tests
{
    // W4 and W5. The spine is one list of intents and a rule that no voice may be missing one; the
    // hint ladder is three rungs off that list and a fourth answer that is not a rung.
    public class SpineTests
    {
        static string Folder()
        {
            string path = Path.Combine(Path.GetTempPath(), "spine_" + Path.GetRandomFileName());

            Directory.CreateDirectory(path);

            return path;
        }

        static string Write(string folder, string name, string text)
        {
            string path = Path.Combine(folder, name);

            File.WriteAllText(path, text);

            return path;
        }


        [Fact]
        public void A_beat_is_keyed_once_per_voice_which_is_the_whole_idea()
        {
            string folder = Folder();

            Write(folder, "spine.json", @"{ ""beats"": [
                { ""id"": ""warn_bridge_trapped"", ""kind"": ""plot"", ""note"": ""tell them"" },
                { ""id"": ""the_goose_incident"", ""kind"": ""colour"" }
            ] }");

            BeatBook book = BeatBook.Read(folder, "greyhollow");

            Assert.Empty(book.Problems);
            Assert.Equal(2, book.Count);

            var keys = book.KeysFor(new[] { "wolf", "imp" }).ToList();

            // two beats, each with one phrasing per voice and the DM's floor beneath them
            Assert.Equal(6, keys.Count);
            Assert.Contains("dialogue.wolf.beat.greyhollow.warn_bridge_trapped", keys);
            Assert.Contains("dialogue.imp.beat.greyhollow.warn_bridge_trapped", keys);
            Assert.Contains("dialogue.dm.beat.greyhollow.warn_bridge_trapped", keys);

            foreach (string key in keys)
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));
        }

        // W5's rule is that no companion may WITHHOLD a beat the player needs - and the reading
        // that every voice must phrase every beat cannot survive a storefront, because a campaign
        // published in March cannot write lines for a class pack published in June. The DM is the
        // floor: always at the table, never a companion, and already narrating.
        [Fact]
        public void A_beat_the_dm_can_say_reaches_every_player_however_many_voices_turn_up()
        {
            string folder = Folder();

            Write(folder, "spine.json", @"{ ""beats"": [ { ""id"": ""warn"", ""kind"": ""plot"" } ] }");

            BeatBook book = BeatBook.Read(folder, "greyhollow");
            Beat warn = book.Of("warn");

            var written = new HashSet<string>(new[] { warn.DmKey("greyhollow") });

            Assert.True(book.Reaches(warn, new[] { "wolf", "imp", "raven" }, written.Contains));
        }

        [Fact]
        public void A_beat_every_voice_phrases_reaches_every_player_without_the_dm()
        {
            string folder = Folder();

            Write(folder, "spine.json", @"{ ""beats"": [ { ""id"": ""warn"", ""kind"": ""plot"" } ] }");

            BeatBook book = BeatBook.Read(folder, "greyhollow");
            Beat warn = book.Of("warn");

            var voices = new[] { "wolf", "imp" };

            var written = new HashSet<string>(
                voices.Select(v => warn.KeyFor(v, "greyhollow")));

            Assert.True(book.Reaches(warn, voices, written.Contains));
            Assert.Empty(book.Silent(warn, voices, written.Contains));
        }

        [Fact]
        public void A_beat_one_voice_is_missing_and_the_dm_cannot_say_reaches_nobody_who_brought_it()
        {
            string folder = Folder();

            Write(folder, "spine.json", @"{ ""beats"": [ { ""id"": ""warn"", ""kind"": ""plot"" } ] }");

            BeatBook book = BeatBook.Read(folder, "greyhollow");
            Beat warn = book.Of("warn");

            var written = new HashSet<string>(new[] { warn.KeyFor("wolf", "greyhollow") });

            Assert.False(book.Reaches(warn, new[] { "wolf", "imp" }, written.Contains));
            Assert.Equal(new[] { "imp" }, book.Silent(warn, new[] { "wolf", "imp" }, written.Contains));
        }

        // still worth writing: the DM saying it is the floor, not the point
        [Fact]
        public void A_beat_the_dm_covers_still_names_who_will_hear_it_from_the_dm()
        {
            string folder = Folder();

            Write(folder, "spine.json", @"{ ""beats"": [ { ""id"": ""warn"" } ] }");

            BeatBook book = BeatBook.Read(folder, "greyhollow");
            Beat warn = book.Of("warn");

            var written = new HashSet<string>(new[]
            {
                warn.DmKey("greyhollow"),
                warn.KeyFor("wolf", "greyhollow"),
            });

            Assert.True(book.Reaches(warn, new[] { "wolf", "imp" }, written.Contains));
            Assert.Equal(new[] { "imp" }, book.Silent(warn, new[] { "wolf", "imp" }, written.Contains));
        }

        [Fact]
        public void The_dm_is_not_a_companion_and_its_key_is_a_key_like_any_other()
        {
            var warn = new Beat("warn", BeatKind.Plot);

            Assert.Equal("dialogue.dm.beat.greyhollow.warn", warn.DmKey("greyhollow"));
            Assert.True(KeyConventions.IsWellFormed(warn.DmKey("greyhollow")));
        }

        [Fact]
        public void A_plot_beat_must_be_delivered_and_a_colour_one_need_not_be()
        {
            Assert.True(new Beat("a", BeatKind.Plot).MustBeDelivered);
            Assert.True(new Beat("a", BeatKind.Hint).MustBeDelivered);
            Assert.False(new Beat("a", BeatKind.Colour).MustBeDelivered);
        }

        [Fact]
        public void The_same_intent_written_twice_is_refused()
        {
            string folder = Folder();

            Write(folder, "spine.json",
                  @"{ ""beats"": [ { ""id"": ""warn"" }, { ""id"": ""warn"" } ] }");

            BeatBook book = BeatBook.Read(folder, "greyhollow");

            Assert.Contains(book.Problems, p => p.What.Contains("written\n once") ||
                                                p.What.Contains("already a beat"));
        }

        [Fact]
        public void A_kind_outside_the_vocabulary_is_refused()
        {
            string folder = Folder();

            Write(folder, "spine.json", @"{ ""beats"": [ { ""id"": ""warn"", ""kind"": ""urgent"" } ] }");

            BeatBook book = BeatBook.Read(folder, "greyhollow");

            Assert.Contains(book.Problems, p => p.What.Contains("urgent") && p.What.Contains("plot"));
        }


        [Fact]
        public void A_ladder_has_exactly_three_rungs_and_a_shorter_one_is_refused()
        {
            string folder = Folder();

            Write(folder, "hints.json",
                  @"{ ""hints"": [ { ""id"": ""the_door"", ""rungs"": [ ""a"", ""b"" ] } ] }");

            HintBook book = HintBook.Read(folder, "greyhollow");

            Assert.Contains(book.Problems, p => p.What.Contains("2 rungs"));
        }

        [Fact]
        public void A_ladder_names_its_rungs_in_order()
        {
            string folder = Folder();

            Write(folder, "hints.json", @"{ ""hints"": [ { ""id"": ""the_door"",
                ""rungs"": [ ""hinges_are_new"", ""somebody_replaced_it"", ""the_pins_lift_out"" ] } ] }");

            HintBook book = HintBook.Read(folder, "greyhollow");

            Assert.Empty(book.Problems);

            Hint hint = book.Of("the_door");

            Assert.Equal("hinges_are_new", hint.Beat(1));
            Assert.Equal("the_pins_lift_out", hint.Beat(3));
            Assert.Null(hint.Beat(4));
        }


        // pulled, not pushed: the player asks, and asking again escalates rather than repeating
        [Fact]
        public void Three_asks_escalate_and_the_fourth_is_the_companion_noticing()
        {
            var ladder = new HintLadder();

            Assert.Equal(1, ladder.Next("the_door").Rung);
            Assert.Equal(2, ladder.Next("the_door").Rung);
            Assert.Equal(3, ladder.Next("the_door").Rung);

            Ask again = ladder.Next("the_door");

            Assert.True(again.Overasked);
            Assert.False(again.Answers);
            Assert.True(ladder.Next("the_door").Overasked);
        }

        [Fact]
        public void The_ladder_counts_per_problem_so_a_new_room_starts_at_the_bottom()
        {
            var ladder = new HintLadder();

            ladder.Next("the_door");
            ladder.Next("the_door");

            Assert.Equal(1, ladder.Next("the_cistern").Rung);
            Assert.Equal(3, ladder.Next("the_door").Rung);
        }

        [Fact]
        public void Peeking_does_not_spend_the_ask()
        {
            var ladder = new HintLadder();

            Assert.Equal(1, ladder.Peek("the_door").Rung);
            Assert.Equal(1, ladder.Peek("the_door").Rung);
            Assert.Equal(1, ladder.Next("the_door").Rung);
            Assert.Equal(2, ladder.Peek("the_door").Rung);
        }

        [Fact]
        public void Solving_a_problem_forgets_the_asks_about_it()
        {
            var ladder = new HintLadder();

            ladder.Next("the_door");
            ladder.Next("the_door");
            ladder.Solved("the_door");

            Assert.Equal(1, ladder.Next("the_door").Rung);
        }

        [Fact]
        public void A_resumed_game_remembers_it_has_already_been_asked()
        {
            var ladder = new HintLadder();

            ladder.Restore("the_door", 2);

            Assert.Equal(3, ladder.Next("the_door").Rung);
        }


        [Fact]
        public void A_companion_sits_on_the_table_and_has_no_numbers_on_its_card()
        {
            Read<CompanionCard> read = CompanionReader.Parse(
                @"{ ""id"": ""wolf"", ""perch"": ""map_edge"", ""idles"": 12 }",
                "wolf.json", "hearthguard");

            Assert.True(read.Ok);
            Assert.Equal("hearthguard.wolf", read.Value.Id);
            Assert.Equal(Perch.MapEdge, read.Value.Perch);
            Assert.Equal("wolf", read.Value.Voice);
            Assert.Equal(12, read.Value.Idles);

            // the type itself is the guarantee: there is nowhere on it to put a statblock
            Assert.Null(typeof(CompanionCard).GetProperty("Vigor"));
            Assert.Null(typeof(CompanionCard).GetProperty("Defense"));
            Assert.Null(typeof(CompanionCard).GetProperty("Tier"));
        }

        [Fact]
        public void A_perch_outside_the_table_is_refused_and_the_five_are_offered()
        {
            Read<CompanionCard> read = CompanionReader.Parse(
                @"{ ""id"": ""wolf"", ""perch"": ""on_the_map"" }", "wolf.json", "hearthguard");

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("map_edge") && p.What.Contains("tray_rim"));
        }

        [Fact]
        public void A_companion_with_no_voice_named_speaks_under_its_own_id()
        {
            Read<CompanionCard> read = CompanionReader.Parse(
                @"{ ""id"": ""raven"", ""perch"": ""tray_rim"" }", "raven.json", "hearthguard");

            Assert.True(read.Ok);
            Assert.Equal("raven", read.Value.Voice);
            Assert.Equal("hearthguard.raven", read.Value.Id);
        }

        [Fact]
        public void A_field_a_companion_does_not_have_is_refused_rather_than_ignored()
        {
            Read<CompanionCard> read = CompanionReader.Parse(
                @"{ ""id"": ""wolf"", ""perch"": ""map_edge"", ""vigor"": 12 }",
                "wolf.json", "hearthguard");

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.Where == "vigor");
        }


        // A COMPANION'S SIZE IS PART OF ITS IDENTITY (Phase T). A wolf, a raven and a reliquary are
        // not the same creature at the same scale, and a raven rendered wolf-sized is a different
        // animal. It is still nothing a rule ever reads.
        [Fact]
        public void A_companion_that_says_nothing_about_its_size_is_wolf_sized()
        {
            Read<CompanionCard> read = CompanionReader.Parse(
                @"{ ""id"": ""wolf"", ""perch"": ""map_edge"" }", "wolf.json", "hearthguard");

            Assert.True(read.Ok);
            Assert.Equal(CompanionCard.NormalSize, read.Value.Size);
        }

        [Fact]
        public void A_raven_is_a_third_of_a_wolf_and_says_so_on_its_card()
        {
            Read<CompanionCard> read = CompanionReader.Parse(
                @"{ ""id"": ""raven"", ""perch"": ""tray_rim"", ""size"": 0.35 }",
                "raven.json", "hearthguard");

            Assert.True(read.Ok);
            Assert.Equal(0.35f, read.Value.Size, 4);
        }

        [Fact]
        public void A_size_nothing_could_be_is_refused_and_the_range_is_offered()
        {
            foreach (string absurd in new[] { "0", "-2", "40" })
            {
                Read<CompanionCard> read = CompanionReader.Parse(
                    $@"{{ ""id"": ""wolf"", ""perch"": ""map_edge"", ""size"": {absurd} }}",
                    "wolf.json", "hearthguard");

                Assert.False(read.Ok);
                Assert.Contains(read.Problems, p => p.Where == "size");
            }
        }

        // SIZE IS NOT A NUMBER A RULE READS. The type is still the guarantee it was: a companion
        // that is bigger is bigger, and there is nowhere on the card to say it is stronger
        [Fact]
        public void A_bigger_companion_is_not_a_stronger_one()
        {
            Read<CompanionCard> read = CompanionReader.Parse(
                @"{ ""id"": ""bear"", ""perch"": ""map_edge"", ""size"": 2.5 }",
                "bear.json", "hearthguard");

            Assert.True(read.Ok);
            Assert.Equal(2.5f, read.Value.Size, 4);

            Assert.Null(typeof(CompanionCard).GetProperty("Vigor"));
            Assert.Null(typeof(CompanionCard).GetProperty("Defense"));
            Assert.Null(typeof(CompanionCard).GetProperty("Tier"));
        }
    }
}
