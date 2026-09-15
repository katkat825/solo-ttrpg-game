using System;
using Core.Characters;
using Core.Dice;

namespace Content.Classes
{
    public sealed class Growth
    {
        public Growth(string id, Attr? attribute = null, Skill? skill = null, string ability = null)
        {
            Id = id;
            Attribute = attribute;
            Skill = skill;
            Ability = ability;
        }

        // an id, not a description, because a save records which steps a hero got and derives the dice
        public string Id { get; }

        public Attr? Attribute { get; }

        // skills aren't on the trait pipeline, so this sets the die rather than modifying it
        public Skill? Skill { get; }

        public string Ability { get; }

        public const Die FirstRating = Die.D6;

        // prefixed; the modifier id space is shared, so growth:veteran can't collide with strain or a phase
        public ModifierSource Source => ModifierSource.Effect("growth:" + Id);


        // modifiers, so order doesn't matter; the kit is not touched here (ClassCard.KitAfter reads that)
        public void ApplyTo(Actor actor)
        {
            if (actor == null) return;

            if (Attribute is { } attr)
                actor.AddModifier(new TraitModifier(Source, attr, +1));

            if (Skill is { } skill)
            {
                Die had = actor.SkillDie(skill);

                actor.With(skill, had.IsReal() ? had.StepUp() : FirstRating);
            }
        }

        public override string ToString() =>
            Id + ": " +
            (Attribute != null ? $"{Attribute} up a size"
           : Skill != null ? $"{Skill} trained"
           : Ability != null ? $"{Ability} unlocked"
           : "nothing");
    }
}
