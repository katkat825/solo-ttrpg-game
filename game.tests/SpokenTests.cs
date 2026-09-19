using System.Collections.Generic;
using System.Linq;
using Core.Localization;
using Game.Access;

namespace Game.Tests
{
    // WHAT A SCREEN READER IS TOLD, AND THE ANSWER TO "WHERE ARE WE" (AX2, AX5).
    //
    // Tested together because the orientation answer IS a Spoken: the same sentences go to the voice
    // and to the DM's note, so there cannot be a spoken answer and a printed one that disagree.
    public class SpokenTests
    {
        static ILocalizer English() => new DictionaryLocalizer(new Dictionary<string, string>
        {
            [Whereabouts.Here] = "You are at {0}.",
            [Whereabouts.Nowhere] = "There is no campaign on the table yet.",
            [Whereabouts.Errand] = "The errand you are on is {0}.",
            [Whereabouts.NoErrand] = "You are not carrying an errand just now.",
            [Whereabouts.Log] = "The story so far is at the front of your book.",
            ["quest.saltmarch.the_quay.name"] = "The Quay",
            ["quest.saltmarch.the_errand.title"] = "Norrel's lockbox",
        });


        // ---- sentences, never fragments ---------------------------------------------------------

        [Fact]
        public void Nothing_to_say_is_said_as_nothing()
        {
            Assert.False(Spoken.Nothing.Any);
            Assert.Equal("", Spoken.Nothing.Aloud);
            Assert.False(Spoken.Of().Any);
            Assert.False(Spoken.Of(null, "", "   ").Any);
        }

        [Fact]
        public void Blank_lines_are_dropped_rather_than_read_as_a_silence()
        {
            Spoken said = Spoken.Of("The door.", "", null, "It is shut.");

            Assert.Equal(2, said.Lines.Count);
            Assert.Equal("The door. It is shut.", said.Aloud);
        }

        [Fact]
        public void A_thing_in_hand_is_read_as_its_name_and_then_its_state()
        {
            var one = new Reachable("Contrast - High", null,
                                    also: new[] { "This line can be turned." });

            Spoken said = Spoken.Reading(one);

            Assert.Equal(new[] { "Contrast - High", "This line can be turned." }, said.Lines);
        }

        [Fact]
        public void Nothing_in_hand_reads_as_nothing()
        {
            Assert.False(Spoken.Reading(null).Any);
        }

        [Fact]
        public void Something_you_just_did_is_said_over_whatever_was_still_being_said()
        {
            Assert.True(Spoken.Urgently("You are at The Quay.").Interrupts);
            Assert.False(Spoken.Of("The door.").Interrupts);

            // and joining an urgent one to a calm one stays urgent
            Assert.True(Spoken.Of("The door.").And(Spoken.Urgently("Wait.")).Interrupts);
        }

        [Fact]
        public void A_line_that_is_still_a_key_is_caught()
        {
            // the default localizer hands back the key, which is impossible to miss on screen and
            // impossible to notice in a voice
            Assert.True(Spoken.IsAKey("ui.door.name"));
            Assert.True(Spoken.IsAKey("quest.saltmarch.the_quay.name"));

            Assert.False(Spoken.IsAKey("The door."));
            Assert.False(Spoken.IsAKey("Vigor 11"));
            Assert.False(Spoken.IsAKey(""));

            Assert.Single(Spoken.Of("The door.", "ui.shelf.name").Keys());
        }


        // ---- where are we -----------------------------------------------------------------------

        [Fact]
        public void Where_you_are_and_what_you_are_doing_and_where_the_rest_is_written_down()
        {
            Spoken said = Whereabouts.Of(English(), "quest.saltmarch.the_quay.name",
                                         "quest.saltmarch.the_errand.title");

            Assert.Equal(new[]
            {
                "You are at The Quay.",
                "The errand you are on is Norrel's lockbox.",
                "The story so far is at the front of your book.",
            }, said.Lines);

            // asked for, so it goes over anything still being said
            Assert.True(said.Interrupts);
        }

        [Fact]
        public void Carrying_nothing_says_so_rather_than_leaving_a_hole_in_the_sentence()
        {
            Spoken said = Whereabouts.Of(English(), "quest.saltmarch.the_quay.name", null);

            Assert.Equal(3, said.Lines.Count);
            Assert.Contains("not carrying an errand", said.Lines[1]);
        }

        [Fact]
        public void A_cold_room_is_told_once_that_there_is_nothing_rather_than_three_times()
        {
            Spoken said = Whereabouts.Of(English(), null, null);

            Assert.Single(said.Lines);
            Assert.Equal("There is no campaign on the table yet.", said.Lines[0]);
        }

        [Fact]
        public void Nobody_to_ask_means_nothing_said()
        {
            Assert.False(Whereabouts.Of(null, "quest.saltmarch.the_quay.name", null).Any);
        }

        [Fact]
        public void The_two_keys_that_count_a_name_in_are_the_two_that_have_a_placeholder()
        {
            foreach (string key in Whereabouts.Keys())
                Assert.Equal(key == Whereabouts.Here || key == Whereabouts.Errand,
                             Whereabouts.TakesAnArgument(key));
        }

        [Fact]
        public void Every_key_it_emits_obeys_the_grammar()
        {
            foreach (string key in Whereabouts.Keys())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));

            foreach (string key in Acts.Keys())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));

            foreach (string key in Game.Audio.Sounds.Keys())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));

            foreach (string key in Game.Book.Settings.Keys())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));
        }
    }
}
