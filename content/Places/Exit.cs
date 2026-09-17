using Content.World;

namespace Content.Places
{
    public sealed class Exit
    {
        public Exit(int slot, string to, int arriving = 0, string road = null,
                    Requirement needs = null)
        {
            Slot = slot;
            To = to ?? "";
            Arriving = arriving;
            Road = road ?? "";
            Needs = needs ?? Requirement.Always;
        }

        // the spawn glyph you step onto to leave; the map already draws the doorway
        public int Slot { get; }

        public string To { get; }

        // the spawn slot you appear on over there; 0 means that place's own start square
        public int Arriving { get; }

        // empty for a step through a door; a road id for a journey that can be interrupted
        public string Road { get; }

        public bool IsAJourney => Road.Length > 0;

        // a locked door is an exit whose requirement is not met yet, not a missing exit
        public Requirement Needs { get; }

        public override string ToString() =>
            $"spawn {Slot} -> {To}" +
            (Arriving > 0 ? $" arriving on {Arriving}" : "") +
            (IsAJourney ? $" along {Road}" : "") +
            (Needs.IsAlways ? "" : $" ({Needs})");
    }
}
