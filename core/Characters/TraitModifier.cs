namespace Core.Characters
{
    // one source moving one attribute some number of places along the die ladder
    //
    // negative is down. a Condition is -1, and that is the only shape the engine builds today -
    // feats, gear and Strain are the callers this exists for, and they arrive in Phases P and R
    //
    // steps and not a die size: two modifiers have to be able to compose without either of them
    // knowing what the base is, which is what keeps the result independent of the order they
    // were applied in (CORE_RULES.md section 9, "Saturation")
    public readonly struct TraitModifier
    {
        public ModifierSource Source { get; }

        public Attr Attribute { get; }

        // places along the ladder - negative down, positive up, zero is legal and inert
        public int Steps { get; }

        public TraitModifier(ModifierSource source, Attr attribute, int steps)
        {
            Source = source;
            Attribute = attribute;
            Steps = steps;
        }

        // DEVELOPER ONLY - not localized, never shown to a player
        public override string ToString() => $"{Source} {Attribute} {Steps:+0;-0;0}";
    }
}
