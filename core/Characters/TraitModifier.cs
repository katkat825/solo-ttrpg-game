namespace Core.Characters
{
    // steps, not a die size, so modifiers compose order-independently
    public readonly struct TraitModifier
    {
        public ModifierSource Source { get; }

        public Attr Attribute { get; }

        public int Steps { get; }

        public TraitModifier(ModifierSource source, Attr attribute, int steps)
        {
            Source = source;
            Attribute = attribute;
            Steps = steps;
        }

        // debug only, never localized - keep it off the screen
        public override string ToString() => $"{Source} {Attribute} {Steps:+0;-0;0}";
    }
}
