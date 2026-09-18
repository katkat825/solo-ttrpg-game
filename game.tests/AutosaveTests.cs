using System;
using Game.Saves;

namespace Game.Tests
{
    // The save MACHINERY was all built and all correct - a snapshot of the fact store, a writer, a
    // shelf, and a load that puts the turn order back rather than re-rolling it. What was missing
    // was any reason for it to run. This is that reason, and it is the half worth a test: it does
    // no I/O and needs no engine, so "is it worth writing right now" is answerable here.
    public class AutosaveTests
    {
        [Fact]
        public void Nothing_has_happened_so_an_event_is_not_worth_a_file()
        {
            var saving = new Autosave();

            Assert.False(saving.Dirty);
            Assert.False(saving.Worth(Autosave.When.Fought));
            Assert.False(saving.Worth(Autosave.When.Arrived));
        }

        [Fact]
        public void Something_happened_so_the_next_event_writes()
        {
            var saving = new Autosave();

            saving.Happened();

            Assert.True(saving.Worth(Autosave.When.Fought));
        }

        // a save per frame is how a folder of snapshots becomes unreadable, and the reload tabs
        // are a view over that folder
        [Fact]
        public void The_same_event_twice_over_writes_once()
        {
            var saving = new Autosave();

            saving.Happened();
            saving.Wrote(Autosave.When.Fought);

            Assert.False(saving.Worth(Autosave.When.Fought));
            Assert.Equal(1, saving.Written);
        }

        // the door cannot be gone back for, and a player who asked and got nothing cannot tell a
        // no-op from a bug
        [Fact]
        public void The_door_and_the_player_are_never_told_there_is_nothing_to_write()
        {
            var saving = new Autosave();

            Assert.True(saving.Worth(Autosave.When.Quit));
            Assert.True(saving.Worth(Autosave.When.Manual));

            saving.Wrote(Autosave.When.Quit);

            Assert.True(saving.Worth(Autosave.When.Quit));
            Assert.True(saving.Worth(Autosave.When.Manual));
        }

        [Fact]
        public void A_written_save_says_what_it_was_written_for()
        {
            var saving = new Autosave();

            saving.Happened();
            saving.Wrote(Autosave.When.Arrived);

            Assert.Equal(Autosave.When.Arrived, saving.Last);
        }

        // sortable first, so the folder reads in the order the shelf stands in, and then WHY -
        // a playthrough's history rather than a column of timestamps
        [Fact]
        public void A_files_name_sorts_by_when_it_was_taken_and_says_what_took_it()
        {
            var at = new DateTime(2026, 9, 17, 21, 4, 5);

            Assert.Equal("20260917_210405_saltmarch_fought",
                         Autosave.FileName("saltmarch", Autosave.When.Fought, at));
        }

        [Fact]
        public void A_save_with_no_campaign_behind_it_is_still_a_file_with_a_name()
        {
            var at = new DateTime(2026, 9, 17, 21, 4, 5);

            Assert.Equal("20260917_210405_game_quit",
                         Autosave.FileName("", Autosave.When.Quit, at));
        }

        // two saves in the same second would otherwise be the same file, and the second would
        // quietly overwrite the first
        [Fact]
        public void Two_different_events_in_the_same_second_are_two_different_files()
        {
            var at = new DateTime(2026, 9, 17, 21, 4, 5);

            Assert.NotEqual(Autosave.FileName("saltmarch", Autosave.When.Fought, at),
                            Autosave.FileName("saltmarch", Autosave.When.Quit, at));
        }

        [Fact]
        public void Every_moment_has_a_word_so_a_new_one_cannot_land_in_a_file_called_nothing()
        {
            foreach (Autosave.When moment in Enum.GetValues<Autosave.When>())
                Assert.False(string.IsNullOrWhiteSpace(Autosave.Word(moment)));
        }
    }
}
