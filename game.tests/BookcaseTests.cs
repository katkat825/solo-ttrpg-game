using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Saves;
using Content.Sheet;
using Game.Book;

namespace Game.Tests
{
    // EVERY BOOK YOU HAVE (BK1, BK2, BK5).
    //
    // Written to a real folder and read back through the real SaveShelf, because the whole claim of
    // this phase is that the books ARE the save folder with a different face on it. A fixture that
    // handed Volume a list of invented boxes would be testing the face and not the identity.
    public sealed class BookcaseTests : IDisposable
    {
        readonly string _folder;

        public BookcaseTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "books-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_folder, recursive: true); } catch { }
        }

        // written minutes apart, so "newest first" is a real question rather than a tie
        void Write(string campaign, string who, int minutesAgo, string place = "the_quay")
        {
            var save = new SaveGame
            {
                Campaign = campaign,
                CampaignFormat = 1,
                Place = place,
                Sheet = new CharacterSheet { Name = who, ClassId = "hearthguard.warden" },
            };

            string file = $"{campaign}_{who}_{minutesAgo}{SaveShelf.Extension}";

            string path = Path.Combine(_folder, file);

            Assert.Null(SaveWriter.To(path, save));

            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-minutesAgo));
        }

        SaveShelf Shelf() => SaveShelf.Read(_folder);


        // ---- BK2: the bookcase is not the save folder ----------------------------------------

        // A CAMPAIGN YOU HAVE NEVER PLAYED IS STILL A BOOK. This is the whole difference between a
        // bookcase and a list of save files, and it is what makes the bookcase the main area.
        [Fact]
        public void An_installed_campaign_with_no_saves_is_a_closed_book_on_the_case()
        {
            Collection books = Collection.Of(new[] { "saltmarch", "greyhollow" }, Shelf());

            Assert.Equal(2, books.Count);
            Assert.All(books.Volumes, v => Assert.True(v.Installed));
            Assert.All(books.Volumes, v => Assert.True(v.Unopened));
            Assert.False(books.CanContinue);
        }

        // AND A SAVE WHOSE CAMPAIGN IS GONE IS STILL A BOOK, said so. Dropping somebody's
        // playthrough because they unsubscribed from a Workshop campaign is how a save folder stops
        // being trustworthy.
        [Fact]
        public void A_save_for_a_campaign_that_is_not_installed_still_stands_and_says_so()
        {
            Write("unsubscribed", "Aeth", 5);

            Collection books = Collection.Of(new[] { "saltmarch" }, Shelf());

            Volume gone = books.Of("unsubscribed");

            Assert.NotNull(gone);
            Assert.False(gone.Installed);
            Assert.Single(books.Unreadable);

            // and it is never what a relaunch continues into, because that would be a crash
            // dressed up as a convenience
            Assert.False(books.CanContinue);
            Assert.Null(books.Most);
        }

        [Fact]
        public void Installed_books_stand_in_the_same_order_every_time_the_case_is_looked_at()
        {
            Collection books = Collection.Of(new[] { "saltmarch", "ashfall", "greyhollow" }, Shelf());

            Assert.Equal(new[] { "ashfall", "greyhollow", "saltmarch" },
                         books.Volumes.Select(v => v.Campaign).ToArray());
        }

        // RELAUNCH CONTINUES IN PLACE: the newest place in any book you can read, with no prompt
        [Fact]
        public void The_newest_place_in_any_readable_book_is_what_a_relaunch_continues()
        {
            Write("saltmarch", "Aeth", 40);
            Write("greyhollow", "Brann", 5);
            Write("saltmarch", "Cass", 90);

            Collection books = Collection.Of(new[] { "saltmarch", "greyhollow" }, Shelf());

            Assert.True(books.CanContinue);
            Assert.Equal("greyhollow", books.Newest.Campaign);
            Assert.Equal("Brann", books.Most.Who);
        }


        // ---- BK5: five characters, and a blank one -------------------------------------------

        [Fact]
        public void Forty_autosaves_under_one_name_are_one_character()
        {
            for (int at = 1; at <= 40; at++) Write("saltmarch", "Aeth", at);

            Volume book = Collection.Of(new[] { "saltmarch" }, Shelf()).Of("saltmarch");

            Bookmark only = Assert.Single(book.Bookmarks);

            Assert.Equal("Aeth", only.Who);

            // and the place it falls open at is the newest of them
            Assert.Equal("saltmarch_Aeth_1" + SaveShelf.Extension, only.Latest.File);
        }

        [Fact]
        public void A_book_with_room_in_it_offers_a_blank_ribbon_at_the_end_of_the_row()
        {
            Write("saltmarch", "Aeth", 10);

            Volume book = Collection.Of(new[] { "saltmarch" }, Shelf()).Of("saltmarch");

            Assert.True(book.RoomForAnother);
            Assert.Equal(2, book.Ribbons.Count());
            Assert.True(book.Ribbons.Last().IsBlank);
        }

        [Fact]
        public void At_five_characters_the_blank_ribbon_is_gone_and_a_sixth_does_not_get_in()
        {
            for (int at = 0; at < Volume.Most + 2; at++) Write("saltmarch", "Hero" + at, 10 + at);

            Volume book = Collection.Of(new[] { "saltmarch" }, Shelf()).Of("saltmarch");

            Assert.Equal(Volume.Most, book.Bookmarks.Count);
            Assert.False(book.RoomForAnother);
            Assert.DoesNotContain(book.Ribbons, r => r.IsBlank);

            // the five it kept are the five most recently played, which is what you reach for
            Assert.Equal(new[] { "Hero0", "Hero1", "Hero2", "Hero3", "Hero4" },
                         book.Bookmarks.Select(b => b.Who).ToArray());
        }

        [Fact]
        public void Characters_in_a_book_stand_newest_first()
        {
            Write("saltmarch", "Older", 90);
            Write("saltmarch", "Newer", 2);

            Volume book = Collection.Of(new[] { "saltmarch" }, Shelf()).Of("saltmarch");

            Assert.Equal(new[] { "Newer", "Older" }, book.Bookmarks.Select(b => b.Who).ToArray());
        }

        // a blank ribbon is New Character and nothing else; it is never mistaken for a nameless one
        [Fact]
        public void A_character_with_no_name_is_a_character_and_a_blank_ribbon_is_not()
        {
            Write("saltmarch", "", 10);

            Volume book = Collection.Of(new[] { "saltmarch" }, Shelf()).Of("saltmarch");

            Bookmark unnamed = Assert.Single(book.Bookmarks);

            Assert.False(unnamed.IsBlank);
            Assert.True(unnamed.Unnamed);

            Assert.True(Bookmark.Blank().IsBlank);
            Assert.False(Bookmark.Blank().Unnamed);
        }

        // the title on the spine is the CAMPAIGN's string, not the engine's - a book's title is
        // content in exactly the way its monsters' names are
        [Fact]
        public void A_books_title_is_keyed_under_the_campaign_that_wrote_it()
        {
            Volume book = Collection.Of(new[] { "saltmarch" }, Shelf()).Of("saltmarch");

            Assert.Equal("campaign.saltmarch.name", book.TitleKey);
            Assert.Equal("campaign.saltmarch.description", book.DescriptionKey);
            Assert.True(Core.Localization.KeyConventions.IsWellFormed(book.TitleKey));
        }

        [Fact]
        public void An_empty_folder_and_nothing_installed_is_an_empty_bookcase()
        {
            Collection books = Collection.Of(null, Shelf());

            Assert.Equal(0, books.Count);
            Assert.Null(books.Most);
            Assert.Null(books.Of("saltmarch"));
            Assert.Null(books.Of(null));
        }
    }
}
