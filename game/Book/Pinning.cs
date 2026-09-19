using System;
using System.Collections.Generic;
using System.Linq;
using Content.Quests;

namespace Game.Book
{
    // WHICH ERRAND YOU ARE POINTED AT, AND WHAT MAY BE HANDED BACK (BK4).
    //
    // THE SPLIT THIS IMPLEMENTS. The corkboard is the live glance: a quests folder of the errands
    // you are carrying, and the one you are pointed at, re-pointed right there without opening
    // anything. The book is the archive and the management. So the pin lives here, one object, read
    // by both - rather than a corkboard state and a book state that could disagree about which
    // quest you are on.
    //
    // NOTHING IS STORED. The folder is QuestBook.Log read again every time, so murdering the man
    // who set the errand empties it, exactly as it empties the quest log. The pin is the one piece
    // of state, it is a preference rather than a fact, and it falls off the board by itself when the
    // errand it pointed at is finished.
    //
    // The abandon itself is Exploring.Abandon: clearing '<quest>.accepted' has to rebuild the place,
    // and this object has no world to rebuild. What lives here is which errands MAY be handed back,
    // which is a question about the quests and not about the world.
    public sealed class Pinning
    {
        readonly List<Quest> _folder = new List<Quest>();

        string _pinned = "";

        // the accepted-but-unfinished errands, in the order the book prints them
        public IReadOnlyList<Quest> Folder => _folder;

        public int Count => _folder.Count;

        // the current target. Empty when there is nothing to be pointed at
        public string Pinned => _pinned;

        public Quest Target => Of(_pinned);

        // READ THE FOLDER AGAIN. Called on every change to the fact store, which is why the pin is
        // re-checked here rather than anywhere that has to remember to: a quest finished while you
        // were looking at something else takes its own pin out.
        public void Read(IEnumerable<(Quest Quest, QuestState State)> log)
        {
            _folder.Clear();

            foreach ((Quest quest, QuestState state) in log ?? Enumerable.Empty<(Quest, QuestState)>())
                if (state == QuestState.Active)
                    _folder.Add(quest);

            if (Of(_pinned) == null) _pinned = "";

            // pointed at the first thing you are carrying, so the corkboard is never blank while
            // there is an errand on it
            if (_pinned.Length == 0 && _folder.Count > 0) _pinned = _folder[0].Id;
        }

        public bool Carrying(string quest) => Of(quest) != null;

        public bool Pin(string quest)
        {
            if (!Carrying(quest) || string.Equals(_pinned, quest, StringComparison.Ordinal))
                return false;

            _pinned = quest;

            return true;
        }

        // MAIN QUESTS CANNOT BE HANDED BACK. A quest on from the moment the campaign opened is the
        // story you are in rather than an errand somebody set, so there is nobody to give it back
        // to - Quest.IsSide is where that is derived and why it needs no new field.
        public bool CanAbandon(string quest)
        {
            Quest one = Of(quest);

            return one != null && one.IsSide;
        }

        // the ones the book prints a hand-it-back line beside
        public IEnumerable<Quest> Abandonable => _folder.Where(q => q.IsSide);

        // a main quest in the folder, which is the story rather than an errand
        public IEnumerable<Quest> Spine => _folder.Where(q => !q.IsSide);

        Quest Of(string quest) =>
            string.IsNullOrEmpty(quest)
                ? null
                : _folder.FirstOrDefault(q => string.Equals(q.Id, quest, StringComparison.Ordinal));

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Count == 0
                ? "nothing in the folder"
                : $"{Count} errand(s), pointed at " +
                  (_pinned.Length > 0 ? _pinned : "nothing");
    }
}
