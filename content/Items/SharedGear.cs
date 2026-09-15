using System;
using System.Collections.Generic;
using System.Linq;
using Core.Characters;
using Core.Dice;

namespace Content.Items
{
    public static class SharedGear
    {
        // the engine's roster, named rather than injected; there's nothing to substitute in "what came in the box"
        public static ItemCatalogue Catalogue { get; } = Of(new BuiltInArchetypes());

        public static bool Has(string id) => Catalogue.Has(id);

        public static Gear Of(string id) => Catalogue.Of(id);

        // deduplicated; two archetypes carrying the same id agree because the die came from one place
        public static ItemCatalogue Of(IArchetypeSource roster)
        {
            var gear = new Dictionary<string, Gear>(StringComparer.Ordinal);

            foreach (string id in (roster?.Ids ?? Array.Empty<string>())
                                  .OrderBy(i => i, StringComparer.Ordinal))
            {
                Gear held = roster.Create(id).Wielded;

                // Gear.Nothing is the empty hand, not a piece of gear; skip it
                if (held == null || !held.Die.IsReal()) continue;

                gear[held.Id] = held;
            }

            return ItemCatalogue.Of(gear.Values);
        }
    }
}
