using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Core.Dice;

namespace Content.Items
{
    // WHAT IT WAS CARRYING (CONTENT_PIPELINE.md P1).
    //
    //     "loot": [
    //       { "item": "cold_iron_axe", "weight": 1 },
    //       { "item": "scale_coat",    "weight": 3 },
    //       { "weight": 6 }
    //     ]
    //
    // A weighted list, one draw, and an entry with no item is "nothing this time" - which is how a
    // table says "one in ten" without a second concept for drop chance. Six parts nothing, three
    // parts a coat, one part the axe.
    //
    // RESOLVED THROUGH `IRng`, WHICH IS THE WHOLE POINT. "Seeded runs are reproducible and there's
    // a test asserting it" (CONVENTIONS.md 6) is a property the project has held since the dice
    // core was written, and a drop rolled off `System.Random` would be the first thing to break it
    // - a fight you re-ran on the same seed would give you a different sword and you would have no
    // idea why. One seam, every source of chance in the game behind it.
    //
    // ONE DRAW AND NOT A BAG. A monster drops one thing or nothing. A campaign that wants a hoard
    // gives the boss a table whose entries are better, not a table that rolls four times - because
    // "how many draws" is the beginning of a loot SYSTEM, and CONVENTIONS.md 5 is about not
    // building one of those.
    public sealed class LootTable
    {
        public static readonly LootTable Nothing = new LootTable(Array.Empty<Entry>());

        readonly Entry[] _entries;

        public LootTable(IReadOnlyList<Entry> entries)
        {
            _entries = (entries ?? Array.Empty<Entry>()).ToArray();
            Total = _entries.Sum(e => e.Weight);
        }

        public readonly struct Entry
        {
            public Entry(string item, int weight)
            {
                Item = item;
                Weight = weight;
            }

            // null for "nothing this time", which is an entry like any other
            public string Item { get; }

            public int Weight { get; }
        }

        public IReadOnlyList<Entry> Entries => _entries;

        public int Total { get; }

        public bool IsEmpty => _entries.Length == 0 || Total <= 0;

        // ONE DRAW. null is a real answer - most things carry nothing worth taking
        public string Draw(IRng rng)
        {
            if (rng == null || IsEmpty) return null;

            // Roll(n) is 1..n, which is exactly a weighted index if you walk the weights down
            int rolled = rng.Roll(Total);

            foreach (Entry entry in _entries)
            {
                rolled -= entry.Weight;

                if (rolled <= 0) return entry.Item;
            }

            // unreachable while the weights sum to Total, and an honest answer if they ever do not
            return null;
        }

        // what the chance of a given item is, exactly. For a report, a validator, and for anybody
        // arguing about whether one in ten is too often
        public double ChanceOf(string item) =>
            IsEmpty ? 0.0 : (double)_entries.Where(e => e.Item == item).Sum(e => e.Weight) / Total;

        // ---- reading one ----

        // <paramref name="where"/> is the JSON path this table was found at, so a problem inside a
        // monster's own `loot` field says `loot[2].weight` rather than starting again at the root
        public static LootTable Parse(JsonElement value, string file, string where,
                                      List<ContentProblem> problems)
        {
            if (value.ValueKind == JsonValueKind.Null) return Nothing;

            if (value.ValueKind != JsonValueKind.Array)
            {
                problems.Add(new ContentProblem(
                    file, where,
                    "loot is a list of entries, like [ { \"item\": \"torch\", \"weight\": 1 } ]"));
                return Nothing;
            }

            var entries = new List<Entry>();
            int at = 0;

            foreach (JsonElement element in value.EnumerateArray())
            {
                string entryAt = $"{where}[{at++}]";

                if (element.ValueKind != JsonValueKind.Object)
                {
                    problems.Add(new ContentProblem(
                        file, entryAt, "an entry is an object with an optional item and a weight"));
                    continue;
                }

                string item = null;

                if (element.TryGetProperty("item", out JsonElement named) &&
                    named.ValueKind != JsonValueKind.Null)
                {
                    if (named.ValueKind != JsonValueKind.String || !ContentId.IsLocal(named.GetString()))
                    {
                        problems.Add(new ContentProblem(
                            file, entryAt + ".item",
                            "an item id is lowercase a-z, 0-9 and underscore - leave it out " +
                            "entirely for 'nothing this time'"));
                        continue;
                    }

                    item = named.GetString();
                }

                int weight = 1;

                if (element.TryGetProperty("weight", out JsonElement weighted))
                {
                    if (weighted.ValueKind != JsonValueKind.Number ||
                        !weighted.TryGetInt32(out weight) || weight < 1)
                    {
                        problems.Add(new ContentProblem(
                            file, entryAt + ".weight",
                            "a weight is a whole number of 1 or more - it is how many parts in the " +
                            "table this entry is"));
                        continue;
                    }
                }

                entries.Add(new Entry(item, weight));
            }

            return entries.Count == 0 ? Nothing : new LootTable(entries);
        }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            IsEmpty
                ? "no loot"
                : string.Join(", ", _entries.Select(
                    e => $"{e.Item ?? "nothing"} {(double)e.Weight / Total:0%}"));
    }
}
