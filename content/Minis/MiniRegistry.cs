using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Schema;

namespace Content.Minis
{
    // A MINI ID, RESOLVED TO SOMETHING THAT CAN BE STOOD ON THE BOARD (MINIS_AND_ART.md A1).
    //
    // "A mini registry in the loader: resolves a mini id to a loaded, treated model, across every
    // pack root, with the shared roster as the fallback source." This is the registry, minus the
    // word "loaded" - because loading is Godot's job and everything up to it is arithmetic and
    // lookup, which belongs here where it can be tested headlessly (CONVENTIONS.md, "where does a
    // helper go").
    //
    // WHAT IT ACTUALLY DOES IS WALK A CHAIN. A0's variants are the point of the whole phase, and a
    // variant is a mini defined in terms of another mini, which may itself be a variant. So
    // resolving `grimdark.oldbones` means following `variant` until something has a MODEL or is
    // one of the shared roster's, collecting overrides on the way down - the asking mini's own
    // words win, and everything it did not say falls through.
    //
    // THE CHAIN IS WHERE THE TWO INTERESTING FAILURES LIVE, and both are refused by name rather
    // than by hanging:
    //
    //   a cycle        two packs varying each other, or a mini varying itself. A `for` loop with
    //                  no bound here is a game that stops responding on a subscribed folder
    //                  somebody else wrote, which is the worst failure mode this phase can have
    //   a dangling id  a variant of a mini in a pack that is not installed. Ordinary, and the
    //                  answer is the placeholder box with a sentence beside it, not a crash
    //
    // NOTHING HERE THROWS. `Mount` hands back null for an id it cannot resolve and says why in
    // `Problems`; the caller stands a box in the square (`A1`, and `BoardTiles`' "where there is
    // no model the boxes are still here"). That is the isolation boundary for art.
    public sealed class MiniRegistry
    {
        // HOW DEEP A VARIANT CHAIN MAY GO. Not a cycle check - the visited set below is that - but
        // a second, cheaper bound, so that a thousand-long legal chain assembled by a pack that
        // meant no harm still resolves in a bounded time. Four is generous: the gesture A0
        // describes is one link, and a variant of a variant of a variant is already a pack that
        // wants a model
        public const int LongestChain = 8;

        readonly Dictionary<string, Entry> _minis = new Dictionary<string, Entry>(StringComparer.Ordinal);

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        // WHERE ONE MINI CAME FROM, which the manifest cannot say on its own: a model path and a
        // foley folder are relative to the PACK, and a registry that has forgotten which pack is a
        // registry that can only resolve names and not files
        sealed class Entry
        {
            public Entry(MiniManifest mini, string folder)
            {
                Mini = mini;
                Folder = folder ?? "";
            }

            public MiniManifest Mini { get; }

            // the pack folder on disk, or empty for one of the shared roster's
            public string Folder { get; }
        }

        // THE SHARED ROSTER IS ALWAYS IN, AND IS ALWAYS THE FALLBACK. Added first so that a pack
        // that somehow declared an un-scoped id cannot displace it - `Add` refuses a second claim
        // on an id rather than letting load order decide (`Rosters` makes the same call out loud)
        public MiniRegistry()
        {
            foreach (MiniManifest mini in SharedMinis.All) _minis[mini.Id] = new Entry(mini, "");
        }

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public IReadOnlyCollection<string> Ids => _minis.Keys;

        public bool Has(string id) => id != null && _minis.ContainsKey(id);

        public MiniManifest Of(string id) =>
            id != null && _minis.TryGetValue(id, out Entry entry) ? entry.Mini : null;

        // every mini in the registry, in id order, so a report reads the same way twice
        public IEnumerable<MiniManifest> All =>
            _minis.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => m.Value.Mini);

        // ---- filling it ----

        // <paramref name="folder"/> is the pack's own folder on disk; the paths in its manifests
        // are relative to it
        public void Add(MiniCatalogue catalogue, string folder)
        {
            if (catalogue == null) return;

            foreach (MiniManifest mini in catalogue.All)
            {
                if (_minis.ContainsKey(mini.Id))
                {
                    // WITH SCOPED IDS THERE IS NEVER A SECOND, so this is not a rule about who
                    // wins - it is a report that the scoping was skipped somewhere, which is worth
                    // being told about rather than resolved silently (Rosters.Collisions)
                    _problems.Add(new ContentProblem(
                        Campaigns.Package.MinisFolder, mini.Id,
                        $"'{mini.Id}' is already the id of another mini that is loaded - one of " +
                        "them is not namespaced by its pack"));
                    continue;
                }

                _minis[mini.Id] = new Entry(mini, folder);
            }
        }

        // ---- and asking it something ----

        // NULL IS AN ANSWER AND NOT A FAILURE. "That pack is not installed" is the ordinary state
        // of a shelf, and what the board does about it is stand a box on the square and say which
        // mini was missing - never a crash, never a stopped fight (A1)
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

            // THE OVERRIDES, NEAREST FIRST. The mini that was ASKED for gets the last word, which
            // is what makes a variant a variant: it states the handful of things it changes and
            // inherits the rest
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

                // THE CYCLE, CAUGHT BEFORE IT IS WALKED TWICE. Named with the whole ring, because
                // "grimdark.a -> grimdark.b -> grimdark.a" is a sentence an author can act on and
                // "a cycle was detected" is not
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

                // THE END OF THE CHAIN, and there are exactly two kinds of end: a mini with a
                // model file of its own, and one of the shared roster's, which has no file because
                // what it stands as is a scene in game/ (SharedMinis)
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

        // A CLIP NAME IS THE MODEL'S AND A FOLEY FOLDER IS THE PACK'S, which is the one asymmetry
        // between the two maps: "Walk_A" means the same thing wherever it is written because it is
        // read out of the model file, and "audio/bones/placed" means a different folder in every
        // pack. So a folder is made absolute AT THE LINK THAT WROTE IT, before it is inherited
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

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{_minis.Count} minis" + (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
