using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Kits
{
    public sealed class KitBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Ability> _abilities =
            new Dictionary<string, Ability>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        KitBook(string pack) => Pack = pack ?? "";

        public string Pack { get; }

        public IReadOnlyCollection<string> Ids => _abilities.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _abilities.ContainsKey(id);

        public Ability Of(string id) =>
            id != null && _abilities.TryGetValue(id, out Ability ability) ? ability : null;

        public IEnumerable<Ability> All =>
            _abilities.OrderBy(a => a.Key, StringComparer.Ordinal).Select(a => a.Value);


        // a missing folder is fine; most packs ship no abilities
        public static KitBook Read(string folder, string pack)
        {
            var book = new KitBook(pack);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return book;

            foreach (string path in Directory.EnumerateFiles(folder, "*" + Extension,
                                                             SearchOption.TopDirectoryOnly)
                                             .OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                string text;

                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception could)
                {
                    book._problems.Add(new ContentProblem(name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<Ability> read = AbilityReader.Parse(text, name, pack);

                if (!read.Ok)
                {
                    book._problems.AddRange(read.Problems);
                    continue;
                }

                if (book._abilities.ContainsKey(read.Value.Id))
                {
                    book._problems.Add(new ContentProblem(
                        name, "id",
                        $"'{read.Value.Id}' is already the id of another ability in this folder"));
                    continue;
                }

                book._abilities[read.Value.Id] = read.Value;
            }

            return book;
        }


        public static KitBook Of(string pack, params Ability[] abilities)
        {
            var book = new KitBook(pack);

            foreach (Ability ability in abilities ?? Array.Empty<Ability>())
                if (ability?.Id != null) book._abilities[ability.Id] = ability;

            return book;
        }

        // resolves across this pack and the engine's own; SharedKit is the fallback, like SharedGear
        public Ability Find(string local)
        {
            if (string.IsNullOrEmpty(local)) return null;

            string scoped = ContentId.IsCampaign(Pack) ? ContentId.Scoped(Pack, local) : local;

            return Of(scoped) ?? SharedKit.Of(local);
        }

        public override string ToString() =>
            $"{_abilities.Count} abilities" +
            (Pack.Length > 0 ? $" from {Pack}" : "") +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
