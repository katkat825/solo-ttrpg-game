using System;

namespace Core.Characters
{
    // where a trait modifier came from - its provenance, and the handle you remove it by
    //
    // this is the whole reason the pipeline holds a list rather than a value. "take off the
    // ring but keep raging" is unanswerable against a die that only remembers its size, and
    // it is the first thing an item or a class feature will ask for
    //
    // an id, never display text - it goes in saves and is matched on, so KeyConventions rule 3
    // applies to it: renaming one breaks every save and every campaign that named it
    public enum ModifierKind
    {
        // a Condition off the vigor track or an attack - the only kind the engine applies today
        Condition,

        // worn or wielded, and removable by taking it off
        Gear,

        // everything with a duration - a buff, a class feature, Strain
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

        // an unnamed source cannot be removed again, which makes it a leak rather than a shortcut
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

        // DEVELOPER ONLY - logs and test failures, not localized and never shown to a player
        public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}:{Id}";
    }
}
