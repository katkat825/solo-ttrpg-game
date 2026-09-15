using System;

namespace Core.Characters
{
    // an id, never display text - it's saved and matched on, so renaming breaks saves
    public enum ModifierKind
    {
        Condition,

        Gear,

        Effect,
    }

    public readonly struct ModifierSource : IEquatable<ModifierSource>
    {
        public ModifierKind Kind { get; }

        public string Id { get; }

        ModifierSource(ModifierKind kind, string id)
        {
            Kind = kind;
            Id = id;
        }

        public static ModifierSource FromCondition(Condition c) =>
            new ModifierSource(ModifierKind.Condition, c.ToString().ToLowerInvariant());

        public static ModifierSource Gear(string id) =>
            new ModifierSource(ModifierKind.Gear, Named(id));

        public static ModifierSource Effect(string id) =>
            new ModifierSource(ModifierKind.Effect, Named(id));

        // an unnamed source can never be removed again - a leak, not a shortcut
        static string Named(string id) =>
            string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("A modifier source needs an id, or nothing can remove it.", nameof(id))
                : id;

        public bool Equals(ModifierSource other) =>
            Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ModifierSource other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, Id);

        public static bool operator ==(ModifierSource a, ModifierSource b) => a.Equals(b);

        public static bool operator !=(ModifierSource a, ModifierSource b) => !a.Equals(b);

        // debug only, never localized - keep it off the screen
        public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{Id}";
    }
}
