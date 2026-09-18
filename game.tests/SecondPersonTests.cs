using System.Linq;
using Game.Localization;

namespace Game.Tests
{
    // The base game addresses YOU and never gives you a gender. The rule is
    // easy; the CHECK is the subtle part, and this is where the argument about it lives.
    //
    // The trap is that a scan for "he" flags every NPC in the game. saltmarch alone ships nine true
    // sentences about Norrel, who is a he, and a check that calls those nine bugs is a check that
    // gets switched off in a week. So the scoping is tested harder than the matching is.
    public class SecondPersonTests
    {
        // ---- which keys are the game talking to you ----------------------------------------

        [Fact]
        public void The_engines_own_strings_and_the_table_talking_at_you_are_spoken_to_you()
        {
            Assert.True(SecondPerson.SpokenToYou("combat.throw.hit"));
            Assert.True(SecondPerson.SpokenToYou("ui.initiative_card.vigor"));
            Assert.True(SecondPerson.SpokenToYou("dialogue.wolf.bark.snag.001"));
            Assert.True(SecondPerson.SpokenToYou("dialogue.wolf.readout.miss"));
        }

        // a conversation, a beat and a narration are all somebody describing the world, and the
        // world has people in it
        [Fact]
        public void A_conversation_a_beat_and_a_narration_are_about_the_world_and_not_at_you()
        {
            Assert.False(SecondPerson.SpokenToYou("dialogue.wolf.line.saltmarch.a_man_who_hires"));
            Assert.False(SecondPerson.SpokenToYou("dialogue.wolf.beat.greyhollow.warn_bridge"));
            Assert.False(SecondPerson.SpokenToYou("dialogue.dm.narration.saltmarch.norrel_looks_you_over"));
            Assert.False(SecondPerson.SpokenToYou("quest.saltmarch.the_sluice.description"));
            Assert.False(SecondPerson.SpokenToYou("actor.saltmarch.marsh_jack.name"));
        }


        // ---- the thing this check exists to catch -------------------------------------------

        [Fact]
        public void A_bark_that_calls_you_he_is_caught()
        {
            Assert.Contains("he", SecondPerson.Faults("dialogue.wolf.bark.snag.004",
                                                      "He is leaning again. He always leans."));
        }

        [Fact]
        public void An_engine_string_that_genders_you_is_caught()
        {
            Assert.NotEmpty(SecondPerson.Faults("combat.throw.hit",
                                                "Best two, {0} - beats {1}, and his axe bites {2}."));
        }

        [Fact]
        public void A_bark_about_you_and_the_dice_and_the_thing_in_front_of_you_is_fine()
        {
            Assert.Empty(SecondPerson.Faults("dialogue.wolf.bark.snag.001", "Ha. Nearly."));
            Assert.Empty(SecondPerson.Faults("dialogue.wolf.bark.trouble.003",
                                             "Get your feet under you. Now."));
            Assert.Empty(SecondPerson.Faults("dialogue.wolf.readout.miss", "Best two, {0}. Short of {1}."));
        }


        // ---- and the nine true sentences it must not cry wolf over --------------------------

        [Theory]
        [InlineData("quest.saltmarch.the_sluice.description",
                    "Norrel wants the gate at the weir opened. He wants to be paid back in person, " +
                    "and he wants to be alive to do the paying.")]
        [InlineData("dialogue.dm.narration.saltmarch.norrel_looks_you_over",
                    "He prices you the way he would price a boat: quickly, and without looking at " +
                    "your face while he does it.")]
        [InlineData("dialogue.wolf.line.saltmarch.a_man_who_hires",
                    "Because he is a man who hires. That is not a crime, whatever your face is doing.")]
        public void An_NPC_is_allowed_to_be_a_he_and_the_check_leaves_it_alone(string key, string english)
        {
            Assert.Empty(SecondPerson.Faults(key, english));
        }

        // a term of address can only be aimed at the listener, and the listener is always you
        [Fact]
        public void A_term_of_address_is_caught_wherever_it_is_written()
        {
            Assert.Contains("sir", SecondPerson.Faults("dialogue.norrel.line.saltmarch.a_greeting",
                                                       "Well met, sir. You will want the weir."));

            Assert.Contains("lad", SecondPerson.Faults("dialogue.dm.narration.greyhollow.the_door",
                                                       "Mind the step, lad."));
        }


        // ---- the matching itself --------------------------------------------------------

        // "his" is inside "history" and "her" is inside "there", "where" and "hers"
        [Fact]
        public void A_word_inside_another_word_is_not_that_word()
        {
            Assert.False(SecondPerson.Says("This place has a history.", "his"));
            Assert.False(SecondPerson.Says("There is something in there.", "her"));
            Assert.False(SecondPerson.Says("Where the water runs fastest.", "her"));
            Assert.False(SecondPerson.Says("A shed at the end of the boards.", "she"));
        }

        [Fact]
        public void An_apostrophe_is_part_of_a_word_so_maam_matches_whole()
        {
            Assert.True(SecondPerson.Says("As you say, ma'am.", "ma'am"));
            Assert.False(SecondPerson.Says("As you say, ma'am.", "ma"));
        }

        [Fact]
        public void Case_does_not_matter_because_a_sentence_starts_with_one()
        {
            Assert.True(SecondPerson.Says("He is leaning.", "he"));
            Assert.True(SecondPerson.Says("SIR.", "sir"));
        }

        [Fact]
        public void A_line_with_nothing_in_it_is_not_a_fault()
        {
            Assert.Empty(SecondPerson.Faults("combat.throw.hit", ""));
            Assert.Empty(SecondPerson.Faults("combat.throw.hit", null));
        }

        // the report names one word; it must be the one that is actually there
        [Fact]
        public void The_words_reported_are_the_ones_the_line_uses()
        {
            var faults = SecondPerson.Faults("dialogue.wolf.bark.down.001", "Up, lad. He needs you.");

            Assert.Equal(new[] { "lad", "he" }, faults.ToArray());
        }

        [Fact]
        public void Every_forbidden_word_is_explained_in_terms_of_the_scope_it_broke()
        {
            Assert.Contains("second person",
                            SecondPerson.Explain("dialogue.wolf.bark.snag.001", "he"));

            Assert.Contains(SecondPerson.AllowFile,
                            SecondPerson.Explain("dialogue.norrel.line.x.y", "sir"));
        }
    }
}
