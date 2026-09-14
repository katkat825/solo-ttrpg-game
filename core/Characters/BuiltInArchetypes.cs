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

        // THE BOSS TIER (CORE_RULES.md section 8): a full statblock, two actions, a reaction, and
        // a phase change at half Vigor (Core.Combat.PhaseChange). One per chapter.
        //
        // THESE NUMBERS WERE SIMULATED AND NOT FELT (CONVENTIONS.md 8), and the first attempt was
        // wrong by two orders of magnitude. Might d10 / Blades d8 / maul d8, Vigor 24, Defense 12
        // measured at a 0.3% WIN RATE in three and a half rounds - not a hard fight, a death
        // sentence, and for the same reason SIMULATION.md section 2 gives for one hero action: two
        // actions a round is an enormous multiplier and a Dread is the only thing in the game that
        // has it. A boss does not need a good pool on top of it.
        //
        // So the pool is a Rival's, and what makes it a boss is that it swings twice and changes
        // at half health. `sim/Reports/DreadReport.cs` measures it:
        //
        //     as it ships        47.6% over 6.2 rounds
        //     no phase change    58.2% over 6.5 rounds     - so the second phase costs 11 points
        //     defense 10 / 12    69.9% / 19.5%             - Defense is still the scalpel
        //
        // 47.6% is measured with the sim's auto-player, which never spends a Nerve, never uses its
        // reaction and never moves - so a played boss sits well above it. Against a standard fight
        // at 85% (SIMULATION.md section 5), that is what a once-a-chapter set piece should read as.
        //
        // VIGOR 14 IS PACING AND NOT DIFFICULTY, exactly as SIMULATION.md section 3 says: it is
        // what makes the fight 6 rounds long, which is what makes there be a second half to change
        // into. Defense 11 rather than 12 because 12 measured at 19.5% and because a boss that is
        // harder to HIT than a Rival, on top of acting twice, is two levers pulled at once.
        //
        // Might d8 and not d10 because the phase change steps it UP, and a boss whose second phase
        // is mostly saturation is a boss whose second phase does nothing (CORE_RULES.md section 9)
        static Actor Dread() =>
            new Actor(DreadId, maxVigor: 14, defense: 11, Tier.Dread)
                .With(Attr.Might, Die.D8)
                .With(Attr.Grace, Die.D6)
                .With(Attr.Wits, Die.D6)
                .With(Skill.Blades, Die.D6)
                .WithWeapon("maul", Die.D6);

        // THE CASTER, and a second playable statblock rather than a second foe. Heart d8 where the
        // Barbarian has Might d8, trained in Channeling, and a focus in the gear slot - so
        // `Heart + Channeling + focus` is a real three-die pool with a real die left over, which
        // is the whole of what C5 needs to be able to look at.
        //
        // Vigor and Defense are the Barbarian's, because changing two things at once is how you
        // learn nothing: this exists to show channelling, not to be balanced against him. When
        // classes are content (Phase R) this file goes and takes the argument with it
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
