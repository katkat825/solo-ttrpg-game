using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Places
{
    public sealed class RoadBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Road> _roads = new Dictionary<string, Road>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        public IReadOnlyCollection<string> Ids => _roads.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _roads.ContainsKey(id);

        public Road Of(string id) =>
            id != null && _roads.TryGetValue(id, out Road road) ? road : null;

        public IEnumerable<Road> All =>
            _roads.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _roads[i]);

        public int Count => _roads.Count;

        public IEnumerable<Road> From(string place) =>
            All.Where(r => r.From == place || r.To == place);


        public static RoadBook Read(string folder)
        {
            var book = new RoadBook();

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

                Read<Road> read = RoadReader.Parse(text, name);

                if (!read.Ok)
                {
                    book._problems.AddRange(read.Problems);
                    continue;
                }

                if (book._roads.ContainsKey(read.Value.Id))
                {
                    book._problems.Add(new ContentProblem(
                        name, "id",
                        $"'{read.Value.Id}' is already the id of another road in this folder"));
                    continue;
                }

                book._roads[read.Value.Id] = read.Value;
            }

            return book;
        }

        public override string ToString() =>
            $"{_roads.Count} roads" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
