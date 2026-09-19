using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Book
{
    // THE RULES BOOK'S CHAPTERS.
    //
    // One chapter per thing a player at a real table would lean over and ask about mid-turn. It is
    // deliberately short: a reference somebody actually opens is one they can find the answer in,
    // and the rules themselves fit on a few pages, which was always the design.
    //
    // Each chapter is a title and a body, both keys, both in game/locale/ - the rules belong to
    // the engine and are the same in every campaign, which is exactly what ui.* means here.
    public enum Reference
    {
        // attribute + skill + gear, up to three dice, sum the best two
        Pool,

        // the largest unused die, and it explodes
        Impact,

        // one 1 is a cue to speak; two or more is a consequence
        Snag,

        // two actions a round for you, one for ordinary foes
        Actions,

        // damage steps a die down; the four conditions and what each shrinks
        Conditions,

        // the pips on the sheet: what spends them and what gives them back
        Nerve,

        // the five things you may do to what is in front of you, out of a fight
        Verbs,
    }

    public static class References
    {
        public static string Word(this Reference chapter) => chapter.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words =>
            Enum.GetValues<Reference>().Select(Word).ToArray();

        public const string Subject = "rules";

        public static string TitleKey(this Reference chapter) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(chapter), "title");

        // one key for the whole chapter, not a key per sentence: a rules paragraph read in another
        // language is rewritten, not reassembled, and a translator needs the whole of it at once
        public static string BodyKey(this Reference chapter) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(chapter), "body");

        public static IEnumerable<string> Keys()
        {
            foreach (Reference chapter in Enum.GetValues<Reference>())
            {
                yield return TitleKey(chapter);
                yield return BodyKey(chapter);
            }
        }

        public static bool TryWord(string word, out Reference chapter)
        {
            chapter = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            foreach (Reference one in Enum.GetValues<Reference>())
            {
                if (Word(one) != trimmed) continue;

                chapter = one;
                return true;
            }

            return false;
        }
    }
}
