using System.Collections.Generic;
using Content.Items;
using Core.Characters;
using Core.Dice;

namespace Content.Monsters
{
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

        public string Id { get; }

        public Tier Tier { get; }

        public int Vigor { get; }

        public int Defense { get; }

        public IReadOnlyDictionary<Attr, Die> Attributes { get; }

        public IReadOnlyDictionary<Skill, Die> Skills { get; }

        // null for empty-handed, which is a real statblock, not an omission
        public string GearId { get; }

        public Die GearDie { get; }

        // a Core.Combat.Behaviours name, or null for the engine's default
        public string Behaviour { get; }

        public LootTable Loot { get; }

        // empty lets the board pick a figure by tier; kept on the recipe, never the Actor (a mini is never a rule)
        public string MiniId { get; }

        // its name key is derived from the scoped id, never declared, so it can't point at someone else's string
        public Actor Create()
        {
            var actor = new Actor(Id, Vigor, Defense, Tier) { Behaviour = Behaviour };

            foreach (KeyValuePair<Attr, Die> a in Attributes) actor.With(a.Key, a.Value);
            foreach (KeyValuePair<Skill, Die> s in Skills) actor.With(s.Key, s.Value);

            if (GearId != null) actor.WithWeapon(GearId, GearDie);

            return actor;
        }

        public override string ToString() =>
            $"{Id} [{Tier}] vigor {Vigor} def {Defense}, {Attributes.Count} attributes, " +
            $"{Skills.Count} skills, {GearId ?? "empty-handed"}" +
            (MiniId.Length > 0 ? $", stands as {MiniId}" : "") +
            (Loot.IsEmpty ? "" : $", loot: {Loot}");
    }
}
