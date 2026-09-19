using System;
using System.IO;
using System.Linq;
using Content.Saves;
using Game.Book;

namespace Game.Tests
{
    // THE RELOAD TABS (BK4).
    //
    // Reloading is allowed on purpose: persistence means the world remembers and reacts, not that
    // the player is trapped. Autosave was built to accumulate snapshots rather than overwrite one,
    // and this is the face of that folder - so the thing worth a machine is that the five under
    // your thumb are the right five, in the right order, and that the sixth tab is absent when
    // there is nothing behind it.
    public sealed class TabsTests : IDisposable
    {
        readonly string _folder;

        public TabsTests()
        {
            _folder = Path.Combine(Path.GetTempPath(), "tabs-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try { Directory.Delete(_folder, recursive: true); } catch { }
        }

        void Write(string campaign, int minutesAgo)
        {
            string path = Path.Combine(_folder,
                                       $"{campaign}_{minutesAgo:000}{SaveShelf.Extension}");

            Assert.Null(SaveWriter.To(path, new SaveGame { Campaign = campaign, Place = "somewhere" }));

            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(-minutesAgo));
        }

        SaveShelf Shelf() => SaveShelf.Read(_folder);

        [Fact]
        public void An_empty_folder_has_no_tabs_and_nothing_behind_the_sixth()
        {
            var tabs = new Tabs(null);

            Assert.Equal(0, tabs.Count);
            Assert.Empty(tabs.Showing);
            Assert.False(tabs.More);
            Assert.False(tabs.ShowMore());
            Assert.Null(tabs.Turn("anything.json"));
        }

        [Fact]
        public void Fewer_than_five_saves_are_all_of_them_and_no_sixth_tab()
        {
            for (int at = 1; at <= 3; at++) Write("saltmarch", at);

            var tabs = new Tabs(Shelf().Boxes);

            Assert.Equal(3, tabs.Count);
            Assert.Equal(3, tabs.Showing.Count);
            Assert.False(tabs.More);
            Assert.Equal(0, tabs.Hidden);
        }

        // five down the edge, because a book with forty tabs is a filing cabinet
        [Fact]
        public void Five_are_reachable_and_the_rest_are_behind_show_more()
        {
            for (int at = 1; at <= 12; at++) Write("saltmarch", at);

            var tabs = new Tabs(Shelf().Boxes);

            Assert.Equal(12, tabs.Count);
            Assert.Equal(Tabs.Reachable, tabs.Showing.Count);
            Assert.True(tabs.More);
            Assert.Equal(7, tabs.Hidden);

            Assert.True(tabs.ShowMore());
            Assert.Equal(Tabs.Reachable + Tabs.Behind, tabs.Showing.Count);
            Assert.True(tabs.More);

            Assert.True(tabs.ShowMore());
            Assert.Equal(12, tabs.Showing.Count);

            // and a control that would do nothing is not offered
            Assert.False(tabs.More);
            Assert.False(tabs.ShowMore());
        }

        [Fact]
        public void Closing_the_book_and_opening_it_again_is_back_to_five()
        {
            for (int at = 1; at <= 12; at++) Write("saltmarch", at);

            var tabs = new Tabs(Shelf().Boxes);

            tabs.ShowMore();
            tabs.Thumb();

            Assert.Equal(Tabs.Reachable, tabs.Showing.Count);
        }

        // NEWEST FIRST, and sorted here rather than trusted from the caller: SaveShelf does sort,
        // and a view that leans on that breaks the first time it is handed a filtered list
        [Fact]
        public void The_newest_save_is_the_tab_under_your_thumb_whatever_order_it_arrives_in()
        {
            Write("saltmarch", 90);
            Write("saltmarch", 2);
            Write("saltmarch", 40);

            var tabs = new Tabs(Shelf().Boxes.Reverse());

            Assert.Equal("saltmarch_002.json", tabs.Showing[0].File);
            Assert.Equal("saltmarch_040.json", tabs.Showing[1].File);
            Assert.Equal("saltmarch_090.json", tabs.Showing[2].File);
        }

        // A TAB IN THIS BOOK, not in the bookcase: another campaign's saves are in another book
        [Fact]
        public void A_books_tabs_are_its_own_campaigns_saves()
        {
            for (int at = 1; at <= 3; at++) Write("saltmarch", at);
            for (int at = 10; at <= 14; at++) Write("greyhollow", at);

            SaveShelf shelf = Shelf();

            Assert.Equal(3, Tabs.Of(shelf, "saltmarch").Count);
            Assert.Equal(5, Tabs.Of(shelf, "greyhollow").Count);
            Assert.Equal(0, Tabs.Of(shelf, "nothing_by_that_name").Count);

            // and no campaign at all is the whole folder, which is what the bookcase reads
            Assert.Equal(8, Tabs.Of(shelf, null).Count);
        }

        [Fact]
        public void Turning_to_a_tab_gives_back_the_save_it_names_and_only_a_showing_one()
        {
            for (int at = 1; at <= 8; at++) Write("saltmarch", at);

            var tabs = new Tabs(Shelf().Boxes);

            Assert.NotNull(tabs.Turn("saltmarch_001.json"));

            // the eighth is behind show-more, so it is not something you can turn to yet
            Assert.Null(tabs.Turn("saltmarch_008.json"));

            tabs.ShowMore();

            Assert.NotNull(tabs.Turn("saltmarch_008.json"));
        }

        [Fact]
        public void The_sixth_tabs_word_counts_a_number_in()
        {
            Assert.Equal("ui.save_tab.more", Tabs.MoreKey);
            Assert.True(BookKeys.TakesAnArgument(Tabs.MoreKey));
        }
    }
}
