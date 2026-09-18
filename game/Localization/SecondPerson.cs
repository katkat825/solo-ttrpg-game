using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Game.Localization
{
    // THE GAME NEVER GENDER-REFERENCES THE PLAYER.
    //
    // The player picks no gender and no pronouns; the DM and the companion address YOU. That is
    // cleaner than pronoun substitution and it dodges a trap the key grammar creates - many
    // languages inflect around gender well past a swapped word, and one-key-per-whole-sentence
    // cannot assemble those from fragments.
    //
    // WHY THIS IS NOT A GREP FOR "he". NPCs are gendered, legitimately and constantly - Norrel is a
    // he, and a campaign that could not say so would be a campaign with no people in it. A naive
    // scan over a locale file finds forty true sentences and one real bug, and a check that cries
    // wolf is a check nobody runs. So the file is read in two scopes:
    //
    //   SPOKEN TO YOU - the engine's own strings, the throw read back in a voice, and a companion's
    //   barks. There is nobody else in these: the subject is you, the dice, or the thing in front
    //   of you, and a monster is an "it". A gendered word here has no innocent reading, so any of
    //   them is a problem.
    //
    //   EVERYWHERE ELSE - conversation lines, the DM's narration, quest text, names. Here a third
    //   person is usually a real third person, so pronouns are left alone entirely and only a
    //   VOCATIVE is flagged: "sir", "my lady", "lad" can only be aimed at whoever is being spoken
    //   to, and the only person anybody in this game speaks to is the player. The one honest false
    //   positive - a guard calling a duke "my lord" - is what the allow-list is for.
    //
    // ENGLISH ONLY, AND IT SAYS SO. The word lists below are English, so this holds the English
    // column and nothing else. That is not a gap to be embarrassed about: a translator works from
    // English that is already second-person, and a locale that has gone wrong in Polish needs a
    // Polish reader, not a longer regex.
    public static class SecondPerson
    {
        // said to you and to nobody else: there is no third party in any of these to be a he
        public static bool SpokenToYou(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;

            if (key.StartsWith("combat.", StringComparison.Ordinal)) return true;
            if (key.StartsWith("ui.", StringComparison.Ordinal)) return true;

            // dialogue.<speaker>.bark.* and dialogue.<speaker>.readout.*; a line, a beat and a
            // narration are somebody talking about the world and are not this
            string[] segments = key.Split('.');

            return segments.Length >= 3 && segments[0] == "dialogue" &&
                   (segments[2] == "bark" || segments[2] == "readout");
        }

        // he, she and their cases. Whole words: "his" is in "history" and "her" is in "there"
        public static readonly IReadOnlyList<string> Pronouns = new[]
        {
            "he", "him", "his", "himself",
            "she", "her", "hers", "herself",
        };

        // terms of address. Whoever is addressed is the listener, and the listener is you
        public static readonly IReadOnlyList<string> Vocatives = new[]
        {
            "sir", "madam", "ma'am", "milord", "milady",
            "lad", "lass", "mister", "mistress", "missus",
            "gentleman", "gentlemen", "lord", "lady",
            "boy", "girl", "son", "daughter",
        };

        // what is not allowed to appear in a line with this key. Ordered so the report names the
        // narrower fault first where a word is on both lists
        public static IEnumerable<string> ForbiddenIn(string key) =>
            SpokenToYou(key) ? Both() : Vocatives;

        static IEnumerable<string> Both()
        {
            foreach (string word in Vocatives) yield return word;

            foreach (string word in Pronouns) yield return word;
        }

        // the words this line uses that its key is not allowed to use, in the order they are read
        public static IReadOnlyList<string> Faults(string key, string english)
        {
            var found = new List<string>();

            if (string.IsNullOrWhiteSpace(english)) return found;

            foreach (string word in ForbiddenIn(key))
                if (Says(english, word) && !found.Contains(word, StringComparer.Ordinal))
                    found.Add(word);

            return found;
        }

        // a whole word, case-insensitively, with an apostrophe counting as part of one so that
        // "ma'am" matches and "her" does not match inside "hers"
        public static bool Says(string english, string word) =>
            !string.IsNullOrEmpty(english) &&
            Regex.IsMatch(english, @"(?<![\w'])" + Regex.Escape(word) + @"(?![\w'])",
                          RegexOptions.IgnoreCase);

        // why this line is wrong, in the words of whoever has to fix it
        public static string Explain(string key, string word) =>
            SpokenToYou(key)
                ? $"'{word}' is said straight to the player here - this key is the game talking to " +
                  "you, and the game addresses you in the second person and never gives you a gender"
                : $"'{word}' is a term of address, and whoever is being addressed in this game is " +
                  "the player - if it is aimed at somebody else in the fiction, list the key in " +
                  AllowFile;

        // one key per line, '#' for a comment. Beside the strings it excuses, so the excuse is
        // read by whoever is reading them
        public const string AllowFile = "gendered.allowed";
    }
}
