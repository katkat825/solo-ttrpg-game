using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Localization
{
    // shape: namespace.subject.aspect[.qualifier]*[.index]; one key = one whole sentence
    public static class KeyConventions
    {
        // suffixed Ns so they don't shadow the Actor/Condition/Skill domain types

        public const string ActorNs = "actor";
        public const string AttrNs = "attr";
        public const string SkillNs = "skill";
        public const string ConditionNs = "condition";
        public const string GearNs = "gear";

        public const string MiniNs = "mini";

        public const string ClassNs = "class";

        public const string AbilityNs = "ability";

        public const string DifficultyNs = "difficulty";
        public const string DialogueNs = "dialogue";
        public const string CombatNs = "combat";
        public const string QuestNs = "quest";
        public const string CampaignNs = "campaign";
        public const string UiNs = "ui";

        public static readonly IReadOnlyCollection<string> Namespaces = new[]
        {
            ActorNs, AttrNs, SkillNs, ConditionNs, GearNs, DifficultyNs,
            DialogueNs, CombatNs, QuestNs, CampaignNs, UiNs, MiniNs, ClassNs, AbilityNs,
        };


        public static string Key(string ns, string subject, string aspect, params string[] qualifiers) =>
            string.Join(".", new[] { ns, subject, aspect }.Concat(qualifiers ?? Array.Empty<string>()));

        public static string Indexed(string ns, string subject, string aspect, string qualifier, int index) =>
            $"{ns}.{subject}.{aspect}.{qualifier}.{index:000}";

        public static string ActorName(string id) => Key(ActorNs, id, "name");

        public static string ActorNameNumbered(string id) => Key(ActorNs, id, "name_numbered");

        public static string GearName(string id) => Key(GearNs, id, "name");

        public static string MiniName(string id) => Key(MiniNs, id, "name");

        public static string ClassName(string id) => Key(ClassNs, id, "name");

        public static string ClassDescription(string id) => Key(ClassNs, id, "description");

        public static string AbilityName(string id) => Key(AbilityNs, id, "name");

        public static string AbilityDescription(string id) => Key(AbilityNs, id, "description");

        public static string DefaultImpactName => Key(CombatNs, "impact", "name");

        public static string Line(string speaker, string aspect, string situation, int index) =>
            Indexed(DialogueNs, speaker, aspect, situation, index);

        public static string Bark(string speaker, string situation, int index) =>
            Line(speaker, "bark", situation, index);


        static bool IsSegment(string s) =>
            s.Length > 0 && s.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');

        static bool IsIndex(string s) => s.Length == 3 && s.All(char.IsDigit);

        public const string WellFormed = "well formed";

        // delegates to Explain so the two can't drift into copies that disagree
        public static bool IsWellFormed(string key) => Explain(key) == WellFormed;

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

            return WellFormed;
        }
    }
}
