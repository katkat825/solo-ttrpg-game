using System;
using System.Linq;
using Content.Campaigns;
using Game.Book;
using Game.Room;

namespace Game.Tests
{
    // WHAT YOU OWN, AND THE DOORS INTO MAKING MORE (BK6).
    //
    // Two halves of one milestone. The loadout is the dice-skin swap, and the thing worth a machine
    // is the refusal: Dressing has no opinion about whether you own a thing, deliberately, so this
    // is the only place that question is asked and it has to be asked correctly. The starters are
    // the Workshop door, and what is checkable there is that every content type has one and that
    // each one names a real kind and a real skeleton - the Workshop behind it is Steam's (P8).
    public class LoadoutTests
    {
        [Fact]
        public void Every_prop_owns_the_plain_one_and_wears_it_to_begin_with()
        {
            var loadout = new Loadout();

            foreach (TableProp prop in Enum.GetValues<TableProp>())
            {
                Assert.Equal(new[] { Dressing.Plain }, loadout.Owned(prop));
                Assert.Equal(Dressing.Plain, loadout.Wearing(prop));
                Assert.True(loadout.Has(prop, Dressing.Plain));
            }
        }

        [Fact]
        public void Earning_a_skin_adds_it_once()
        {
            var loadout = new Loadout();

            Assert.True(loadout.Earn(TableProp.Tray, "gamblers"));
            Assert.False(loadout.Earn(TableProp.Tray, "gamblers"));
            Assert.False(loadout.Earn(TableProp.Tray, "  gamblers  "));

            Assert.Equal(2, loadout.Count(TableProp.Tray));

            // and a nameless one is a prop quietly disappearing, so it is refused
            Assert.False(loadout.Earn(TableProp.Tray, ""));
            Assert.False(loadout.Earn(TableProp.Tray, null));
        }

        // THE REFUSAL. This is why the loadout exists at all rather than the book writing straight
        // to Dressing: a table can be dressed by a check, a save or a campaign without any of them
        // knowing what the player has earned, and the book is the one place that does know.
        [Fact]
        public void A_skin_you_do_not_own_never_reaches_the_table()
        {
            var loadout = new Loadout();

            Assert.False(loadout.Wear(TableProp.Tray, "somebody_elses"));
            Assert.Equal(Dressing.Plain, loadout.Wearing(TableProp.Tray));

            loadout.Earn(TableProp.Tray, "gamblers");

            Assert.True(loadout.Wear(TableProp.Tray, "gamblers"));
            Assert.Equal("gamblers", loadout.Wearing(TableProp.Tray));

            // and owning it for the tray does not mean owning it for the screen
            Assert.False(loadout.Wear(TableProp.Screen, "gamblers"));
        }

        [Fact]
        public void Going_back_to_plain_is_the_one_swap_that_cannot_be_refused()
        {
            var loadout = new Loadout();

            loadout.Earn(TableProp.Tray, "gamblers");
            loadout.Wear(TableProp.Tray, "gamblers");

            Assert.True(loadout.Strip(TableProp.Tray));
            Assert.Equal(Dressing.Plain, loadout.Wearing(TableProp.Tray));
        }

        // the loadout page has a line per prop there is a CHOICE about; one thing is not a choice
        [Fact]
        public void A_prop_you_own_one_of_is_not_a_line_on_the_page()
        {
            var loadout = new Loadout();

            Assert.Empty(loadout.Choices);

            loadout.Earn(TableProp.Tray, "gamblers");

            Assert.Equal(new[] { TableProp.Tray }, loadout.Choices.ToArray());

            // and the mat is never a choice: it is swapped because you walked somewhere
            loadout.Earn(TableProp.Mat, "somewhere");

            Assert.DoesNotContain(TableProp.Mat, loadout.Choices);
        }

        // the loadout wears things through the dressing it was handed, so a table already dressed
        // by something else is the table it changes
        [Fact]
        public void The_loadout_dresses_the_table_it_was_given()
        {
            var dressing = new Dressing();

            var loadout = new Loadout(dressing);

            loadout.Earn(TableProp.Tray, "gamblers");
            loadout.Wear(TableProp.Tray, "gamblers");

            Assert.Equal("gamblers", dressing.Wearing(TableProp.Tray));
            Assert.Equal(1, dressing.Swapped);
        }


        // ---- the Workshop door ---------------------------------------------------------------

        [Fact]
        public void There_is_a_blank_object_for_every_content_type_and_each_has_a_name_key()
        {
            foreach (Starter starter in Enum.GetValues<Starter>())
            {
                Assert.False(string.IsNullOrWhiteSpace(starter.Word()));

                Assert.True(Core.Localization.KeyConventions.IsWellFormed(starter.NameKey()),
                            Core.Localization.KeyConventions.Explain(starter.NameKey()));

                Assert.True(Starters.TryWord(starter.Word(), out Starter back));
                Assert.Equal(starter, back);
            }

            Assert.Equal(Enum.GetValues<Starter>().Length, Starters.Keys().Count());
        }

        // AN ABILITY LIVES IN A CLASS PACK AND A BUILDING IN A MINI PACK. Neither is a pack kind of
        // its own, and saying so here is the schema agreeing with itself rather than growing a
        // fourth kind of folder.
        [Fact]
        public void Each_blank_thing_lands_in_a_pack_kind_the_loader_already_reads()
        {
            Assert.Equal(PackKind.Campaign, Starter.Campaign.Kind());
            Assert.Equal(PackKind.Classes, Starter.Classes.Kind());
            Assert.Equal(PackKind.Classes, Starter.Ability.Kind());
            Assert.Equal(PackKind.Minis, Starter.Minis.Kind());
            Assert.Equal(PackKind.Minis, Starter.Building.Kind());
        }

        [Fact]
        public void A_blank_thing_with_a_skeleton_says_which_folder_it_fills_in_first()
        {
            foreach (Starter starter in Starters.Ready)
            {
                Assert.NotEqual("", starter.Skeleton());
                Assert.NotEqual("", starter.Fills());
                Assert.False(starter.Waiting());
            }
        }

        // THE ONE HONEST GAP. A dice skin is a .tres in the engine's own folder and is not a pack at
        // all, so there is nothing to copy - and the blank object says so rather than opening a
        // Workshop onto nothing. Written down as a test so that making skins a pack kind flips it.
        [Fact]
        public void The_only_content_type_with_nothing_to_copy_is_the_dice()
        {
            Assert.Equal(new[] { Starter.Dice },
                         Enum.GetValues<Starter>().Where(Starters.Waiting).ToArray());

            Assert.Equal("", Starter.Dice.Skeleton());
        }
    }
}
