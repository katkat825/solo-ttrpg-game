using System;
using System.Collections.Generic;
using Core.Dice;

namespace Core.Characters
{
    // statblocks tuned against simulation
    // Defense 11 and Rival Vigor 8 give roughly 85% survival at ~6.6 rounds
    // TEMPORARY - content living in code, so the sim and tests have something to run
    // replace with a data-backed IArchetypeSource and delete this file
    //
    // the factory methods below are private on purpose. they were public and every consumer
    // called them directly - 36 sites against one that used the interface - which made the
    // seam decorative and meant the move to data would touch every test in the suite.
    // ask an IArchetypeSource for an id instead; see EngineIds
    public sealed class BuiltInArchetypes : IArchetypeSource
    {
        // kept as aliases so existing references still resolve
        // EngineIds is where these live now, next to the encounters that name them
        public const string BarbarianId = EngineIds.Barbarian;
        public const string RabbleId = EngineIds.Rabble;
        public const string RivalId = EngineIds.Rival;

        static readonly string[] AllIds = { BarbarianId, RabbleId, RivalId };

        public IReadOnlyCollection<string> Ids => AllIds;

        public bool Has(string id) => Array.IndexOf(AllIds, id) >= 0;

        public Actor Create(string id) => id switch
        {
            BarbarianId => Barbarian(),
            RabbleId => Rabble(),
            RivalId => Rival(),
            _ => throw new KeyNotFoundException($"No archetype '{id}'.")
        };

        // ---- the statblocks themselves ----

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
    }
}
