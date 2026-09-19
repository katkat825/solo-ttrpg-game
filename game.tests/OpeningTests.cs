using Game.Book;

namespace Game.Tests
{
    // A BOOK IN YOUR HANDS. The book is the diegetic system menu, which makes this the state a
    // pause menu would have had - and the two things worth a machine are that it always opens at
    // its contents, and that the rules book DOES NOT LOSE YOUR PLACE.
    //
    // That second one is what makes the rules book a peer rather than a help overlay: somebody
    // hands you a second book, you do not drop the one you were reading.
    public class OpeningTests
    {
        [Fact]
        public void A_shut_book_is_nowhere_and_turns_to_nothing()
        {
            var open = new Opening();

            Assert.False(open.IsOpen);
            Assert.Null(open.Which);
            Assert.False(open.TurnTo(Page.Save));
            Assert.False(open.TurnTo(Reference.Pool));
            Assert.False(open.Back());
            Assert.False(open.Close());
        }

        [Fact]
        public void A_book_opens_at_its_contents_page()
        {
            var open = new Opening();

            Assert.True(open.Open(Tome.Campaign));
            Assert.True(open.IsOpen);
            Assert.True(open.AtTheContents);
            Assert.Equal(Tome.Campaign, open.Which);
            Assert.Null(open.At);
        }

        [Fact]
        public void Opening_the_book_you_are_already_holding_does_nothing()
        {
            var open = new Opening();

            open.Open(Tome.Campaign);

            Assert.False(open.Open(Tome.Campaign));
        }

        [Fact]
        public void Turning_to_a_page_puts_you_on_it_and_turning_to_it_again_does_not()
        {
            var open = new Opening();

            open.Open(Tome.Campaign);

            Assert.True(open.TurnTo(Page.StorySoFar));
            Assert.Equal(Page.StorySoFar, open.At);
            Assert.False(open.AtTheContents);

            Assert.False(open.TurnTo(Page.StorySoFar));
        }

        // the campaign book has no chapter on the dice, and the rules book has no save page
        [Fact]
        public void A_book_only_turns_to_the_pages_it_has()
        {
            var open = new Opening();

            open.Open(Tome.Campaign);
            Assert.False(open.TurnTo(Reference.Impact));

            open.Open(Tome.Rules);
            Assert.False(open.TurnTo(Page.Save));
            Assert.True(open.TurnTo(Reference.Impact));
            Assert.Equal(Reference.Impact, open.Chapter);
            Assert.Null(open.At);
        }

        [Fact]
        public void Back_from_a_page_is_the_contents_and_back_from_the_contents_is_shut()
        {
            var open = new Opening();

            open.Open(Tome.Campaign);
            open.TurnTo(Page.Settings);

            Assert.True(open.Back());
            Assert.True(open.AtTheContents);

            Assert.True(open.Back());
            Assert.False(open.IsOpen);
        }

        // THE ONE THAT MATTERS. Pressing "?" mid-page and closing the rules book again puts you
        // back on the page you were reading, because that is what happens when somebody hands you
        // a second book.
        [Fact]
        public void The_rules_book_lays_over_the_campaign_book_and_gives_it_back()
        {
            var open = new Opening();

            open.Open(Tome.Campaign);
            open.TurnTo(Page.StorySoFar);

            Assert.True(open.Open(Tome.Rules));
            Assert.True(open.Beneath);
            Assert.Equal(Tome.Rules, open.Which);

            open.TurnTo(Reference.Nerve);

            // out of the chapter, then out of the book
            Assert.True(open.Back());
            Assert.True(open.AtTheContents);

            Assert.True(open.Back());

            Assert.Equal(Tome.Campaign, open.Which);
            Assert.Equal(Page.StorySoFar, open.At);
            Assert.False(open.Beneath);
        }

        // nothing else stacks: picking up a campaign book while holding one replaces it, because
        // you have two hands and one of them is holding the pencil
        [Fact]
        public void Only_the_rules_book_stacks()
        {
            var open = new Opening();

            open.Open(Tome.Rules);
            open.TurnTo(Reference.Pool);

            Assert.True(open.Open(Tome.Campaign));
            Assert.False(open.Beneath);

            Assert.True(open.Back());
            Assert.False(open.IsOpen);
        }

        [Fact]
        public void Closing_the_rules_book_closes_the_one_under_it_too()
        {
            var open = new Opening();

            open.Open(Tome.Campaign);
            open.Open(Tome.Rules);

            Assert.True(open.Close());
            Assert.False(open.IsOpen);
            Assert.False(open.Beneath);
            Assert.Null(open.Which);
        }

        // every page and chapter printed in the contents has a well-formed key, or the contents
        // page is a column of raw identifiers
        [Fact]
        public void Every_line_of_both_contents_pages_is_a_key_the_grammar_allows()
        {
            foreach (Tome tome in System.Enum.GetValues<Tome>())
            {
                Assert.NotEmpty(Contents.Of(tome));

                foreach (string key in Contents.Of(tome))
                    Assert.True(Core.Localization.KeyConventions.IsWellFormed(key),
                                Core.Localization.KeyConventions.Explain(key));
            }
        }

        [Fact]
        public void Every_key_this_phase_emits_obeys_the_grammar()
        {
            foreach (string key in BookKeys.All())
                Assert.True(Core.Localization.KeyConventions.IsWellFormed(key),
                            Core.Localization.KeyConventions.Explain(key));
        }

        // derived, never listed: a page added to the book has to turn up in the contents and in the
        // key list without anybody editing a second place
        [Fact]
        public void The_contents_page_has_a_line_per_page_and_a_line_per_chapter()
        {
            Assert.Equal(System.Enum.GetValues<Page>().Length, Contents.Count(Tome.Campaign));
            Assert.Equal(System.Enum.GetValues<Reference>().Length, Contents.Count(Tome.Rules));
        }

        [Fact]
        public void A_word_round_trips_for_every_closed_vocabulary_in_the_book()
        {
            foreach (Tome tome in System.Enum.GetValues<Tome>())
            {
                Assert.True(Tomes.TryWord(tome.Word(), out Tome back));
                Assert.Equal(tome, back);
            }

            foreach (Page page in System.Enum.GetValues<Page>())
            {
                Assert.True(Pages.TryWord(page.Word(), out Page back));
                Assert.Equal(page, back);
            }

            foreach (Reference chapter in System.Enum.GetValues<Reference>())
            {
                Assert.True(References.TryWord(chapter.Word(), out Reference back));
                Assert.Equal(chapter, back);
            }

            Assert.False(Pages.TryWord("", out _));
            Assert.False(Tomes.TryWord("grimoire", out _));
        }
    }
}
