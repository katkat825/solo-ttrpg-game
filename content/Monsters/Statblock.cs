using System.Collections.Generic;
using Content.Items;
using Core.Characters;
using Core.Dice;

namespace Content.Monsters
{
    // A MONSTER, AS A CAMPAIGN WROTE IT DOWN.
    //
    // `ARCHITECTURE.md` section 2 draws the line at "if adding a new campaign would require
    // touching it, it's content", and a statblock is the clearest case there is: attribute dice,
    // skill dice, what it is holding, how much Vigor, how hard it is to hit, and how it picks who
    // to swing at. None of that is a rule. All of it is a number somebody chose.
    //
    // WHY THIS IS NOT JUST AN `Actor`. `Actor` is runtime state - it takes damage, it carries
    // Conditions, it is mutable and one per creature in the room. A statblock is the recipe, read
    // once and used to stamp out as many as the encounter asks for. `Create` hands back a NEW one
    // every time for exactly the reason `IArchetypeSource` documents: actors are mutable and a
    // shared one would let a wounded ghoul turn up already wounded.
    //
    // AND IT CARRIES ONE THING AN `Actor` DOES NOT: the behaviour name. That goes onto the Actor
    // as an id (`Actor.Behaviour`) because the fight needs it per-creature, but the vocabulary it
    // is drawn from is the engine's (`Core.Combat.Behaviours`) and the choice is the campaign's.
    public sealed class Statblock
    {
        public Statblock(
            string id,
            Tier tier,
            int vigor,
            int defense,
            IReadOnlyDictionary<Attr, Die> attributes,
            IReadOnlyDictionary<Skill, Die> skills,
            string gearId,
            Die gearDie,
            string behaviour,
            LootTable loot = null,
            string miniId = null)
        {
            Id = id;
            Tier = tier;
            Vigor = vigor;
            Defense = defense;
            Attributes = attributes ?? new Dictionary<Attr, Die>();
            Skills = skills ?? new Dictionary<Skill, Die>();
            GearId = gearId;
            GearDie = gearDie;
            Behaviour = behaviour;
            Loot = loot ?? LootTable.Nothing;
            MiniId = miniId ?? "";
        }

        // SCOPED BY THE CAMPAIGN THAT WROTE IT - `ashfall.ghoul`. See Campaigns/ContentId.cs
        public string Id { get; }

        public Tier Tier { get; }

        public int Vigor { get; }

        public int Defense { get; }

        public IReadOnlyDictionary<Attr, Die> Attributes { get; }

        public IReadOnlyDictionary<Skill, Die> Skills { get; }

        // null when it carries nothing, which is a real statblock and not an omission - an
        // unarmed thing throws a smaller pool (CORE_RULES.md section 1)
        public string GearId { get; }

        public Die GearDie { get; }

        // a name out of Core.Combat.Behaviours, or null for the engine's default
        public string Behaviour { get; }

        // what it was carrying, drawn once when it goes down (P1). Empty for most things, because
        // most things are carrying nothing worth taking
        public LootTable Loot { get; }

        // WHAT IT STANDS AS ON THE TABLE (MINIS_AND_ART.md A1). Empty for a statblock that does
        // not care, which is most of them - and then the board chooses a figure by tier, which is
        // what it did for the whole of Phase C.
        //
        // IT IS DELIBERATELY NOT ON THE `Actor`. A mini never becomes a rule: `core/` has no
        // opinion about what a thing looks like, the sim runs without art at all, and a scary
        // model on a weak statblock is a weak monster. So this stays on the RECIPE, where the
        // presentation layer can read it and the rules cannot
        public string MiniId { get; }

        // THE KEY IT NAMES ITSELF WITH IS DERIVED AND NEVER DECLARED. A schema field for it would
        // be a second description of the id, free to disagree with it, and would let a campaign
        // point its ghoul's name at somebody else's string. `Actor.NameKey` composes it from the
        // scoped id, which is the whole reason the id is scoped
        public Actor Create()
        {
            var actor = new Actor(Id, Vigor, Defense, Tier) { Behaviour = Behaviour };

            foreach (KeyValuePair<Attr, Die> a in Attributes) actor.With(a.Key, a.Value);
            foreach (KeyValuePair<Skill, Die> s in Skills) actor.With(s.Key, s.Value);

            if (GearId != null) actor.WithWeapon(GearId, GearDie);

            return actor;
        }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{Id} [{Tier}] vigor {Vigor} def {Defense}, {Attributes.Count} attributes, " +
            $"{Skills.Count} skills, {GearId ?? "empty-handed"}" +
            (MiniId.Length > 0 ? $", stands as {MiniId}" : "") +
            (Loot.IsEmpty ? "" : $", loot: {Loot}");
    }
}
