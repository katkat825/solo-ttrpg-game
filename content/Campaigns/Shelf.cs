using System;
using System.Collections.Generic;
using System.Linq;
using Content.Minis;
using Content.Schema;

namespace Content.Campaigns
{
    // EVERY PACK ON THIS MACHINE, SIDE BY SIDE (MINIS_AND_ART.md A4, MODDING.md section 2).
    //
    // `Package` reads ONE folder and answers every question a folder can answer on its own.
    // Dependencies are the first question it cannot: "is `grimdark_minis` installed" is about the
    // shelf, not about the campaign asking. So this is the layer above - several packages, the
    // mini registry built across all of them, and the two cross-pack questions asked once.
    //
    // IT IS THE SAME SHAPE AS `Package.CrossCheck`, ONE LEVEL UP. A reader sees one file and
    // cannot say whether the campaign ships a ghoul; a package sees one folder and cannot say
    // whether the shelf ships a skeleton. Each level asks exactly what it can see, and says so by
    // name.
    //
    // A MISSING DEPENDENCY TAKES THE PACK OUT OF PLAY, AND ONLY THAT PACK. `MINIS_AND_ART.md` A4:
    // "reports a missing dependency by name rather than half-loading the campaign" - a campaign
    // whose art pack is not installed would otherwise load with a box where every monster should
    // be, which looks like a broken game rather than a missing subscription. So it stays on the
    // shelf, `Missing` says what it needs, and nothing of it is registered: the same all-or-
    // nothing rule `Package.Failed` already follows, for a reason one folder could not see.
    //
    // IN `content/` AND NOT IN `game/`, like everything else here: a shelf is a list of folders,
    // and which folders are on it is the only part that needs to know where a Steam directory is.
    public sealed class Shelf
    {
        readonly List<Entry> _entries = new List<Entry>();

        readonly List<ContentProblem> _problems = new List<ContentProblem>();

        Shelf() { }

        // ONE PACK AND WHAT THE SHELF DECIDED ABOUT IT. The package is the folder; everything else
        // here is a fact about its neighbours
        public sealed class Entry
        {
            public Entry(Package package, IReadOnlyList<string> missing)
            {
                Package = package;
                Missing = missing ?? Array.Empty<string>();
            }

            public Package Package { get; }

            public string Id => Package.Id;

            // the ids of packs it declared and the shelf has not got, in the order it declared them
            public IReadOnlyList<string> Missing { get; }

            // A PACK IS IN PLAY WHEN ITS FOLDER READ AND EVERYTHING IT NEEDS IS HERE. Anything
            // else is on the shelf and contributes nothing - no monsters, no items, no minis
            public bool InPlay => !Package.Failed && Missing.Count == 0;

            // DEVELOPER ONLY - not localized, never reaches a player
            public override string ToString() =>
                Missing.Count > 0
                    ? $"{Package} - NOT LOADED, it needs {string.Join(", ", Missing)}"
                    : Package.ToString();
        }

        public IReadOnlyList<Entry> Entries => _entries;

        public IReadOnlyList<ContentProblem> Problems => _problems;

        // every pack that is actually in play, in the order the roots were walked
        public IEnumerable<Package> Loaded => _entries.Where(e => e.InPlay).Select(e => e.Package);

        // THE MINI REGISTRY, BUILT ACROSS EVERY ROOT (A1). The shared roster is already in it;
        // what this adds is each loaded pack's own, scoped by that pack's id
        public MiniRegistry Minis { get; } = new MiniRegistry();

        public Entry Of(string id) =>
            id == null ? null : _entries.FirstOrDefault(e => e.Id == id);

        // ---- assembling one ----

        public static Shelf Of(IEnumerable<Package> packages)
        {
            var shelf = new Shelf();

            var read = new List<Package>(packages?.Where(p => p != null) ?? Array.Empty<Package>());

            // WHICH IDS ARE PRESENT, WORKED OUT BEFORE ANY DEPENDENCY IS RESOLVED, because a
            // dependency does not care what order the folders were walked in. A shelf where
            // `a` needs `b` must load `a` whether `b` sorts before or after it
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

                        // NAMED, AND NAMED AS THE THING A PLAYER CAN DO SOMETHING ABOUT. "It needs
                        // grimdark_minis" is a sentence that ends in subscribing to something; "a
                        // mini failed to resolve" is one that ends in a forum post
                        shelf._problems.Add(new ContentProblem(
                            package.Id + "/" + package.ManifestFile, "dependencies",
                            $"'{package.Id}' needs the pack '{needed}', and it is not installed - " +
                            "nothing of this pack is loaded until it is. Subscribe to it, or " +
                            "take the line out if it is no longer needed"));
                    }

                shelf._entries.Add(new Entry(package, missing));
            }

            // AND ONLY THEN THE MINIS, so that a pack which is not in play contributes none. A
            // half-loaded pack's minis in the registry would be exactly the "campaign that loads
            // with boxes where the figures should be" this milestone exists to prevent
            foreach (Entry entry in shelf._entries)
            {
                if (!entry.InPlay) continue;

                shelf.Minis.Add(entry.Package.Minis, entry.Package.Folder);
            }

            shelf._problems.AddRange(shelf.Minis.Problems);

            shelf.CrossCheck();

            return shelf;
        }

        // ---- and the question only the shelf can answer ----

        // EVERY MINI ID ANYTHING NAMES, FOLLOWED TO SOMETHING THAT CAN STAND ON A SQUARE. A
        // package already checked the ids it could see; what it deliberately let through were the
        // dotted ones belonging to other packs, and this is where those land.
        //
        // IT IS NOT FATAL. An unresolved mini is a placeholder box in the right square with a
        // sentence on the shelf - `MINIS_AND_ART.md`: "a missing or unresolved id falls back to
        // the placeholder box - the piece is where it should be, the fight is playable, and the
        // shelf tile says which mini failed"
        void CrossCheck()
        {
            foreach (Entry entry in _entries)
            {
                if (!entry.InPlay) continue;

                Package package = entry.Package;

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

                // AND THE VARIANTS THAT REACH ACROSS PACKS, which are the other half of the same
                // question and the one that can loop. `MiniRegistry.Mount` is what refuses a ring
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

        // THE ONE PLACE A BARE MINI NAME IS RESOLVED, and the order is the one an author would
        // expect: my own pack's first, then the ones the game ships. Same shape as
        // `ItemCatalogue`'s shared gear namespace, and stated here rather than left to whichever
        // lookup ran first
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

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{_entries.Count(e => e.InPlay)} of {_entries.Count} packs in play, {Minis}" +
            (_problems.Count > 0 ? $", {_problems.Count} problems" : "");
    }
}
