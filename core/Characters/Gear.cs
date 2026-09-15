using System;
using System.Collections.Generic;
using Core.Dice;
using Core.Localization;

namespace Core.Characters
{
    public sealed class Gear
    {
        public static readonly Gear Nothing = new Gear("unarmed", Die.None);

        public Gear(
            string id,
            Die die,
            Skill supports = Skill.None,
            int defense = 0,
            IReadOnlyList<Condition> inflicts = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Die = die;
            Supports = supports;
            Defense = defense;
            Inflicts = inflicts ?? Array.Empty<Condition>();
        }

        public string Id { get; }

        public string NameKey => KeyConventions.GearName(Id);

        public string DescriptionKey => KeyConventions.Key(KeyConventions.GearNs, Id, "description");

        public Die Die { get; }

        // Skill.None helps every check; a named skill helps only that one
        public Skill Supports { get; }

        public int Defense { get; }

        public IReadOnlyList<Condition> Inflicts { get; }

        public bool IsReal => Die.IsReal() || Defense != 0 || Inflicts.Count > 0;

        public bool Helps(Skill skill) => Supports == Skill.None || Supports == skill;

        // debug only, never localized - keep it off the screen
        public override string ToString() =>
            $"{Id} {Die.Label()}" +
            (Supports != Skill.None ? $" ({Supports})" : "") +
            (Defense != 0 ? $" def {Defense:+0;-0}" : "") +
            (Inflicts.Count > 0 ? " -> " + string.Join(", ", Inflicts) : "");
    }
}
