using System;
using System.Collections.Generic;
using System.Linq;
using Content.Entities;
using Content.Quests;
using Content.World;

namespace Game.Book
{
    // THE RE-READABLE LOG (BK4).
    //
    // The bubbles at the table fade on purpose - a line you have to read before it goes is a line
    // you look AT rather than skim. This is where they went. It is one page of one book, and it is
    // what makes letting them fade safe.
    //
    // THE SPLIT WITH THE CORKBOARD. The corkboard is the live glance - the quests you are carrying
    // and the one you are pointed at, re-pointed right there. The book is the archive and the
    // management. Neither is a copy of the other: the corkboard shows Pinning, this shows the log.
    //
    // TWO HALVES, AND THEY ARE REMEMBERED DIFFERENTLY ON PURPOSE.
    //
    //   WHAT WAS SAID AND DONE is appended as it happens and belongs to this sitting. BK does not
    //   change the save format - "the save data underneath does not change; its face does" - so a
    //   transcript is not written into a save, and it would be the wrong thing to write anyway:
    //   the save is small because it holds facts rather than history (PLACES_AND_PERSISTENCE).
    //
    //   THE QUEST LEDGER is DERIVED from the fact store every time it is asked, exactly as the
    //   quest log always has been. So it survives a reload, it cannot disagree with the world, and
    //   murdering the man who set the errand changes what the book says about it - which is the
    //   whole point of deriving quest state rather than storing it.
    public sealed class StorySoFar
    {
        readonly List<Entry> _said = new List<Entry>();

        public IReadOnlyList<Entry> Sitting => _said;

        public int Count => _said.Count;

        public bool NothingYet => _said.Count == 0;

        public Entry Said(string speaker, string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            var entry = new Entry(Recorded.Said, speaker ?? "", line);

            _said.Add(entry);

            return entry;
        }

        // ONE OF THE FIVE VERBS, AIMED AT SOMETHING WITH A NAME. The name arrives as a key rather
        // than as words, because the log is re-read in whatever language the game is in now - and
        // it is the THING's key rather than the verb card's, since a card says "Search it" and a log
        // says "You searched the lockbox".
        public Entry Did(Interaction verb, string subject, string named)
        {
            if (string.IsNullOrWhiteSpace(named)) return null;

            var entry = new Entry(Recorded.Did, subject ?? "", named, verb.Reading());

            _said.Add(entry);

            return entry;
        }

        public void Forget() => _said.Clear();


        // THE LEDGER, DERIVED. The log is handed in rather than a quest book and a fact store,
        // because deriving it is QuestBook.Log's job and doing it twice would be two answers to
        // "what have you done". Nothing is stored, so asking again after a murder gives a different
        // answer - which is by design and is the same sentence QuestBook.Log carries.
        public static IEnumerable<Entry> Ledger(IEnumerable<(Quest Quest, QuestState State)> log,
                                                string campaign)
        {
            foreach ((Quest quest, QuestState state) in
                     log ?? Enumerable.Empty<(Quest, QuestState)>())
            {
                Recorded? what = Of(state);

                if (what == null) continue;

                yield return new Entry(what.Value, quest.Id, quest.TitleKey(campaign ?? ""));
            }
        }

        // an offered quest is not news and an unknown one is not even that; the ledger is what
        // HAPPENED, and being asked is not a thing that happened to the story
        static Recorded? Of(QuestState state) => state switch
        {
            QuestState.Active => Recorded.Accepted,
            QuestState.Done => Recorded.Completed,
            QuestState.Failed => Recorded.Failed,
            _ => null,
        };

        // the whole page: the ledger first, because it is what the story IS, then the sitting's
        // own speech and actions in the order they happened
        public IEnumerable<Entry> Read(IEnumerable<(Quest Quest, QuestState State)> log,
                                       string campaign) =>
            Ledger(log, campaign).Concat(_said);

        public bool IsEmpty(IEnumerable<(Quest Quest, QuestState State)> log, string campaign) =>
            !Read(log, campaign).Any();

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            NothingYet
                ? "nothing said or done this sitting"
                : $"{_said.Count} thing(s) said and done this sitting";
    }
}
