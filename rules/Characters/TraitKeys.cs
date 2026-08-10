using Rules.Localization;

namespace Rules.Characters
{
    // localization keys for the trait enums
    // lives beside the enums it names so the two cannot drift apart
    // every key goes through KeyConventions, so the grammar holds by construction
    public static class TraitKeys
    {
        public static string Key(this Attr a) =>
            KeyConventions.Key(KeyConventions.AttrNs, a.ToString().ToLowerInvariant(), "name");

        public static string DescriptionKey(this Attr a) =>
            KeyConventions.Key(KeyConventions.AttrNs, a.ToString().ToLowerInvariant(), "description");

        public static string Key(this Skill s) =>
            KeyConventions.Key(KeyConventions.SkillNs, s.ToString().ToLowerInvariant(), "name");

        public static string DescriptionKey(this Skill s) =>
            KeyConventions.Key(KeyConventions.SkillNs, s.ToString().ToLowerInvariant(), "description");

        public static string Key(this Condition c) =>
            KeyConventions.Key(KeyConventions.ConditionNs, c.ToString().ToLowerInvariant(), "name");

        public static string DescriptionKey(this Condition c) =>
            KeyConventions.Key(KeyConventions.ConditionNs, c.ToString().ToLowerInvariant(), "description");

        // tiers are developer-facing today, but keyed like anything else
        public static string Key(this Tier t) =>
            KeyConventions.Key(KeyConventions.ActorNs, "tier_" + t.ToString().ToLowerInvariant(), "name");
    }
}
