using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.World
{
    public sealed class Episode
    {
        public Episode(string place, IReadOnlyList<string> roster,
                       IReadOnlyDictionary<int, string> entities, int began)
        {
            Place = place ?? "";
            Roster = roster ?? Array.Empty<string>();
            Entities = entities ?? new Dictionary<int, string>();
            Began = began;
        }

        public string Place { get; }

        // one archetype id per slot, slot 1 first, holes where a slot is unused
        public IReadOnlyList<string> Roster { get; }

        // slot -> entity id, for standings that were somebody rather than something
        public IReadOnlyDictionary<int, string> Entities { get; }

        // the slot the fight was picked on, or 0 for a place that was already armed
        public int Began { get; }

        public int Foes => Roster.Count(id => id != null);

        public bool IsReal => Foes > 0;

        public override string ToString() =>
            $"a fight in {Place}, {Foes} foe(s)" +
            (Entities.Count > 0
                ? $", {Entities.Count} of them somebody (" +
                  string.Join(", ", Entities.OrderBy(e => e.Key).Select(e => $"{e.Key}:{e.Value}")) + ")"
                : "");
    }
}
