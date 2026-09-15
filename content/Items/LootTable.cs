using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Content.Campaigns;
using Content.Schema;
using Core.Dice;

namespace Content.Items
{
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

            // null for "nothing this time"
            public string Item { get; }

            public int Weight { get; }
        }

        public IReadOnlyList<Entry> Entries => _entries;

        public int Total { get; }

        public bool IsEmpty => _entries.Length == 0 || Total <= 0;

        // null is a real answer; most things carry nothing
        public string Draw(IRng rng)
        {
            if (rng == null || IsEmpty) return null;

            // Roll(n) is 1..n, a weighted index if you walk the weights down
            int rolled = rng.Roll(Total);

            foreach (Entry entry in _entries)
            {
                rolled -= entry.Weight;

                if (rolled <= 0) return entry.Item;
            }

            // unreachable while the weights sum to Total
            return null;
        }

        public double ChanceOf(string item) =>
            IsEmpty ? 0.0 : (double)_entries.Where(e => e.Item == item).Sum(e => e.Weight) / Total;


        // where is the JSON path this table sits at, so a problem reads loot[2].weight, not the root
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

        public override string ToString() =>
            IsEmpty
                ? "no loot"
                : string.Join(", ", _entries.Select(
                    e => $"{e.Item ?? "nothing"} {(double)e.Weight / Total:0%}"));
    }
}
