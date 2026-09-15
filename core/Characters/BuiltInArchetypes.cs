using System;
using System.Collections.Generic;
using Core.Dice;

namespace Core.Characters
{
    // temporary content in code so the sim and tests have a roster; replace with a data-backed IArchetypeSource
    // factories are private on purpose - public ones let callers bypass the IArchetypeSource seam
    public sealed class BuiltInArchetypes : IArchetypeSource
    {
        public const string BarbarianId = EngineIds.Barbarian;
        public const string RabbleId = EngineIds.Rabble;
        public const string RivalId = EngineIds.Rival;

        public const string MageId = EngineIds.Mage;

        public const string DreadId = EngineIds.Dread;

        static readonly string[] AllIds = { BarbarianId, RabbleId, RivalId, MageId, DreadId };

        public IReadOnlyCollection<string> Ids => AllIds;

        public bool Has(string id) => Array.IndexOf(AllIds, id) >= 0;

        public Actor Create(string id) => id switch
        {
            BarbarianId => Barbarian(),
            RabbleId => Rabble(),
            RivalId => Rival(),
            MageId => Mage(),
            DreadId => Dread(),
            _ => throw new KeyNotFoundException($"No archetype '{id}'.")
        };


        static Actor Barbarian() =>
            new Actor(BarbarianId, maxVigor: 20, defense: 11)
                .With(Attr.Might, Die.D8)
                .With(Attr.Grace, Die.D6)
                .With(Attr.Wits, Die.D6)
                .With(Attr.Heart, Die.D6)
                .With(Skill.Blades, Die.D6)
                .With(Skill.Brawl, Die.D6)
                .WithWeapon("axe", Die.D6);

        static Actor Rabble() =>
            new Actor(RabbleId, maxVigor: 1, defense: 7, Tier.Rabble)
                .With(Attr.Might, Die.D6)
                .WithWeapon("club", Die.D4);

        static Actor Rival() =>
            new Actor(RivalId, maxVigor: 8, defense: 11)
                .With(Attr.Might, Die.D8)
                .With(Skill.Blades, Die.D6)
                .WithWeapon("blade", Die.D6);

        static Actor Dread() =>
            new Actor(DreadId, maxVigor: 14, defense: 11, Tier.Dread)
                .With(Attr.Might, Die.D8)
                .With(Attr.Grace, Die.D6)
                .With(Attr.Wits, Die.D6)
                .With(Skill.Blades, Die.D6)
                .WithWeapon("maul", Die.D6);

        static Actor Mage() =>
            new Actor(MageId, maxVigor: 20, defense: 11)
                .With(Attr.Might, Die.D6)
                .With(Attr.Grace, Die.D6)
                .With(Attr.Wits, Die.D6)
                .With(Attr.Heart, Die.D8)
                .With(Skill.Channeling, Die.D6)
                .With(Skill.Lore, Die.D6)
                .WithWeapon("focus", Die.D6);
    }
}
