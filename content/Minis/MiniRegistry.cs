using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Minis
{
    public sealed class MiniRegistry
    {
        // a second, cheaper bound on top of the cycle check, so a very long legal chain still resolves fast
        public const int LongestChain = 8;

        readonly Dictionary<string, Entry> _minis = new Dictionary<string, Entry>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        // tracks the pack folder because a model path and foley folder are relative to it
        sealed class Entry
        {
            public Entry(MiniManifest mini, string folder)
            {
                Mini = mini;
                Folder = folder ?? "";
            }

            public MiniManifest Mini { get; }

            public string Folder { get; }
        }

        // shared roster added first so a stray un-scoped id can't displace it; Add refuses a second claim
        public MiniRegistry()
        {
            foreach (MiniManifest mini in SharedMinis.All) _minis[mini.Id] = new Entry(mini, "");
        }

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public IReadOnlyCollection<string> Ids => _minis.Keys;

        public bool Has(string id) => id != null && _minis.ContainsKey(id);

        public MiniManifest Of(string id) =>
            id != null && _minis.TryGetValue(id, out Entry entry) ? entry.Mini : null;

        // id order, so a report reads the same way twice
        public IEnumerable<MiniManifest> All =>
            _minis.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => m.Value.Mini);


        public void Add(MiniCatalogue catalogue, string folder)
        {
            if (catalogue == null) return;

            foreach (MiniManifest mini in catalogue.All)
            {
                if (_minis.ContainsKey(mini.Id))
                {
                    // with scoped ids there's never a second; a collision here means scoping was skipped somewhere
                    _problems.Add(new ContentProblem(
                        Campaigns.Package.MinisFolder, mini.Id,
                        $"'{mini.Id}' is already the id of another mini that is loaded - one of " +
                        "them is not namespaced by its pack"));
                    continue;
                }

                _minis[mini.Id] = new Entry(mini, folder);
            }
        }


        // null is an answer, not a failure; the board stands a box and says which mini was missing
        public Mounted Mount(string id) => Mount(id, out _);

        public Mounted Mount(string id, out string why)
        {
            why = "";

            if (string.IsNullOrWhiteSpace(id))
            {
                why = "nothing names a mini";
                return null;
            }

            if (!_minis.TryGetValue(id, out Entry entry))
            {
                why = $"there is no mini '{id}' - the pack that ships it may not be installed";
                return null;
            }

            // nearest wins: the asked-for mini gets the last word and inherits the rest
            Fit? fit = null;
            float? height = null;
            float? foot = null;
            Tint tint = Tint.None;

            var clips = new Dictionary<Motion, string>();
            var foley = new Dictionary<Motion, string>();

            var seen = new List<string>();

            for (int link = 0; ; link++)
            {
                MiniManifest mini = entry.Mini;

                // cycle caught before it's walked twice, named with the whole ring so an author can act on it
                if (seen.Contains(mini.Id, StringComparer.Ordinal))
                {
                    why = "these minis are variants of each other and nothing in the ring has a " +
                          $"model: {string.Join(" -> ", seen)} -> {mini.Id}";
                    Note(id, why);
                    return null;
                }

                seen.Add(mini.Id);

                fit ??= mini.Fit;
                height ??= mini.Height;
                foot ??= mini.Foot;

                if (!tint.IsSomething) tint = mini.Tint;

                Inherit(clips, mini.Clips, entry.Folder, scoped: false);
                Inherit(foley, mini.Foley, entry.Folder, scoped: true);

                // end of the chain: either a mini with a model of its own or one of the shared roster's
                if (!mini.IsVariant)
                    return new Mounted(id, mini.Id, entry.Folder, mini.Model,
                                       fit ?? Fit.Cell, height ?? 0f, foot ?? 0f, tint,
                                       clips, foley);

                if (link >= LongestChain)
                {
                    why = $"'{id}' is a variant of a variant {LongestChain} deep and still has no " +
                          $"model - {string.Join(" -> ", seen)}";
                    Note(id, why);
                    return null;
                }

                if (!_minis.TryGetValue(mini.Variant, out entry))
                {
                    why = $"'{mini.Id}' is a variant of '{mini.Variant}', and there is no such " +
                          "mini - the pack that ships it may not be installed, or this pack may " +
                          "need to declare it in its dependencies";
                    Note(mini.Id, why);
                    return null;
                }
            }
        }

        // a clip name is the model's, a foley folder is the pack's; make a folder absolute at the link that wrote it
        static void Inherit(Dictionary<Motion, string> into, IReadOnlyDictionary<Motion, string> from,
                            string folder, bool scoped)
        {
            foreach (KeyValuePair<Motion, string> said in from)
            {
                if (into.ContainsKey(said.Key)) continue;

                into[said.Key] = scoped && folder.Length > 0
                    ? System.IO.Path.Combine(folder, said.Value.Replace('/', System.IO.Path.DirectorySeparatorChar))
                    : said.Value;
            }
        }

        void Note(string id, string why) =>
            _problems.Add(new ContentProblem(Campaigns.Package.MinisFolder, id, why));

        public override string ToString() =>
            $"{_minis.Count} minis" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
