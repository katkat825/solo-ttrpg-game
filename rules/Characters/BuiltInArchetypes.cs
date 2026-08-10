using System;
using System.Collections.Generic;
using Rules.Dice;

namespace Rules.Characters
{
    // statblocks tuned against simulation
    // Defense 11 and Rival Vigor 8 give roughly 85% survival at ~6.6 rounds
    // TEMPORARY - content living in code, so the sim and tests have something to run
    // replace with a data-backed IArchetypeSource and delete the static helpers
    public sealed class BuiltInArchetypes : IArchetypeSource
    {
        public const string BarbarianId = "barbarian";
        public const string RabbleId = "rabble";
        public const string RivalId = "rival";

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

        // ---- static convenience, used by tests and the sim ----

        public static Actor Barbarian() =>
            new Actor(BarbarianId, maxVigor: 20, defense: 11)
                .With(Attr.Might, Die.D8)
                .With(Attr.Grace, Die.D6)
                .With(Attr.Wits, Die.D6)
                .With(Attr.Heart, Die.D6)
                .With(Skill.Blades, Die.D6)
                .With(Skill.Brawl, Die.D6)
                .WithWeapon("axe", Die.D6);

        public static Actor Rabble(int ordinal = 0) =>
            new Actor(RabbleId, maxVigor: 1, defense: 7, Tier.Rabble)
                .With(Attr.Might, Die.D6)
                .WithWeapon("club", Die.D4)
                .Numbered(ordinal);

        public static Actor Rival() =>
            new Actor(RivalId, maxVigor: 8, defense: 11)
                .With(Attr.Might, Die.D8)
                .With(Skill.Blades, Die.D6)
                .WithWeapon("blade", Die.D6);

        // the standard first serious fight - N Rabble and one Rival
        public static IList<Actor> StandardEncounter(int rabbleCount = 4)
        {
            var foes = new List<Actor>();
            for (int i = 0; i < rabbleCount; i++) foes.Add(Rabble(i + 1));
            foes.Add(Rival());
            return foes;
        }
    }
}
