using System;
using System.Collections.Generic;
using System.Linq;
using Content.Saves;
using Core.Localization;

namespace Game.Book
{
    // THE RELOAD TABS (BK4).
    //
    // RELOADING IS ALLOWED, ON PURPOSE. Persistence means the world remembers and reacts; it does
    // not mean the player is trapped. Autosave already accumulates snapshots rather than
    // overwriting one - this is the face of that folder, and the reason the folder was built to
    // accumulate.
    //
    // FIVE TABS, AND A SIXTH THAT IS "SHOW MORE". Five because a book has tabs down its edge and
    // a book with forty is a filing cabinet; show-more rather than a scrollbar for the same
    // reason. Turning the pages back is a thing you do with a thumb.
    //
    // Sorts itself newest-first rather than trusting the caller to have done it: SaveShelf does
    // sort, and a view that depends on that is a view that breaks the first time somebody hands it
    // a filtered list.
    public sealed class Tabs
    {
        // down the edge of the book at once
        public const int Reachable = 5;

        // and the next handful behind the sixth tab
        public const int Behind = 5;

        readonly List<Box> _boxes;

        int _shown = Reachable;

        public Tabs(IEnumerable<Box> boxes)
        {
            _boxes = (boxes ?? Enumerable.Empty<Box>())
                     .Where(b => b != null)
                     .OrderByDescending(b => b.Written)
                     .ThenBy(b => b.File, StringComparer.Ordinal)
                     .ToList();
        }

        public int Count => _boxes.Count;

        public IReadOnlyList<Box> Showing => _boxes.Take(_shown).ToArray();

        // the sixth tab. Absent when there is nothing behind it, because a control that does
        // nothing is worse than no control
        public bool More => _boxes.Count > _shown;

        public int Hidden => Math.Max(0, _boxes.Count - _shown);

        public bool ShowMore()
        {
            if (!More) return false;

            _shown += Behind;

            return true;
        }

        // back to five, which is what closing the book and opening it again gives you
        public void Thumb() => _shown = Reachable;

        public Box Turn(string file) =>
            file == null
                ? null
                : Showing.FirstOrDefault(b => string.Equals(b.File, file, StringComparison.Ordinal));

        // only the saves for one campaign: a tab in THIS book, not in the bookcase
        public static Tabs Of(SaveShelf shelf, string campaign) =>
            new Tabs((shelf?.Boxes ?? (IReadOnlyList<Box>)Array.Empty<Box>())
                     .Where(b => campaign == null ||
                                 string.Equals(b.Campaign, campaign, StringComparison.Ordinal)));

        public const string Subject = "save_tab";

        // the sixth tab's own word, and the only string the tabs need: the rest are labelled by the
        // save they turn to
        public static string MoreKey =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, "more");

        public static IEnumerable<string> Keys()
        {
            yield return MoreKey;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            Count == 0
                ? "no saves to turn back to"
                : $"{Showing.Count} of {Count} tab(s)" + (More ? $", {Hidden} behind show-more" : "");
    }
}
