using Content.World;

namespace Content.Places
{
    public sealed class Standing
    {
        public Standing(int slot, string monster, string entity, Requirement needs = null)
        {
            Slot = slot;
            Monster = monster ?? "";
            Entity = entity ?? "";
            Needs = needs ?? Requirement.Always;
        }

        public static Standing Foe(int slot, string monster, Requirement needs = null) =>
            new Standing(slot, monster, null, needs);

        public static Standing Thing(int slot, string entity, Requirement needs = null) =>
            new Standing(slot, null, entity, needs);

        // 1-9, matching the glyph drawn on the map
        public int Slot { get; }

        // empty when this is an entity
        public string Monster { get; }

        // empty when this is a monster
        public string Entity { get; }

        public bool IsAFoe => Monster.Length > 0;

        public string Id => IsAFoe ? Monster : Entity;

        public Requirement Needs { get; }

        public override string ToString() =>
            (IsAFoe ? $"{Monster} on spawn {Slot}" : $"{Entity} on spawn {Slot}") +
            (Needs.IsAlways ? "" : $" ({Needs})");
    }
}
