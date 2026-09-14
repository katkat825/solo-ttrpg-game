using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;

namespace Content.Schema
{
    // THE WORDS A CAMPAIGN IS ALLOWED TO USE, DERIVED FROM THE ENUMS THAT DEFINE THEM.
    //
    // `ARCHITECTURE.md` section 2: the vocabulary is the engine's and which word fires when is the
    // content's. A statblock says `"tier": "rival"` and `"might": "d8"`, and both of those are
    // words the engine already owns - `Tier.Rival` and `Die.D8`. So this reads and writes them and
    // nothing more, and it is DERIVED from the enums exactly as `EngineKeys` is derived from them:
    // adding a sixth Skill makes it authorable the same day, and a schema listing the ten by hand
    // would be a second description of `Traits.cs` free to drift from it.
    //
    // THE ERROR MESSAGE IS THE FEATURE. A campaign author who types "d7" gets told what a die may
    // be, in the file, at the field - because the alternative is a silent Die.None, a pool one die
    // short, and a monster that is mysteriously easy (`SEAMS.md`'s "a check that passes for the
    // wrong reason").
    public static class Vocabulary
    {
        // ---- dice ----

        // "d8". The enum value IS the side count, so this is derived and cannot drift
        public static string NameOf(Die die) => die == Die.None ? "none" : "d" + (int)die;

        public static IReadOnlyList<string> Dice =>
            Enum.GetValues<Die>().Where(d => d != Die.None).Select(NameOf).ToArray();

        public static bool TryDie(string word, out Die die)
        {
            die = Die.None;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim().ToLowerInvariant();

            if (trimmed == "none") return true;

            if (trimmed.Length < 2 || trimmed[0] != 'd') return false;
            if (!int.TryParse(trimmed.Substring(1), out int sides)) return false;
            if (!Enum.IsDefined(typeof(Die), sides)) return false;

            die = (Die)sides;
            return die != Die.None;
        }

        // ---- everything else the enums name ----

        public static string NameOf<TEnum>(TEnum value) where TEnum : struct, Enum =>
            value.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words<TEnum>() where TEnum : struct, Enum =>
            Enum.GetValues<TEnum>().Select(NameOf).ToArray();

        // A NUMBER IS NOT A WORD. Enum.TryParse happily accepts "3" and hands back the third
        // member, which would let a campaign name a Tier by its ordinal - and then a reordering of
        // the enum silently re-tiers every monster in every published campaign. Only the words
        public static bool TryWord<TEnum>(string word, out TEnum value) where TEnum : struct, Enum
        {
            value = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim();

            if (trimmed.Any(char.IsDigit)) return false;

            return Enum.TryParse(trimmed, ignoreCase: true, out value)
                && Enum.IsDefined(typeof(TEnum), value);
        }

        // what to print after "that is not a die" - the list, so nobody has to go and find it
        public static string Offer(IEnumerable<string> words) => string.Join(", ", words);

        public static string Offer<TEnum>() where TEnum : struct, Enum => Offer(Words<TEnum>());
    }
}
