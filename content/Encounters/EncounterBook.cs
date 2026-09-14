using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Encounters
{
    // EVERY ENCOUNTER A CAMPAIGN SHIPS (CONTENT_PIPELINE.md P4).
    //
    // The same shape as `ItemCatalogue` and `JsonArchetypeSource`, for the third time and
    // deliberately: a folder of JSON files, one per thing, read whole, every problem collected and
    // nothing thrown. Three folders that behave identically are a thing an author learns once.
    //
    // NOT FOLDED ACROSS CAMPAIGNS, which is the one way this differs from the other two. Monster
    // and gear ids end up in one shared roster because a fight has to look up `ashfall.ghoul`
    // without knowing where it came from. An encounter is only ever named by its own campaign's
    // `campaign.json`, so there is nothing to collide with and no reason to flatten - `the_yard`
    // in two campaigns is two encounters, and both keep their name.
    public sealed class EncounterBook
    {
        public const string Extension = ".json";

        readonly Dictionary<string, EncounterPlan> _plans =
            new Dictionary<string, EncounterPlan>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        public IReadOnlyCollection<string> Ids => _plans.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _plans.ContainsKey(id);

        // null rather than an exception, for the reason `ItemCatalogue.Of` gives: a save that
        // names an encounter out of a campaign nobody has installed is an ordinary thing (P6)
        public EncounterPlan Of(string id) =>
            id != null && _plans.TryGetValue(id, out EncounterPlan plan) ? plan : null;

        public IEnumerable<EncounterPlan> All =>
            _plans.Keys.OrderBy(i => i, StringComparer.Ordinal).Select(i => _plans[i]);

        // ---- reading a folder ----

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

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{_plans.Count} encounters" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
