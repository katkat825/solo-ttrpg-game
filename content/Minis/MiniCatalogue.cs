using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Minis
{
    // EVERY MINI ONE PACK SHIPS (MINIS_AND_ART.md A1).
    //
    // The same shape as `ItemCatalogue` and `JsonArchetypeSource`: a folder of JSON files, one
    // mini each, read whole, with every problem collected and nothing thrown. A folder diffs and
    // reviews per mini, and an author adding a skeleton should not open the file that also has the
    // boss in it.
    //
    // IT RESOLVES NOTHING. A mini can be a variant of a mini in another pack, and no single folder
    // can answer whether that one exists - that is `MiniRegistry`, which has every pack open at
    // once, exactly as `Package.CrossCheck` is where "does this campaign ship a ghoul" is asked.
    // What this does is read the folder and say what is wrong with the FILES.
    public sealed class MiniCatalogue
    {
        readonly Dictionary<string, MiniManifest> _minis =
            new Dictionary<string, MiniManifest>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        MiniCatalogue() { }

        public IReadOnlyCollection<string> Ids => _minis.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _minis.ContainsKey(id);

        // null rather than an exception: "the pack that had that figure is not installed" is an
        // ordinary thing for a statblock to run into, and the answer to it is a placeholder box
        // rather than a stopped game (A1, the fallback)
        public MiniManifest Of(string id) =>
            id != null && _minis.TryGetValue(id, out MiniManifest mini) ? mini : null;

        public IEnumerable<MiniManifest> All =>
            _minis.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => m.Value);

        // ---- built from manifests, for the shared roster and for tests ----

        public static MiniCatalogue Of(IEnumerable<MiniManifest> minis)
        {
            var catalogue = new MiniCatalogue();

            foreach (MiniManifest mini in minis ?? Array.Empty<MiniManifest>())
                if (mini?.Id != null) catalogue._minis[mini.Id] = mini;

            return catalogue;
        }

        // ---- or read off a disk ----

        // A MISSING FOLDER IS NOT A PROBLEM, the same way a campaign with no monsters of its own
        // is a perfectly good campaign. Most campaigns will ship no minis at all and stand their
        // monsters on the shared roster's figures; what IS a problem is a `minis/` that exists and
        // has something broken in it
        public static MiniCatalogue Read(string folder, string pack)
        {
            var catalogue = new MiniCatalogue();

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
                    catalogue._problems.Add(new ContentProblem(
                        name, "", "could not be read - " + could.Message));
                    continue;
                }

                Read<MiniManifest> read = MiniReader.Parse(text, name, pack);

                if (!read.Ok)
                {
                    catalogue._problems.AddRange(read.Problems);
                    continue;
                }

                if (catalogue._minis.ContainsKey(read.Value.Id))
                {
                    catalogue._problems.Add(new ContentProblem(
                        name, "id",
                        $"'{read.Value.Id}' is already the id of another mini in this folder"));
                    continue;
                }

                catalogue._minis[read.Value.Id] = read.Value;
            }

            return catalogue;
        }

        // SORTED, because the order a directory hands back its entries is the filesystem's opinion
        // and a load order that differs between machines is a bug nobody can reproduce
        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + MiniReader.Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{_minis.Count} minis" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
