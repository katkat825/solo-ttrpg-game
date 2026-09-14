using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;
using Core.Characters;

namespace Content.Items
{
    // EVERY PIECE OF GEAR A CAMPAIGN SHIPS, AND THE ENGINE'S OWN (CONTENT_PIPELINE.md P1).
    //
    // The same shape as `JsonArchetypeSource`: a folder of JSON files, one item each, loaded whole,
    // with every problem reported and nothing thrown. A folder diffs and reviews per item, and an
    // author adding a sword should not open the file that also has the plate mail in it.
    //
    // IT IS NOT AN `IArchetypeSource`, because there is no seam for gear to sit behind - nothing in
    // `core/` looks gear up by id. `Actor` holds a `Gear` it was handed, and who hands it one is the
    // caller's business: a statblock inlines its own (`"gear": { ... }`), and a campaign's
    // `items/` folder is where the ones that get picked up and swapped live. So this is a
    // catalogue and not a source, and the day something in the rules needs to resolve an id there
    // is an obvious interface to add.
    public sealed class ItemCatalogue
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Gear> _items = new Dictionary<string, Gear>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        public IReadOnlyCollection<string> Ids => _items.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _items.ContainsKey(id);

        // null rather than an exception: "the campaign that had that sword is not installed" is an
        // ordinary thing for a save to run into (P6), and the caller shows the hero empty-handed
        public Gear Of(string id) =>
            id != null && _items.TryGetValue(id, out Gear gear) ? gear : null;

        // ---- reading a folder ----

        public static ItemCatalogue Read(string folder)
        {
            var catalogue = new ItemCatalogue();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return catalogue;

            foreach (string path in Files(folder))
            {
                string name = Path.GetFileName(path);
                string text;

                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception could)
                {
                    catalogue._problems.Add(new ContentProblem(name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<Gear> read = ItemReader.Parse(text, name);

                if (!read.Ok)
                {
                    catalogue._problems.AddRange(read.Problems);
                    continue;
                }

                catalogue.Add(read.Value, name);
            }

            return catalogue;
        }

        void Add(Gear gear, string file)
        {
            if (_items.ContainsKey(gear.Id))
            {
                _problems.Add(new ContentProblem(
                    file, "id", $"'{gear.Id}' is already the id of another item in this folder"));
                return;
            }

            _items[gear.Id] = gear;
        }

        // ANOTHER CATALOGUE'S ITEMS, FOLDED IN. Gear ids are a shared namespace (see ItemReader), so
        // two campaigns that both ship a `torch` are describing the same word - and the first one
        // loaded keeps it, said out loud rather than decided by enumeration order
        public IReadOnlyList<string> Absorb(ItemCatalogue other)
        {
            var taken = new List<string>();

            if (other == null) return taken;

            foreach (string id in other.Ids.OrderBy(i => i, StringComparer.Ordinal))
            {
                if (_items.ContainsKey(id)) { taken.Add(id); continue; }

                _items[id] = other.Of(id);
            }

            _problems.AddRange(other.Problems);

            return taken;
        }

        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{_items.Count} items" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
