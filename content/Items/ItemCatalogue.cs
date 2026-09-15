using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;
using Core.Characters;

namespace Content.Items
{
    public sealed class ItemCatalogue
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Gear> _items = new Dictionary<string, Gear>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        public IReadOnlyCollection<string> Ids => _items.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _items.ContainsKey(id);

        // null rather than throw; a save may name gear from a campaign nobody installed
        public Gear Of(string id) =>
            id != null && _items.TryGetValue(id, out Gear gear) ? gear : null;


        public static ItemCatalogue Of(IEnumerable<Gear> gear)
        {
            var catalogue = new ItemCatalogue();

            foreach (Gear one in gear ?? Array.Empty<Gear>())
                if (one?.Id != null) catalogue._items[one.Id] = one;

            return catalogue;
        }


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

        // shared namespace; the first-loaded torch wins where two catalogues have the id
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

        public override string ToString() =>
            $"{_items.Count} items" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
