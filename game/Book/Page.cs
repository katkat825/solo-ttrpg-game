using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Book
{
    // THE CAMPAIGN BOOK'S CONTENTS PAGE, WHICH IS THE PAUSE MENU AND IS NOT ONE.
    //
    // Every other game opens a panel here. This turns to the front of the book you are already
    // holding and reads its contents, because a contents page is a thing a book HAS - the object was
    // there before the need for it, which is the test every replaced menu has to pass.
    //
    // In the order they are printed. Closed, because each member is a leaf of real paper with a
    // real line on it, and a new one is a new line to translate.
    public enum Page
    {
        // the re-readable log: everything said and done, and every quest taken and finished
        StorySoFar,

        // back to the bookcase - you close the book and stand up
        Bookcase,

        // the same save call the door makes, on a page instead of at a door
        Save,

        // THE ONE HONESTLY-META PAGE. Volume, text size, the accessibility dials. A plain page in a
        // book is wrapper enough; fictionalising the settings themselves would make them harder to
        // find and no more diegetic
        Settings,

        // WHICH KEY DOES WHAT, and a page of its own rather than the bottom of the settings page -
        // seven acts and eight dials do not fit on one leaf of paper, and a book that needs
        // scrolling is not a book (AX1)
        Keys,

        // what you own and what is on the table tonight - dice, trays (BK6). A page rather than a
        // bag, because the bag is the flavour and the choice is a list of things you have earned
        Loadout,
    }

    public static class Pages
    {
        public static string Word(this Page page) => page switch
        {
            Page.StorySoFar => "story_so_far",
            _ => page.ToString().ToLowerInvariant(),
        };

        public static IReadOnlyList<string> Words => Enum.GetValues<Page>().Select(Word).ToArray();

        public const string Subject = "book_page";

        public static string NameKey(this Page page) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(page));

        public static IEnumerable<string> Keys()
        {
            foreach (Page page in Enum.GetValues<Page>()) yield return NameKey(page);
        }

        // the pages that admit to being about the software rather than about the game. Named so that
        // a third one has to be argued for rather than added
        public static bool IsMeta(this Page page) => page is Page.Settings or Page.Keys;

        // turning to it closes the book and takes you out of the campaign, so it is the one the
        // book asks about if there is anything unwritten
        public static bool Leaves(this Page page) => page == Page.Bookcase;

        public static bool TryWord(string word, out Page page)
        {
            page = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Page one in Enum.GetValues<Page>())
            {
                if (Word(one) != trimmed) continue;

                page = one;
                return true;
            }

            return false;
        }
    }
}
