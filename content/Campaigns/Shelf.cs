using System;
using System.Collections.Generic;
using System.Linq;
using Content.Minis;
using Content.Schema;

namespace Content.Campaigns
{
    public sealed class Shelf
    {
        readonly List<Entry> _entries = new List<Entry>();

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        Shelf() { }

        public sealed class Entry
        {
            public Entry(Package package, IReadOnlyList<string> missing)
            {
                Package = package;
                Missing = missing ?? Array.Empty<string>();
            }

            public Package Package { get; }

            public string Id => Package.Id;

            public IReadOnlyList<string> Missing { get; }

            public bool InPlay => !Package.Failed && Missing.Count == 0;

            public override string ToString() =>
                Missing.Count > 0
                    ? $"{Package} - NOT LOADED, it needs {string.Join(", ", Missing)}"
                    : Package.ToString();
        }

        public IReadOnlyList<Entry> Entries => _entries;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        public IEnumerable<Package> Loaded => _entries.Where(e => e.InPlay).Select(e => e.Package);

        public MiniRegistry Minis { get; } = new MiniRegistry();

        public Entry Of(string id) =>
            id == null ? null : _entries.FirstOrDefault(e => e.Id == id);


        public static Shelf Of(IEnumerable<Package> packages)
        {
            var shelf = new Shelf();

            var read = new List<Package>(packages?.Where(p => p != null) ?? Array.Empty<Package>());

            // all present ids first, so a dependency resolves regardless of folder walk order
            var present = new HashSet<string>(
                read.Where(p => !p.Failed).Select(p => p.Id), StringComparer.Ordinal);

            foreach (Package package in read)
            {
                var missing = new List<string>();

                if (!package.Failed)
                    foreach (string needed in package.Manifest.Dependencies)
                    {
                        if (present.Contains(needed)) continue;

                        missing.Add(needed);

                        shelf._problems.Add(new ContentProblem(
                            package.Id + "/" + package.ManifestFile, "dependencies",
                            $"'{package.Id}' needs the pack '{needed}', and it is not installed - " +
                            "nothing of this pack is loaded until it is. Subscribe to it, or " +
                            "take the line out if it is no longer needed"));
                    }

                shelf._entries.Add(new Entry(package, missing));
            }

            // after dependency resolution, so a pack not in play contributes no minis
            foreach (Entry entry in shelf._entries)
            {
                if (!entry.InPlay) continue;

                shelf.Minis.Add(entry.Package.Minis, entry.Package.Folder);
            }

            shelf._problems.AddRange(shelf.Minis.Problems);

            shelf.CrossCheck();

            return shelf;
        }


        // every creature anything on this shelf can speak as. A voice is shelf-wide by design: a
        // class pack can ship the wolf and a campaign can ship the wolf's lines, and neither pack
        // knows the other exists (Phase W).
        public IEnumerable<string> Voices =>
            Loaded.SelectMany(p => p.Voices)
                  .Distinct(StringComparer.Ordinal)
                  .OrderBy(v => v, StringComparer.Ordinal);

        // cross-pack mini ids a single package couldn't resolve; unresolved is a placeholder box, not fatal
        void CrossCheck()
        {
            var voices = new HashSet<string>(Voices, StringComparer.Ordinal);

            foreach (Entry entry in _entries)
            {
                if (!entry.InPlay) continue;

                Package package = entry.Package;

                // a conversation keyed under a creature nothing on this shelf can be: the lines
                // exist, the voice does not, and the words end up filed under a companion with no
                // bark bank and nowhere to sit
                foreach (string node in package.Dialogue.Nodes.OrderBy(n => n, StringComparer.Ordinal))
                {
                    string speaker = package.Dialogue.SpeakerOf(node);

                    if (speaker == null || voices.Contains(speaker)) continue;

                    _problems.Add(new ContentProblem(
                        package.Id + "/" + Package.DialogueFolder + "/",
                        node + "." + Content.Dialogue.DialogueBook.SpeakerHeader,
                        $"'{speaker}' is not a voice anything installed can be - this shelf speaks " +
                        $"as {(voices.Count == 0 ? "nobody at all" : string.Join(", ", voices))}. " +
                        "Ship a companion or a bark bank for it, or name the pack that does in " +
                        "dependencies"));
                }

                foreach (string id in package.Monsters.Ids.OrderBy(i => i, StringComparer.Ordinal))
                {
                    string named = package.Monsters.Of(id)?.MiniId;

                    if (string.IsNullOrEmpty(named)) continue;

                    if (Stands(package.Id, named, out string why)) continue;

                    _problems.Add(new ContentProblem(
                        package.Id + "/" + Package.MonstersFolder + "/" +
                        ContentId.LocalOf(id) + ".json",
                        "mini",
                        why + " - this monster will stand on a placeholder box, and the fight is " +
                        "playable either way"));
                }

                // variants that reach across packs; Mount refuses a cycle
                foreach (MiniManifest mini in package.Minis.All)
                {
                    if (Minis.Mount(mini.Id, out string why) != null) continue;

                    _problems.Add(new ContentProblem(
                        package.Id + "/" + Package.MinisFolder + "/" +
                        ContentId.LocalOf(mini.Id) + MiniReader.Extension,
                        "variant", why));
                }
            }
        }

        // bare names resolve to my own pack first, then what the game ships
        public Mounted Mount(string pack, string named) => Mount(pack, named, out _);

        public Mounted Mount(string pack, string named, out string why)
        {
            why = "";

            if (string.IsNullOrEmpty(named)) return null;

            if (ContentId.IsScoped(named)) return Minis.Mount(named, out why);

            if (!string.IsNullOrEmpty(pack) && ContentId.IsLocal(named))
            {
                string mine = ContentId.Scoped(pack, named);

                if (Minis.Has(mine)) return Minis.Mount(mine, out why);
            }

            return Minis.Mount(named, out why);
        }

        bool Stands(string pack, string named, out string why) =>
            Mount(pack, named, out why) != null;

        public override string ToString() =>
            $"{_entries.Count(e => e.InPlay)} of {_entries.Count} packs in play, {Minis}" +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
