using System;
using System.Collections.Generic;
using System.Linq;
using Content.Saves;
using Core.Localization;

namespace Game.Book
{
    // A CAMPAIGN IS A BOOK (BK1). R4 stood a boxed game on a shelf, and a box is what a board game
    // comes in rather than what a campaign IS - you do not read a box, and everything this game
    // does with a campaign (open it, go back through it, find your place) is what you do with a
    // book.
    //
    // NOTHING UNDER THIS CHANGED. The versioned JSON P6 writes is still the whole of what is
    // remembered, SaveShelf and Box are untouched, and a volume holds no state of its own: it is
    // the saves for one campaign, grouped by whose they are. What changed is its face.
    //
    // An INSTALLED campaign with no saves is a book too - a closed one you have never opened. That
    // is the difference between a bookcase and a save folder, and it is why this takes the loader's
    // list as well as the shelf's.
    public sealed class Volume
    {
        // FIVE CHARACTERS PER CAMPAIGN (BK5). A number rather than a limit dressed as one: the
        // sixth ribbon is blank, and when five are written there is no sixth.
        public const int Most = 5;

        public Volume(string campaign, bool installed, IEnumerable<Bookmark> bookmarks)
        {
            Campaign = campaign ?? "";
            Installed = installed;

            Bookmarks = (bookmarks ?? Enumerable.Empty<Bookmark>())
                        .Where(b => b != null && !b.IsBlank)
                        .OrderByDescending(b => b.Written)
                        .ThenBy(b => b.Who, StringComparer.Ordinal)
                        .Take(Most)
                        .ToArray();
        }

        public string Campaign { get; }

        // whether the pack is on the loader's roots. A save whose campaign is not installed is a
        // book you can see and cannot read - it says so rather than failing when you open it
        public bool Installed { get; }

        // newest first, which is the order somebody reaches for them in
        public IReadOnlyList<Bookmark> Bookmarks { get; }

        public bool Unopened => Bookmarks.Count == 0;

        public bool RoomForAnother => Bookmarks.Count < Most;

        public Bookmark Latest => Bookmarks.FirstOrDefault();

        // the trophy goes on the book, not beside it - it belongs to that campaign (R4)
        public bool Finished => Bookmarks.Any(b => b.Finished);

        // every ribbon a bookcase stands up: the characters, and a blank one while there is room
        public IEnumerable<Bookmark> Ribbons
        {
            get
            {
                foreach (Bookmark one in Bookmarks) yield return one;

                if (RoomForAnother) yield return Bookmark.Blank();
            }
        }

        public Bookmark Whose(string who) =>
            who == null
                ? null
                : Bookmarks.FirstOrDefault(b => string.Equals(b.Who, who, StringComparison.Ordinal));

        // the campaign's own name, in the campaign's own locale - the title on the spine. A book's
        // title is content, exactly as its monsters' names are
        public string TitleKey =>
            Campaign.Length == 0
                ? ""
                : KeyConventions.Key(KeyConventions.CampaignNs, Campaign, "name");

        public string DescriptionKey =>
            Campaign.Length == 0
                ? ""
                : KeyConventions.Key(KeyConventions.CampaignNs, Campaign, "description");


        // GROUPED BY WHOSE GAME IT IS. The newest save under a name is that character's place, so
        // a playthrough with forty autosaves in it is one ribbon rather than forty.
        public static Volume Of(string campaign, bool installed, IEnumerable<Box> boxes)
        {
            var newest = new Dictionary<string, Box>(StringComparer.Ordinal);

            foreach (Box box in boxes ?? Enumerable.Empty<Box>())
            {
                if (box == null) continue;

                string who = box.Whose ?? "";

                if (!newest.TryGetValue(who, out Box standing) || box.Written > standing.Written)
                    newest[who] = box;
            }

            return new Volume(campaign, installed,
                              newest.Select(pair => new Bookmark(pair.Key, pair.Value)));
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{Campaign}" +
            (Installed ? "" : " - NOT INSTALLED") +
            (Unopened ? ", never opened"
                      : $", {Bookmarks.Count} of {Most} character(s)") +
            (Finished ? ", finished" : "");
    }
}
