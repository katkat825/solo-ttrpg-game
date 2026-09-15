using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Encounters
{
    public sealed class EncounterBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, EncounterPlan> _plans =
            new Dictionary<string, EncounterPlan>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        public IReadOnlyCollection<string> Ids => _plans.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _plans.ContainsKey(id);

        // null rather than throw; a save may name an encounter from a campaign nobody installed
        public EncounterPlan Of(string id) =>
            id != null && _plans.TryGetValue(id, out EncounterPlan plan) ? plan : null;

        public IEnumerable<EncounterPlan> All =>
            _plans.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _plans[i]);


        public static EncounterBook Read(string folder)
        {
            var book = new EncounterBook();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return book;

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
                    book._problems.Add(new ContentProblem(
                        name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<EncounterPlan> read = EncounterReader.Parse(text, name);

                if (!read.Ok)
                {
                    book._problems.AddRange(read.Problems);
                    continue;
                }

                book.Add(read.Value, name);
            }

            return book;
        }

        void Add(EncounterPlan plan, string file)
        {
            if (_plans.ContainsKey(plan.Id))
            {
                _problems.Add(new ContentProblem(
                    file, "id",
                    $"'{plan.Id}' is already the id of another encounter in this folder"));
                return;
            }

            _plans[plan.Id] = plan;
        }

        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        public override string ToString() =>
            $"{_plans.Count} encounters" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
