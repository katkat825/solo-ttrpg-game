namespace Core.Characters
{
    // the closed vocabulary a character is described in
    // attributes and skills are rated as dice, conditions step one down a size
    // adding to any of these enums multiplies content and needs a locale string
    // EngineKeys derives the key list from them, so nothing here is free
    public enum Attr { Might, Grace, Wits, Heart }

    public enum Skill
    {
        None,
        Blades, Marksman, Brawl, Stealth, Larceny,
        Lore, Survival, Insight, Sway, Channeling
    }

    public enum Condition { Winded, Reeling, Rattled, Shaken }

    public static class ConditionRules
    {
        public static Attr Affects(this Condition c) => c switch
        {
            Condition.Winded => Attr.Might,
            Condition.Reeling => Attr.Grace,
            Condition.Rattled => Attr.Wits,
            Condition.Shaken => Attr.Heart,
            _ => Attr.Might
        };

        // AND THE SAME MAPPING READ THE OTHER WAY - which Condition presses on this attribute.
        // C4's Trouble needs it: over-committing with Might leaves you Winded, and the pairing
        // must not be written down twice for the two directions to disagree over. DERIVED, so
        // adding a fifth Condition needs no edit here (the EngineKeys rule, applied to a lookup)
        public static Condition Pressing(this Attr a)
        {
            foreach (Condition c in System.Enum.GetValues<Condition>())
                if (c.Affects() == a) return c;

            return Condition.Winded;
        }
    }

    public enum Tier
    {
        // no health track - any successful hit removes one
        Rabble,
        Rival,
        Dread
    }
}
