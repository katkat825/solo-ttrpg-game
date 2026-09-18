using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Dialogue
{
    // what the table just did, in the companion's hearing. Closed: a situation the engine cannot
    // raise is a bark nothing would ever say, and an open string here would be a scripting hook.
    public enum Bark
    {
        // exactly one 1 - 33% of early rolls, and the reason this whole system exists
        Snag,

        // two or more, a real consequence
        Trouble,

        // one die on its top face - the mirror of a Snag, and about as common
        Maxed,

        // every die on its top face. The rare one, and the reason Maxed is not enough on its own
        Perfect,

        // a die landed cocked and the companion is nosing it flat. The throw you made is kept
        Nudge,

        // walked into a place where a quest you accepted can be moved on
        Nearby,

        // a Nerve spent, or committed; the companion has an opinion about being pushed
        Nerve,

        // a rattle behind the screen, and a pause
        Secret,

        // asked for a hint once too often; the fiend charges more, the hound gets anxious
        Overasked,

        // the fire is lit and there is nothing to fight
        Camp,

        Victory,

        // the hero is on the floor. This is where a companion goes quiet, so most banks are short
        Down,
    }

    public static class Barks
    {
        public static string Word(this Bark situation) => situation.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words =>
            Enum.GetValues<Bark>().Select(Word).ToArray();

        public static bool TryWord(string word, out Bark situation)
        {
            situation = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim();

            // digits would let Enum.TryParse take "3" as the third member, and a reorder would re-file every bank
            if (trimmed.Any(char.IsDigit)) return false;

            return Enum.TryParse(trimmed, ignoreCase: true, out situation)
                && Enum.IsDefined(typeof(Bark), situation);
        }
    }
}
