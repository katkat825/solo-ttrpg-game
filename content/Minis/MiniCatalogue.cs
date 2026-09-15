using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Minis
{
    public sealed class MiniCatalogue
    {
        readonly Dictionary<string, MiniManifest> _minis =
            new Dictionary<string, MiniManifest>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        MiniCatalogue() { }

        public IReadOnlyCollection<string> Ids => _minis.Keys;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public bool Has(string id) => id != null && _minis.ContainsKey(id);

        // null rather than throw; a missing figure is a placeholder box, not a stopped game
        public MiniManifest Of(string id) =>
            id != null && _minis.TryGetValue(id, out MiniManifest mini) ? mini : null;

        public IEnumerable<MiniManifest> All =>
            _minis.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => m.Value);


        public static MiniCatalogue Of(IEnumerable<MiniManifest> minis)
        {
            var catalogue = new MiniCatalogue();

            foreach (MiniManifest mini in minis ?? Array.Empty<MiniManifest>())
                if (mini?.Id != null) catalogue._minis[mini.Id] = mini;

            return catalogue;
        }


        // a missing folder is fine; a minis/ that exists and holds something broken is not
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

        // sorted; a load order that differs between machines is a bug nobody can reproduce
        static IEnumerable<string> Files(string folder) =>
            Directory.EnumerateFiles(folder, "*" + MiniReader.Extension, SearchOption.TopDirectoryOnly)
                     .OrderBy(Path.GetFileName, StringComparer.Ordinal);

        public override string ToString() =>
            $"{_minis.Count} minis" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
