using System.Collections.Generic;
using Core.Characters;
using Core.Combat;
using Core.Resolution;

namespace Content.Kits
{
    public static class SharedKit
    {

        public const string ForceDoorId = "force_door";

        public static Ability ForceDoor { get; } = new Ability(
            ForceDoorId,
            Primitive.Check,
            Attr.Might,
            Skill.Blades,
            useGear: true,
            against: Difficulty.Standard,
            magnitude: Magnitude.Impact,
            cost: Cost.None,
            target: Target.Reach,
            onHit: new[] { Effect.Open },
            onMiss: new[] { Effect.Open, Effect.Rough, Effect.Recoil });


        public const string BoltId = "bolt";

        public static Ability Bolt { get; } = new Ability(
            BoltId,
            Primitive.Channel,
            Channeling.Attribute,
            Channeling.Skill,
            useGear: true,
            against: 0,
            againstDefense: true,
            magnitude: Magnitude.Impact,
            cost: Cost.Strain,
            target: Target.Sight,
            onHit: new[] { Effect.Damage });

        public static IReadOnlyList<Ability> All { get; } = new[] { ForceDoor, Bolt };

        public static Ability Of(string id)
        {
            foreach (Ability ability in All)
                if (ability.Id == id) return ability;

            return null;
        }

        public static bool Has(string id) => Of(id) != null;

        public static KitBook Book { get; } = KitBook.Of("", ForceDoor, Bolt);
    }
}
