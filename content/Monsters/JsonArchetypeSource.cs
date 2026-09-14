using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;
using Core.Characters;

namespace Content.Monsters
{
    // THE DATA-BACKED SOURCE THE SEAM HAS BEEN WAITING FOR (CONTENT_PIPELINE.md P0).
    //
    // `IArchetypeSource` has existed since the rules core did, and `SEAMS.md` section 2a called it
    // "the seam for the entire content plan, used by nothing" - one interface call site against
    // thirty-six static ones. `SEAMS.md` section 10 item 1 routed the consumers through it. This
    // is the other half: something behind it that is not a C# file full of numbers.
    //
    // `BuiltInArchetypes` DOES NOT GO. It stays as the engine's own shared roster - the Rabble and
    // the Rival that belong to no campaign - and as what the tests and the sim measure against.
    // Both implement the same interface and `Rosters.Of` puts them side by side, which is what
    // makes a campaign's ghoul and the engine's rabble stand on the same board.
    //
    // ONE FOLDER OF JSON FILES, one statblock each. Not one big file: a folder diffs, merges and
    // reviews per monster, and a campaign author adding a ghoul should not touch a file that also
    // has the boss in it.
    //
    // IT NEVER THROWS ON BAD CONTENT. A folder with three good files and one broken one loads
    // three monsters and reports one problem, because a broken campaign is the normal case once
    // players write them and the whole point of the isolation boundary is that one bad file fails
    // alone and named (`CONTENT_PIPELINE.md`).
    public sealed class JsonArchetypeSource : IArchetypeSource
    {
        public const string Extension = ".json";

        readonly Dictionary<string, Statblock> _statblocks =
            new Dictionary<string, Statblock>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        JsonArchetypeSource(string campaign) => Campaign = campaign ?? "";

        // which campaign these came out of, or empty for an unscoped roster
        public string Campaign { get; }

        public IReadOnlyCollection<string> Ids => _statblocks.Keys;

        // everything wrong with the folder, all of it, at once
        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _statblocks.ContainsKey(id);

        // always a new instance - actors are mutable, and a shared one would let a wounded ghoul
        // turn up already wounded
        public Actor Create(string id) =>
            _statblocks.TryGetValue(id, out Statblock block)
                ? block.Create()
                : throw new KeyNotFoundException($"No archetype '{id}'.");

        // the recipe rather than a creature, for anything that wants the facts an `Actor` does not
        // carry. Null for an id this source does not have
        public Statblock Of(string id) =>
            id != null && _statblocks.TryGetValue(id, out Statblock block) ? block : null;

        // ---- reading a folder ----

        // A MISSING FOLDER IS NOT A PROBLEM. A campaign with no monsters of its own is a perfectly
        // good campaign - it can use the engine's roster - and a game with no campaigns installed
        // has to boot. What IS a problem is a folder that exists and contains something broken
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
                    // unreadable is as much a content problem as unparseable, and for an author
                    // the difference is "check the file" either way
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

        // SORTED, because the order a directory hands back its entries is the filesystem's opinion
        // and a load order that changes between machines is a bug nobody can reproduce
        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{_statblocks.Count} statblocks" +
            (Campaign.Length > 0 ? $" from {Campaign}" : "") +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
