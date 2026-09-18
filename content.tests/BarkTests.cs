using System.Collections.Generic;
using System.Linq;
using Content.Dialogue;
using Content.Schema;
using Core.Dice;
using Core.Localization;

namespace Content.Tests
{
    // W2. The bank is the highest-frequency piece of characterisation in the game, so the two
    // things that have to be true of it are that it is complete (every counted line has words) and
    // that it does not sound like a random number generator (a bank is dealt, not drawn).
    public class BarkTests
    {
        static BarkBank Bank(string json) => BarkReader.Parse(json, "wolf.json").Value;

        const string Wolf = @"{
            ""speaker"": ""wolf"",
            ""banks"": { ""snag"": 40, ""trouble"": 12, ""nerve"": 6 }
        }";


        [Fact]
        public void A_bank_is_keyed_by_creature_situation_and_a_three_digit_index()
        {
            Assert.Equal("dialogue.wolf.bark.snag.017", DialogueKeys.Bark("wolf", Bark.Snag, 17));
        }

        [Fact]
        public void Every_counted_line_gets_a_key_and_all_of_them_obey_the_grammar()
        {
            BarkBank bank = Bank(Wolf);

            Assert.Equal(58, bank.Lines);
            Assert.Equal(58, bank.Keys().Count());

            foreach (string key in bank.Keys())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));
        }

        [Fact]
        public void The_index_runs_from_one_because_that_is_how_a_bank_is_numbered_on_paper()
        {
            var keys = DialogueKeys.Barks("wolf", Bark.Trouble, 3).ToList();

            Assert.Equal(new[]
            {
                "dialogue.wolf.bark.trouble.001",
                "dialogue.wolf.bark.trouble.002",
                "dialogue.wolf.bark.trouble.003",
            }, keys);
        }

        [Fact]
        public void A_situation_the_engine_cannot_raise_is_refused_and_the_list_is_offered()
        {
            Read<BarkBank> read = BarkReader.Parse(
                @"{ ""speaker"": ""wolf"", ""banks"": { ""elevenses"": 4 } }", "wolf.json");

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("elevenses") && p.What.Contains("snag"));
        }

        [Fact]
        public void A_speaker_is_a_creature_and_a_class_name_is_still_read_but_it_is_the_author_who_must_not()
        {
            // the reader cannot tell 'barbarian' from 'wolf'; what it can refuse is a non-id
            Read<BarkBank> read = BarkReader.Parse(
                @"{ ""speaker"": ""Wolf Of The North"", ""banks"": { ""snag"": 4 } }", "wolf.json");

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.Where == "speaker");
        }

        [Fact]
        public void A_voice_with_no_banks_at_all_is_refused()
        {
            Read<BarkBank> read = BarkReader.Parse(
                @"{ ""speaker"": ""wolf"", ""banks"": { } }", "wolf.json");

            Assert.False(read.Ok);
            Assert.Contains(read.Problems, p => p.What.Contains("never speaks"));
        }

        [Fact]
        public void A_voice_that_reads_the_throw_owes_two_more_keys_and_both_of_them_take_a_number()
        {
            BarkBank bank = Bank(@"{
                ""speaker"": ""wolf"",
                ""banks"": { ""snag"": 2 },
                ""reads_the_throw"": true
            }");

            Assert.True(bank.ReadsTheThrow);
            Assert.Contains("dialogue.wolf.readout.hit", bank.Keys());
            Assert.Contains("dialogue.wolf.readout.miss", bank.Keys());

            Assert.True(bank.TakesAnArgument("dialogue.wolf.readout.hit"));
            Assert.True(bank.TakesAnArgument("dialogue.wolf.readout.miss"));
            Assert.False(bank.TakesAnArgument("dialogue.wolf.bark.snag.001"));
        }

        // the Defence reveal used to be a third readout. It is on the foe's initiative card now,
        // and a voice that still owed a key for it would be asking campaigns to translate a line
        // nothing will ever say
        [Fact]
        public void No_voice_reads_a_foes_Defence_aloud_any_more()
        {
            BarkBank bank = Bank(@"{
                ""speaker"": ""wolf"",
                ""banks"": { ""snag"": 2 },
                ""reads_the_throw"": true
            }");

            Assert.DoesNotContain(bank.Keys(), k => k.Contains("guard"));
        }

        [Fact]
        public void A_voice_that_does_not_read_the_throw_owes_no_readout_keys()
        {
            BarkBank bank = Bank(Wolf);

            Assert.False(bank.ReadsTheThrow);
            Assert.DoesNotContain(bank.Keys(), k => k.Contains("readout"));
        }


        // the shuffle CORE_RULES.md section 12 asks for, and DICE_TRAY.md M9 deferred to here
        [Fact]
        public void A_bank_is_dealt_so_every_line_is_said_once_before_any_is_said_twice()
        {
            var bag = new ShuffleBag(8, new SeededRng(4242));

            var first = new List<int>();

            for (int i = 0; i < 8; i++) first.Add(bag.Next());

            Assert.Equal(8, first.Distinct().Count());
            Assert.Equal(Enumerable.Range(1, 8), first.OrderBy(n => n));
        }

        [Fact]
        public void A_reshuffle_does_not_hand_back_the_line_it_just_said()
        {
            for (int seed = 1; seed <= 200; seed++)
            {
                var bag = new ShuffleBag(4, new SeededRng(seed));

                int last = 0;

                for (int i = 0; i < 4; i++) last = bag.Next();

                Assert.NotEqual(last, bag.Next());
            }
        }

        [Fact]
        public void A_bank_of_one_repeats_itself_because_there_is_nothing_else_to_say()
        {
            var bag = new ShuffleBag(1, new SeededRng(1));

            Assert.Equal(1, bag.Next());
            Assert.Equal(1, bag.Next());
        }

        [Fact]
        public void An_empty_bank_says_nothing_rather_than_throwing()
        {
            var bag = new ShuffleBag(0, new SeededRng(1));

            Assert.Equal(0, bag.Next());
        }

        [Fact]
        public void A_seeded_session_says_the_same_things_twice()
        {
            BarkBank bank = Bank(Wolf);

            string[] once = Said(bank, 4242);
            string[] again = Said(bank, 4242);
            string[] elsewhere = Said(bank, 99);

            Assert.Equal(once, again);
            Assert.NotEqual(once, elsewhere);
        }

        static string[] Said(BarkBank bank, int seed)
        {
            Speaking speaking = bank.Open(new SeededRng(seed));

            return Enumerable.Range(0, 20).Select(_ => speaking.Next(Bark.Snag)).ToArray();
        }

        [Fact]
        public void A_situation_this_voice_has_no_bank_for_is_silence_and_not_a_missing_key()
        {
            Speaking speaking = Bank(Wolf).Open(new SeededRng(1));

            Assert.Null(speaking.Next(Bark.Victory));
            Assert.NotNull(speaking.Next(Bark.Snag));
        }

        [Fact]
        public void A_folder_with_no_barks_is_a_book_with_nothing_in_it_and_no_problems()
        {
            BarkBook book = BarkBook.Read(null);

            Assert.Empty(book.Problems);
            Assert.Empty(book.Keys());
        }
    }
}
