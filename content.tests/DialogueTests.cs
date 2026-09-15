using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Dialogue;
using Core.Dice;
using Core.Localization;

namespace Content.Tests
{
    // W0. The runtime is Yarn Spinner and is not on trial here - what is on trial is the seam:
    // structure comes out of the .yarn, words come out of the locale, and nothing in between ever
    // puts the author's working text on the screen.
    public class DialogueTests
    {
        const string Campaign = "greyhollow";

        static DialogueBook Book(string source, string file = "dialogue/talk.yarn") =>
            DialogueBook.Of(Campaign, (file, source));

        static string Yarn(params string[] lines) => string.Join("\n", lines) + "\n";


        [Fact]
        public void A_tagged_line_is_keyed_by_speaker_campaign_and_tag()
        {
            DialogueBook book = Book(Yarn(
                "title: at_the_stair",
                "speaker: wolf",
                "---",
                "The hinges are new. #line:the_hinges_are_new",
                "==="));

            Assert.Empty(book.Problems);

            DialogueLine line = book.Line("line:the_hinges_are_new");

            Assert.NotNull(line);
            Assert.Equal("dialogue.wolf.line.greyhollow.the_hinges_are_new", line.Key);
            Assert.Equal("wolf", line.Speaker);
            Assert.Equal("at_the_stair", line.Node);
        }

        [Fact]
        public void Every_key_a_book_emits_obeys_the_grammar()
        {
            DialogueBook book = Book(Yarn(
                "title: at_the_stair",
                "speaker: wolf",
                "---",
                "One. #line:one",
                "-> Ask #line:ask",
                "    Two. #line:two",
                "==="));

            Assert.Empty(book.Problems);
            Assert.NotEmpty(book.Keys());

            foreach (string key in book.Keys())
                Assert.True(KeyConventions.IsWellFormed(key), KeyConventions.Explain(key));
        }

        // an invented id is a hash of the text: edit the line and every translation of it is orphaned
        [Fact]
        public void An_untagged_line_is_refused_by_name_and_line_number()
        {
            DialogueBook book = Book(Yarn(
                "title: at_the_stair",
                "speaker: wolf",
                "---",
                "This one has no tag.",
                "==="));

            Assert.Contains(book.Problems, p => p.What.Contains("#line:") && p.Line == 4);
            Assert.Empty(book.Keys());
        }

        [Fact]
        public void A_node_with_no_speaker_is_refused()
        {
            DialogueBook book = Book(Yarn(
                "title: at_the_stair",
                "---",
                "Somebody says this. #line:somebody",
                "==="));

            Assert.Contains(book.Problems, p => p.What.Contains("speaker:"));
        }

        [Fact]
        public void A_line_may_name_its_own_speaker_where_two_share_a_scene()
        {
            DialogueBook book = Book(Yarn(
                "title: the_bargain",
                "speaker: wolf",
                "---",
                "Careful. #line:careful",
                "For a price. #line:for_a_price #speaker:imp",
                "==="));

            Assert.Empty(book.Problems);
            Assert.Equal("wolf", book.Line("line:careful").Speaker);
            Assert.Equal("imp", book.Line("line:for_a_price").Speaker);
            Assert.Equal("dialogue.imp.line.greyhollow.for_a_price", book.Line("line:for_a_price").Key);
        }

        [Fact]
        public void A_syntax_error_is_reported_with_its_file_and_line_and_nothing_compiles()
        {
            DialogueBook book = Book(Yarn(
                "title: broken",
                "speaker: wolf",
                "---",
                "<<if>>",
                "==="));

            Assert.NotEmpty(book.Problems);
            Assert.All(book.Problems, p => Assert.Equal("dialogue/talk.yarn", p.File));
            Assert.Null(book.Program);
        }

        [Fact]
        public void A_jump_to_a_node_nobody_wrote_is_refused()
        {
            DialogueBook book = Book(Yarn(
                "title: start",
                "speaker: wolf",
                "---",
                "Off we go. #line:off_we_go",
                "<<jump nowhere>>",
                "==="));

            Assert.Contains(book.Problems, p => p.What.Contains("nowhere"));
        }

        [Fact]
        public void The_same_line_id_in_two_files_is_refused()
        {
            DialogueBook book = DialogueBook.Of(
                Campaign,
                ("dialogue/one.yarn", Yarn("title: one", "speaker: wolf", "---", "A. #line:same", "===")),
                ("dialogue/two.yarn", Yarn("title: two", "speaker: wolf", "---", "B. #line:same", "===")));

            Assert.Contains(book.Problems, p => p.What.Contains("same"));
        }

        [Fact]
        public void Nodes_can_jump_between_files_in_the_same_campaign()
        {
            DialogueBook book = DialogueBook.Of(
                Campaign,
                ("dialogue/one.yarn", Yarn("title: one", "speaker: wolf", "---", "A. #line:a", "<<jump two>>", "===")),
                ("dialogue/two.yarn", Yarn("title: two", "speaker: imp", "---", "B. #line:b", "===")));

            Assert.Empty(book.Problems);

            var talk = new Conversation(book);

            Assert.True(talk.Start("one"));

            var heard = new List<string>();

            while (!talk.IsOver)
            {
                if (talk.Saying != null) heard.Add(talk.Saying.Key);

                talk.Advance();
            }

            Assert.Equal(
                new[] { "dialogue.wolf.line.greyhollow.a", "dialogue.imp.line.greyhollow.b" },
                heard);
        }

        [Fact]
        public void A_branch_is_taken_by_choosing_and_the_words_come_from_the_localizer()
        {
            DialogueBook book = Book(Yarn(
                "title: at_the_stair",
                "speaker: wolf",
                "---",
                "The hinges are new. #line:hinges",
                "-> Push it #line:push",
                "    It gives. #line:it_gives",
                "-> Leave it #line:leave",
                "    Good. #line:good",
                "==="));

            Assert.Empty(book.Problems);

            var words = new DictionaryLocalizer(new Dictionary<string, string>
            {
                ["dialogue.wolf.line.greyhollow.hinges"] = "The hinges are new.",
                ["dialogue.wolf.line.greyhollow.push"] = "Push it",
                ["dialogue.wolf.line.greyhollow.leave"] = "Leave it",
                ["dialogue.wolf.line.greyhollow.it_gives"] = "It gives.",
                ["dialogue.wolf.line.greyhollow.good"] = "Good.",
            });

            var talk = new Conversation(book);

            Assert.True(talk.Start("at_the_stair"));
            Assert.Equal("The hinges are new.", talk.Saying.Text(words));

            talk.Advance();

            Assert.True(talk.IsChoosing);
            Assert.Equal(2, talk.Choosing.Count);
            Assert.Equal("Push it", talk.Choosing[0].Text(words));

            talk.Choose(1);

            Assert.Equal("Good.", talk.Saying.Text(words));
        }

        // the point of the seam: the .yarn source is the author's working copy and never the screen
        [Fact]
        public void The_words_shown_are_the_locales_and_not_the_yarn_sources()
        {
            DialogueBook book = Book(Yarn(
                "title: at_the_stair",
                "speaker: wolf",
                "---",
                "WORKING TEXT, NOT FOR SHOW. #line:hinges",
                "==="));

            var words = new DictionaryLocalizer(new Dictionary<string, string>
            {
                ["dialogue.wolf.line.greyhollow.hinges"] = "The hinges are new.",
            });

            var talk = new Conversation(book);
            talk.Start("at_the_stair");

            Assert.Equal("The hinges are new.", talk.Saying.Text(words));
            Assert.DoesNotContain("WORKING", talk.Saying.Text(words));
        }

        // a campaign is data, never code: an unknown command is recorded and stepped over, never run
        [Fact]
        public void An_unknown_command_is_named_and_not_run()
        {
            DialogueBook book = Book(Yarn(
                "title: at_the_stair",
                "speaker: wolf",
                "---",
                "<<give_me_the_moon 3>>",
                "Done. #line:done",
                "==="));

            Assert.Empty(book.Problems);

            var talk = new Conversation(book);
            talk.Start("at_the_stair");

            while (!talk.IsOver) talk.Advance();

            Assert.Contains("give_me_the_moon 3", talk.Commands);
        }

        [Fact]
        public void A_camp_node_carries_the_topic_it_is_for()
        {
            DialogueBook book = Book(Yarn(
                "title: after_a_bad_one",
                "speaker: wolf",
                "topic: bloodied",
                "---",
                "You are still bleeding. #line:still_bleeding",
                "==="));

            Assert.Empty(book.Problems);
            Assert.Equal(Topic.Bloodied, book.TopicOf("after_a_bad_one"));
            Assert.Equal(new[] { "after_a_bad_one" }, book.CampNodes);
        }

        [Fact]
        public void A_topic_outside_the_vocabulary_is_refused_and_lists_what_there_is()
        {
            DialogueBook book = Book(Yarn(
                "title: after_a_bad_one",
                "speaker: wolf",
                "topic: elevenses",
                "---",
                "Tea. #line:tea",
                "==="));

            Assert.Contains(book.Problems,
                            p => p.What.Contains("elevenses") && p.What.Contains("quiet"));
        }

        [Fact]
        public void A_folder_with_no_dialogue_is_a_book_with_nothing_in_it_and_no_problems()
        {
            DialogueBook book = DialogueBook.Read(null, Campaign);

            Assert.Empty(book.Problems);
            Assert.Empty(book.Keys());
            Assert.Null(book.Program);
        }
    }
}
