using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;

namespace Content.Schema
{
    public static class Vocabulary
    {

        // the enum value is the side count, so "d8" is derived and can't drift
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


        public static string NameOf<TEnum>(TEnum value) where TEnum : struct, Enum =>
            value.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words<TEnum>() where TEnum : struct, Enum =>
            Enum.GetValues<TEnum>().Select(NameOf).ToArray();

        // reject digits: Enum.TryParse accepts "3" as the third member, and a reorder would silently re-tier every monster
        public static bool TryWord<TEnum>(string word, out TEnum value) where TEnum : struct, Enum
        {
            value = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim();

            if (trimmed.Any(char.IsDigit)) return false;

            return Enum.TryParse(trimmed, ignoreCase: true, out value)
                && Enum.IsDefined(typeof(TEnum), value);
        }

        public static string Offer(IEnumerable<string> words) => string.Join(", ", words);

        public static string Offer<TEnum>() where TEnum : struct, Enum => Offer(Words<TEnum>());
    }
}
