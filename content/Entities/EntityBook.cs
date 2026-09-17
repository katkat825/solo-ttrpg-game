using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Entities
{
    public sealed class EntityBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Entity> _entities =
            new Dictionary<string, Entity>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        EntityBook(string campaign)
        {
            Campaign = campaign ?? "";
        }

        public string Campaign { get; }

        public IReadOnlyCollection<string> Ids => _entities.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public int Count => _entities.Count;

        // takes the local id a place writes, the only form a place ever has
        public bool Has(string local) => Of(local) != null;

        public Entity Of(string local)
        {
            if (string.IsNullOrEmpty(local)) return null;

            string id = ContentId.IsScoped(local) || Campaign.Length == 0
                ? local
                : ContentId.Scoped(Campaign, local);

            return _entities.TryGetValue(id, out Entity entity) ? entity : null;
        }

        public IEnumerable<Entity> All =>
            _entities.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _entities[i]);

        public IEnumerable<string> Keys() => All.SelectMany(e => e.Keys());


        public static EntityBook Read(string folder, string campaign)
        {
            var book = new EntityBook(campaign);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return book;

            foreach (string path in Directory
                         .EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
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
                    book._problems.Add(new ContentProblem(
                        name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<Entity> read = EntityReader.Parse(text, name, campaign);

                if (!read.Ok)
                {
                    book._problems.AddRange(read.Problems);
                    continue;
                }

                if (book._entities.ContainsKey(read.Value.Id))
                {
                    book._problems.Add(new ContentProblem(
                        name, "id",
                        $"'{read.Value.Local}' is already the id of another entity in this folder"));
                    continue;
                }

                book._entities[read.Value.Id] = read.Value;
            }

            return book;
        }

        public override string ToString() =>
            $"{_entities.Count} entities" +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
