using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Book
{
    // THE TWO BOOKS AT THIS TABLE.
    //
    // A campaign is a book you pick up and open, and the rules are a book beside it - a PEER, not
    // a help overlay laid over the game. Same binding, same gesture, standing on the same shelf
    // and on the same bookcase. That peerage is the whole reason this is an enum rather than one
    // book class with a special case for help: a special case is how a rules book ends up being
    // opened by a different motion from the campaign, and a different motion is a menu.
    public enum Tome
    {
        // the campaign you are playing. Its contents page is the pause menu, and is not one,
        // because a book's contents page is a page in a book
        Campaign,

        // the rules and reference, handed over by the DM when you press the "?" (BK3)
        Rules,
    }

    public static class Tomes
    {
        public static string Word(this Tome tome) => tome switch
        {
            Tome.Rules => "rules_book",
            _ => "book",
        };

        public static IReadOnlyList<string> Words => Enum.GetValues<Tome>().Select(Word).ToArray();

        // ui.*, because both books are the engine's: every campaign is read in the same binding,
        // and what is printed INSIDE a campaign book is that campaign's own
        public static string NameKey(this Tome tome) =>
            KeyConventions.Key(KeyConventions.UiNs, Word(tome), "name");

        public static IEnumerable<string> Keys()
        {
            foreach (Tome tome in Enum.GetValues<Tome>()) yield return NameKey(tome);
        }

        public static bool TryWord(string word, out Tome tome)
        {
            tome = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Tome one in Enum.GetValues<Tome>())
            {
                if (Word(one) != trimmed) continue;

                tome = one;
                return true;
            }

            return false;
        }
    }
}
