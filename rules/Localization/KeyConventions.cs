using System;
using System.Collections.Generic;
using System.Linq;

namespace Rules.Localization
{
    // the key grammar, in one place, and there is exactly one of it
    // rules/ emits keys and never a string a player will read
    // keys are code - English lives in a locale file like every other language
    // IsWellFormed enforces the shape below and the tests run every engine key through it
    //
    //     namespace . subject . aspect [ . qualifier ]* [ . index ]
    //
    //   namespace   one of Namespaces - what kind of thing this is
    //   subject     which one - an id, never a display name
    //   aspect      what about it - name, description, title, bark, narration
    //   qualifier   zero or more further narrowings, optional
    //   index       three digits, zero-padded, always last, optional
    //
    // every segment lowercase snake_case, namespaces singular, minimum three segments
    //
    //   actor.rabble.name_numbered             takes {0}
    //   condition.winded.description
    //   dialogue.wolf.bark.snag.017
    //   quest.ashfall.chapter_02.title
    //
    // dialogue sits under a namespace like everything else
    // dialogue.wolf.* is still one contiguous block for a translator
    // and every first segment still means the same kind of thing
    // speakers are keyed by creature, not by class - wolf, not barbarian
    //
    // 1. one key per whole sentence - never assemble one from translated fragments
    // 2. numbers go in as {0}, never concatenated
    // 3. keys are stable ids - renaming breaks every locale file, so deprecate instead
    // 4. never parse or branch on a key's text at runtime
    // 5. indices are three digits from the first line written - renumbering breaks locales
    // 6. translate the personality, not the wording
    // 7. anything a player can read gets a key
    //    DebugName and every ToString() are developer-only and must never reach the screen
    public static class KeyConventions
    {
        // suffixed "Ns" rather than named bare
        // Actor, Condition, Difficulty and Skill are domain types elsewhere in this library
        // shadowing them here would trap whoever next adds a using to this file

        public const string ActorNs = "actor";
        public const string AttrNs = "attr";
        public const string SkillNs = "skill";
        public const string ConditionNs = "condition";
        public const string GearNs = "gear";
        public const string DifficultyNs = "difficulty";
        public const string DialogueNs = "dialogue";
        public const string CombatNs = "combat";
        public const string QuestNs = "quest";
        public const string CampaignNs = "campaign";
        public const string UiNs = "ui";

        // the complete set - a key outside these is malformed, not merely unusual
        public static readonly IReadOnlyCollection<string> Namespaces = new[]
        {
            ActorNs, AttrNs, SkillNs, ConditionNs, GearNs, DifficultyNs,
            DialogueNs, CombatNs, QuestNs, CampaignNs, UiNs,
        };

        // ---- builders. Prefer these over hand-written strings. ----

        public static string Key(string ns, string subject, string aspect, params string[] qualifiers) =>
            string.Join(".", new[] { ns, subject, aspect }.Concat(qualifiers ?? Array.Empty<string>()));

        public static string Indexed(string ns, string subject, string aspect, string qualifier, int index) =>
            $"{ns}.{subject}.{aspect}.{qualifier}.{index:000}";

        public static string ActorName(string id) => Key(ActorNs, id, "name");

        // takes an ordinal as {0}
        public static string ActorNameNumbered(string id) => Key(ActorNs, id, "name_numbered");

        public static string GearName(string id) => Key(GearNs, id, "name");

        public static string Line(string speaker, string aspect, string situation, int index) =>
            Indexed(DialogueNs, speaker, aspect, situation, index);

        // a companion reacting - dialogue.wolf.bark.snag.017
        public static string Bark(string speaker, string situation, int index) =>
            Line(speaker, "bark", situation, index);

        // ---- enforcement ----

        static bool IsSegment(string s) =>
            s.Length > 0 && s.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');

        static bool IsIndex(string s) => s.Length == 3 && s.All(char.IsDigit);

        // run by the tests against every key the engine can produce
        // so a malformed key fails there rather than shipping
        public static bool IsWellFormed(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            if (key != key.ToLowerInvariant()) return false;

            string[] parts = key.Split('.');
            if (parts.Length < 3) return false;
            if (!Namespaces.Contains(parts[0])) return false;
            if (!parts.All(IsSegment)) return false;

            // an index may only ever be the last segment
            for (int i = 0; i < parts.Length - 1; i++)
                if (IsIndex(parts[i])) return false;

            return true;
        }

        // why a key is malformed, for a test failure that explains itself
        public static string Explain(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "key is empty";
            if (key != key.ToLowerInvariant()) return $"'{key}' is not lowercase";

            string[] parts = key.Split('.');
            if (parts.Length < 3) return $"'{key}' has {parts.Length} segments; the grammar needs at least 3";
            if (!Namespaces.Contains(parts[0]))
                return $"'{parts[0]}' is not a known namespace ({string.Join(", ", Namespaces)})";

            string bad = parts.FirstOrDefault(p => !IsSegment(p));
            if (bad != null) return $"segment '{bad}' must be lowercase a-z, 0-9 and underscore only";

            for (int i = 0; i < parts.Length - 1; i++)
                if (IsIndex(parts[i])) return $"'{key}' has an index at position {i}; indices go last";

            return "well formed";
        }
    }
}
