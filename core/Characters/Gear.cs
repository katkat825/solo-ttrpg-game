using System;
using System.Collections.Generic;
using Core.Dice;
using Core.Localization;

namespace Core.Characters
{
    // A THING YOU CARRY, AND THE THREE WAYS IT CAN MATTER (CORE_RULES.md sections 1 and 9).
    //
    // "Getting better means bigger rocks. Progression is legible at a glance because it's
    // physical. You don't gain +1, you trade a d6 for a d8." That is pillar 3, and once gear is
    // data it stops being a sentence in a design document and becomes the only thing a campaign
    // has to write down to make a better axe.
    //
    // THREE WAYS, AND NO MORE:
    //
    //   A DIE IN THE POOL.  The gear die - "the tool, weapon, or focus you're using". The whole of
    //                       what a weapon is, mechanically.
    //   A NUMBER ON DEFENSE. Armour. `SIMULATION.md` section 3 measured Defense as by far the
    //                       biggest balance lever there is, so a campaign handing out +2 plate is
    //                       handing out something enormous - which is worth knowing, and is why it
    //                       is one number in one place rather than a system.
    //   A CONDITION IT PUTS ON SOMEBODY. A blade that leaves you Reeling. This is the "tagged
    //                       attacks" half of `CORE_RULES.md` section 9 that nothing could express
    //                       until gear was data.
    //
    // WHAT IS DELIBERATELY NOT HERE: charges, durability, stacking, sockets, rarity tiers,
    // set bonuses. CONVENTIONS.md 5 - over-generalising is its own failure mode. A campaign that
    // needs a magic sword writes a bigger die.
    //
    // AND THE ONE THING EVERY PIECE OF GEAR CAN DO NEEDS NO FIELD: it can be Notched. "Gear takes
    // Conditions too - a Notched blade drops its gear die" is true of all of it, so it is
    // `Actor.NotchGear` and runtime state, not something an author declares.
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

        // a stable id, never display text - it goes in saves and is matched on
        public string Id { get; }

        public string NameKey => KeyConventions.GearName(Id);

        public string DescriptionKey => KeyConventions.Key(KeyConventions.GearNs, Id, "description");

        public Die Die { get; }

        // WHICH CHECKS IT IS FOR. `Skill.None` means any of them - a lantern, a rope, a crowbar,
        // and every piece of gear the engine ships, which is why nothing in the built-in roster
        // moves when this is read. A weapon that names a skill contributes its die to that skill's
        // checks and sits out the rest, because "the tool you're using" is not a tool you are not
        // using (CORE_RULES.md section 1)
        public Skill Supports { get; }

        public int Defense { get; }

        // what a hit with it leaves on the target, beyond the damage
        public IReadOnlyList<Condition> Inflicts { get; }

        public bool IsReal => Die.IsReal() || Defense != 0 || Inflicts.Count > 0;

        // is this die in the pool for that check
        public bool Helps(Skill skill) => Supports == Skill.None || Supports == skill;

        // DEVELOPER ONLY - not localized, never shown to a player
        public override string ToString() =>
            $"{Id} {Die.Label()}" +
            (Supports != Skill.None ? $" ({Supports})" : "") +
            (Defense != 0 ? $" def {Defense:+0;-0}" : "") +
            (Inflicts.Count > 0 ? " -> " + string.Join(", ", Inflicts) : "");
    }
}
