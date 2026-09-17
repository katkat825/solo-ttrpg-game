using System;
using System.Collections.Generic;
using System.Linq;
using Content.Entities;
using Core.Space;

namespace Content.World
{
    public sealed class Present
    {
        public Present(int slot, Cell at, string id, Entity what,
                       IReadOnlyList<Interaction> offers)
        {
            Slot = slot;
            At = at;
            Id = id ?? "";
            What = what;
            Offers = offers ?? Array.Empty<Interaction>();
        }

        // the spawn glyph it came from; how a fight names it and a save finds it again
        public int Slot { get; }

        public Cell At { get; }

        public string Id { get; }

        // null for a foe already standing in a pre-armed fight; foes are statblocks, not entities
        public Entity What { get; }

        public bool IsAFoe => What == null;

        public IReadOnlyList<Interaction> Offers { get; }

        public bool Offering(Interaction verb) => Offers.Contains(verb);

        public override string ToString() =>
            $"{Id} on spawn {Slot} at {At}" +
            (Offers.Count > 0 ? $" - {string.Join(", ", Offers)}" : "");
    }
}
