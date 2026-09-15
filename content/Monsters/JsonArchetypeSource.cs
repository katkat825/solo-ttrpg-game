using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Campaigns;
using Content.Schema;
using Core.Characters;

namespace Content.Monsters
{
    public sealed class JsonArchetypeSource : IArchetypeSource, IPackRoster
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Statblock> _statblocks =
            new Dictionary<string, Statblock>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        JsonArchetypeSource(string campaign) => Campaign = campaign ?? "";

        // empty for an unscoped roster
        public string Campaign { get; }

        // a forward, not a rename: call sites reading monsters want Campaign; the audit wants Pack
        string IPackRoster.Pack => Campaign;

        public IReadOnlyCollection<string> Ids => _statblocks.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _statblocks.ContainsKey(id);

        // always a new instance; actors are mutable and a shared ghoul would arrive already wounded
        public Actor Create(string id) =>
            _statblocks.TryGetValue(id, out Statblock block)
                ? block.Create()
                : throw new KeyNotFoundException($"No archetype '{id}'.");

        public Statblock Of(string id) =>
            id != null && _statblocks.TryGetValue(id, out Statblock block) ? block : null;


        // a missing folder is fine; a folder that exists and holds something broken is not
        public static JsonArchetypeSource Read(string folder, string campaign)
        {
            var source = new JsonArchetypeSource(campaign);

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return source;

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
                    source._problems.Add(new ContentProblem(name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<Statblock> read = StatblockReader.Parse(text, name, campaign);

                if (!read.Ok)
                {
                    source._problems.AddRange(read.Problems);
                    continue;
                }

                if (source._statblocks.ContainsKey(read.Value.Id))
                {
                    source._problems.Add(new ContentProblem(
                        name, "id",
                        $"'{read.Value.Id}' is already the id of another statblock in this folder"));
                    continue;
                }

                source._statblocks[read.Value.Id] = read.Value;
            }

            return source;
        }

        // sorted; a load order that changes between machines is a bug nobody can reproduce
        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        public override string ToString() =>
            $"{_statblocks.Count} statblocks" +
            (Campaign.Length > 0 ? $" from {Campaign}" : "") +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
