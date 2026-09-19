using System;
using System.Collections.Generic;
using System.Linq;
using Content.Entities;
using Content.Quests;
using Content.World;
using Game.Book;

namespace Game.Tests
{
    // THE STORY SO FAR, AND THE CORKBOARD BESIDE IT (BK4).
    //
    // The two halves of the log are remembered differently on purpose, and that is the thing worth
    // a machine: what was said and done belongs to this sitting, and the quest ledger is DERIVED
    // from the fact store every time it is asked - so it survives a reload, and it changes when the
    // world does. Murder the man who set the errand and the book says something different about it,
    // which is the whole reason quest state is never stored.
    public class StorySoFarTests
    {
        static Quest Errand(string id) =>
            new Quest(id,
                      new Requirement(new[] { "norrel.spoken" }, null),
                      new Requirement(new[] { id + ".done" }, null),
                      new Requirement(new[] { "norrel.dead" }, null));

        static Quest Spine(string id) =>
            new Quest(id, Requirement.Always, new Requirement(new[] { id + ".done" }, null), null);

        [Fact]
        public void A_fresh_book_has_nothing_on_the_page()
        {
            var story = new StorySoFar();

            Assert.True(story.NothingYet);
            Assert.True(story.IsEmpty(null, "saltmarch"));
            Assert.Empty(story.Read(null, "saltmarch"));
        }

        [Fact]
        public void What_was_said_and_done_is_kept_in_the_order_it_happened()
        {
            var story = new StorySoFar();

            story.Said("norrel", "dialogue.norrel.line.saltmarch.001");
            story.Did(Interaction.Search, "lockbox", "actor.saltmarch.lockbox.name");
            story.Said("wolf", "dialogue.wolf.bark.snag.003");

            Assert.Equal(3, story.Count);

            Assert.Equal(new[] { Recorded.Said, Recorded.Did, Recorded.Said },
                         story.Sitting.Select(e => e.What).ToArray());

            Assert.Equal("lockbox", story.Sitting[1].Subject);

            // the log reads back in the verb's OWN sentence, not the card's instruction: a card
            // says "Search it" and a log says "You searched the lockbox"
            Assert.Equal("ui.story_verb.search", story.Sitting[1].Reads);
            Assert.Equal("actor.saltmarch.lockbox.name", story.Sitting[1].Key);
        }

        // a line with no key is nothing that happened; it is a bug upstream, and it does not get a
        // blank row in the middle of somebody's log
        [Fact]
        public void A_line_with_no_key_behind_it_is_not_recorded()
        {
            var story = new StorySoFar();

            Assert.Null(story.Said("wolf", ""));
            Assert.Null(story.Said("wolf", null));

            // and an action on a thing with no name would print "You searched ."
            Assert.Null(story.Did(Interaction.Search, "lockbox", ""));

            Assert.True(story.NothingYet);
        }

        // THE LEDGER IS DERIVED. Nothing is stored, so the log is a view over the world and always
        // agrees with it.
        [Fact]
        public void A_quest_you_have_not_been_offered_is_not_news()
        {
            var facts = Facts.For("saltmarch");

            Assert.Empty(StorySoFar.Ledger(Log(facts, Errand("the_sluice")), "saltmarch"));
        }

        [Fact]
        public void An_offered_quest_is_not_in_the_log_until_you_take_it_on()
        {
            var facts = Facts.For("saltmarch");

            facts.Set("norrel.spoken");

            Assert.Empty(StorySoFar.Ledger(Log(facts, Errand("the_sluice")), "saltmarch"));

            facts.Set("the_sluice.accepted");

            Entry taken =
                Assert.Single(StorySoFar.Ledger(Log(facts, Errand("the_sluice")), "saltmarch"));

            Assert.Equal(Recorded.Accepted, taken.What);
            Assert.Equal("the_sluice", taken.Subject);
            Assert.Equal("quest.saltmarch.the_sluice.title", taken.Key);
        }

        [Fact]
        public void Finishing_it_and_then_ruining_it_both_change_what_the_book_says()
        {
            var facts = Facts.For("saltmarch");

            facts.Set("norrel.spoken");
            facts.Set("the_sluice.accepted");
            facts.Set("the_sluice.done");

            Assert.Equal(Recorded.Completed,
                         StorySoFar.Ledger(Log(facts, Errand("the_sluice")), "saltmarch")
                                   .Single().What);

            // and it un-does itself, because the turn-in depended on somebody who is now dead
            facts.Set("norrel.dead");

            Assert.Equal(Recorded.Failed,
                         StorySoFar.Ledger(Log(facts, Errand("the_sluice")), "saltmarch")
                                   .Single().What);
        }

        // THE HALF THAT SURVIVES A RELOAD, which is the reason this phase changed no save format:
        // a new StorySoFar with the same facts reads the same ledger back
        [Fact]
        public void A_reload_keeps_the_quest_half_of_the_log_and_not_the_transcript()
        {
            var facts = Facts.For("saltmarch");

            facts.Set("norrel.spoken");
            facts.Set("the_sluice.accepted");

            var sitting = new StorySoFar();

            sitting.Said("norrel", "dialogue.norrel.line.saltmarch.001");

            Assert.Equal(2, sitting.Read(Log(facts, Errand("the_sluice")), "saltmarch").Count());

            // a fresh sitting, the same world
            var later = new StorySoFar();

            Assert.Single(later.Read(Log(facts, Errand("the_sluice")), "saltmarch"));
            Assert.All(later.Read(Log(facts, Errand("the_sluice")), "saltmarch"),
                       e => Assert.True(e.IsAQuest));
        }

        // the ledger comes first, because it is what the story IS
        [Fact]
        public void The_page_reads_the_ledger_first_and_then_the_sitting()
        {
            var facts = Facts.For("saltmarch");

            facts.Set("norrel.spoken");
            facts.Set("the_sluice.accepted");

            var story = new StorySoFar();

            story.Said("norrel", "dialogue.norrel.line.saltmarch.001");

            Entry[] page = story.Read(Log(facts, Errand("the_sluice")), "saltmarch").ToArray();

            Assert.True(page[0].IsAQuest);
            Assert.False(page[1].IsAQuest);
        }


        // ---- the corkboard: what is pinned, and what may be handed back -----------------------

        // A QUEST YOU WERE OFFERED IS ONE YOU CAN HAND BACK, derived from the clause that already
        // says it rather than from a field every author would have to learn
        [Fact]
        public void An_errand_somebody_set_is_a_side_quest_and_the_opening_story_is_not()
        {
            Assert.True(Errand("the_sluice").IsSide);
            Assert.False(Spine("the_tide").IsSide);
        }

        [Fact]
        public void The_folder_is_what_you_are_carrying_and_nothing_else()
        {
            var pinning = new Pinning();

            pinning.Read(new[]
            {
                (Errand("the_sluice"), QuestState.Active),
                (Spine("the_tide"), QuestState.Offered),
                (Errand("the_nets"), QuestState.Done),
            });

            Quest only = Assert.Single(pinning.Folder);

            Assert.Equal("the_sluice", only.Id);
            Assert.Equal("the_sluice", pinning.Pinned);
        }

        [Fact]
        public void An_empty_folder_points_at_nothing()
        {
            var pinning = new Pinning();

            pinning.Read(null);

            Assert.Equal(0, pinning.Count);
            Assert.Equal("", pinning.Pinned);
            Assert.Null(pinning.Target);
            Assert.False(pinning.Pin("the_sluice"));
        }

        [Fact]
        public void The_corkboard_can_be_re_pointed_at_anything_you_are_carrying_and_nothing_else()
        {
            var pinning = new Pinning();

            pinning.Read(new[]
            {
                (Errand("the_sluice"), QuestState.Active),
                (Spine("the_tide"), QuestState.Active),
            });

            Assert.True(pinning.Pin("the_tide"));
            Assert.Equal("the_tide", pinning.Pinned);

            // already pointed there
            Assert.False(pinning.Pin("the_tide"));

            // and not at one you have not taken on
            Assert.False(pinning.Pin("the_nets"));
            Assert.Equal("the_tide", pinning.Pinned);
        }

        // THE PIN FALLS OFF BY ITSELF. A quest finished while you were looking at something else
        // takes its own pin out, which is why re-reading the folder is where that is decided.
        [Fact]
        public void Finishing_the_pinned_errand_moves_the_pin_to_what_is_left()
        {
            var pinning = new Pinning();

            pinning.Read(new[]
            {
                (Spine("the_tide"), QuestState.Active),
                (Errand("the_sluice"), QuestState.Active),
            });

            pinning.Pin("the_tide");

            pinning.Read(new[]
            {
                (Spine("the_tide"), QuestState.Done),
                (Errand("the_sluice"), QuestState.Active),
            });

            Assert.Equal("the_sluice", pinning.Pinned);
        }

        [Fact]
        public void Only_a_side_errand_can_be_handed_back()
        {
            var pinning = new Pinning();

            pinning.Read(new[]
            {
                (Errand("the_sluice"), QuestState.Active),
                (Spine("the_tide"), QuestState.Active),
            });

            Assert.True(pinning.CanAbandon("the_sluice"));
            Assert.False(pinning.CanAbandon("the_tide"));

            // and not one you are not carrying at all
            Assert.False(pinning.CanAbandon("the_nets"));
            Assert.False(pinning.CanAbandon(null));

            Assert.Equal(new[] { "the_sluice" }, pinning.Abandonable.Select(q => q.Id).ToArray());
            Assert.Equal(new[] { "the_tide" }, pinning.Spine.Select(q => q.Id).ToArray());
        }

        // a folder with quests in it is never a blank corkboard
        [Fact]
        public void Something_is_always_pinned_while_something_is_carried()
        {
            var pinning = new Pinning();

            pinning.Read(new[] { (Spine("the_tide"), QuestState.Active) });

            Assert.Equal("the_tide", pinning.Pinned);

            pinning.Read(new[] { (Spine("the_tide"), QuestState.Done) });

            Assert.Equal("", pinning.Pinned);
        }

        // THE REAL DERIVATION, over quests built by hand. QuestBook.Log is what the game calls and
        // all it does is ask each quest its state, so asking them directly is the same question
        // without a folder of JSON in the middle - and it is the derivation being tested, not the
        // reader, which is content.tests' business.
        static IEnumerable<(Quest, QuestState)> Log(Facts facts, params Quest[] quests) =>
            quests.Select(q => (q, q.StateIn(facts)));
    }
}
