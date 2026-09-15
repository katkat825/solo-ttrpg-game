namespace Core.Characters
{
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

        // the reverse of Affects, derived so a new Condition needs no second table
        public static Condition Pressing(this Attr a)
        {
            foreach (Condition c in System.Enum.GetValues<Condition>())
                if (c.Affects() == a) return c;

            return Condition.Winded;
        }
    }

    public enum Tier
    {
        Rabble,
        Rival,
        Dread
    }
}
