using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Book
{
    // EVERY WORD PRINTED IN OR ON A BOOK, derived from the enums rather than listed.
    //
    // The books are the engine's - a campaign is read in the same binding whichever campaign it is -
    // so these are ui.* and live in game/locale/ beside the words on the character sheet. What is
    // printed INSIDE a campaign book is that campaign's own, keyed under campaign.* and quest.* by
    // its own locale, which is why the title on the spine is not here.
    //
    // Derived, never listed: add a page to the book, a chapter to the rules or a blank object to the
    // bookcase and the locale audit fails until somebody has written the line for it.
    public static class BookKeys
    {
        public static IEnumerable<string> All()
        {
            // the two books, their contents pages and the rules chapters
            foreach (string key in Contents.Keys()) yield return key;

            // the bookcase itself, and the help button that hands the rules over
            yield return BookcaseName;

            yield return Continue;

            yield return HelpName;

            yield return NewCharacter;

            // the story so far: how a line of it reads, and what an empty page says
            foreach (string key in Recordings.Keys()) yield return key;

            // the sixth tab
            foreach (string key in Tabs.Keys()) yield return key;

            // the blank objects that are the Workshop door
            foreach (string key in Starters.Keys()) yield return key;

            // the headings on the one honestly-meta page
            foreach (string key in Game.Book.Settings.Keys()) yield return key;
        }

        public static string BookcaseName =>
            KeyConventions.Key(KeyConventions.UiNs, "bookcase", "name");

        // the launch's own offer, and the only thing it asks: there is no prompt, the book is
        // already open at it
        public static string Continue =>
            KeyConventions.Key(KeyConventions.UiNs, "bookcase", "continue");

        // the "?" on the table. An object, so it has a name like every other object
        public static string HelpName => KeyConventions.Key(KeyConventions.UiNs, "help", "name");

        // the blank ribbon in a campaign book (BK5)
        public static string NewCharacter =>
            KeyConventions.Key(KeyConventions.UiNs, "new_character", "name");

        // THE KEYS THAT COUNT A NUMBER OR A NAME IN, so a translation that dropped the placeholder
        // reads as a deliberate choice rather than as a bug. Every line of the story so far names
        // the thing it is about, and the sixth tab counts what is behind it - derived from the same
        // list the keys come from, so a line added to the log cannot quietly stop being checked.
        public static bool TakesAnArgument(string key) =>
            key == Tabs.MoreKey ||
            Settings.TakesAnArgument(key) ||
            Recordings.Keys().Contains(key) && key != Recordings.Empty;
    }
}
