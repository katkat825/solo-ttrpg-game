using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Campaigns;
using Content.Items;
using Content.Minis;
using Content.Monsters;
using Content.Schema;
using Core.Characters;

namespace Game.Campaigns
{
    // THE ONE LINE EVERY COMPOSITION ROOT HAS BEEN PROMISING (CONTENT_PIPELINE.md P0).
    //
    // Five files in this project said some version of "the one place this names a concrete roster
    // - swapping in a data-backed source is this line and nothing else". This is that swap, made
    // once instead of five times: the engine's own roster, plus every campaign folder found on
    // disk, behind the same `IArchetypeSource` nothing downstream stops holding.
    //
    // A FACTORY AND NOT A SINGLETON. `Load` builds a fresh roster and hands it over; nothing here
    // holds mutable state and nothing reaches for a global (CONVENTIONS.md 4). Five callers
    // calling it is five rosters, which costs a directory listing each and keeps every one of them
    // able to be handed a different set of roots by a test.
    //
    // A MISSING `campaigns/` IS THE NORMAL CASE TODAY and has to stay a non-event: the game boots,
    // reads nothing, and plays the built-in roster. That is also exactly what "with Steam absent
    // the game still boots" means (ARCHITECTURE.md section 8), reached from the other direction.
    //
    // THE READING ITSELF IS NOT HERE (P4). A campaign folder is read by `Content.Campaigns.Package`,
    // which has no Godot in it and is therefore testable headlessly; what this adds is the two
    // things that need an engine - registering the locale CSVs, and saying out loud what happened.
    // So the isolation boundary lives one layer down and this is the layer that respects it: a
    // failed package is added to the shelf and contributes nothing to the roster.
    public sealed class Library : IArchetypeSource
    {
        Library(Rosters archetypes, ItemCatalogue items,
                IReadOnlyDictionary<string, Statblock> statblocks,
                IReadOnlyList<Loaded> campaigns, Shelf shelf)
        {
            Archetypes = archetypes;
            Items = items;
            Campaigns = campaigns;
            Shelf = shelf;
            _statblocks = statblocks;
        }

        readonly IReadOnlyDictionary<string, Statblock> _statblocks;

        // WHAT CAME OFF THE DISK, still separable. The rosters and the catalogue above are folded
        // together so the game sees one of each; this keeps who brought what, because the locale
        // audit has to ask "does ashfall name everything ashfall named" and a fold cannot answer it
        public IReadOnlyList<Loaded> Campaigns { get; }

        public Rosters Archetypes { get; }

        public ItemCatalogue Items { get; }

        // EVERY PACK SIDE BY SIDE, AND THE ONE THING THAT NEEDED THEM THAT WAY (Phase A). Reading
        // a folder is `Package`; deciding whether the folder next to it is installed, and
        // resolving a mini id across all of them, is `Content.Campaigns.Shelf` - which is
        // Godot-free like everything else in `content/`, so what this class adds to it is still
        // exactly the two things that need an engine: the locale CSVs, and saying what happened
        public Shelf Shelf { get; }

        // what a monster stands as, resolved across every pack, with the shared roster as the
        // fallback. Null for an id nothing ships, which is a placeholder box and never a crash
        public Mounted Mount(string pack, string mini) => Shelf?.Mount(pack, mini);

        // ONE CAMPAIGN BY ID, including a failed one - the shelf shows those too
        public Loaded Campaign(string id) =>
            id == null ? null : Campaigns.FirstOrDefault(c => c.Id == id);

        // ---- and it IS the seam, so nothing downstream learns there are two halves ----

        public IReadOnlyCollection<string> Ids => Archetypes.Ids;

        public bool Has(string id) => Archetypes.Has(id);

        public Actor Create(string id) => Archetypes.Create(id);

        // WHAT A MONSTER WAS CARRYING. Only a statblock knows, and only a JSON one has one - the
        // engine's built-in roster carries nothing, which is right: loot is content
        public LootTable LootFor(string id) =>
            id != null && _statblocks.TryGetValue(id, out Statblock block) ? block.Loot : LootTable.Nothing;

        // AND WHAT IT STANDS AS (MINIS_AND_ART.md A1). Empty for the engine's own roster and for
        // any campaign that did not say, which is the fallback `MiniMaker` turns into the tiered
        // proxy - a campaign should not have to ship art in order to ship a monster
        public string MiniFor(string id) =>
            id != null && _statblocks.TryGetValue(id, out Statblock block) ? block.MiniId : "";

        // <paramref name="roots"/> defaults to wherever campaigns live on this machine. A test or
        // a check passes its own
        public static Library Load(IReadOnlyList<string> roots = null, bool quiet = false)
        {
            var rosters = new Rosters(new BuiltInArchetypes());
            var items = new ItemCatalogue();
            var statblocks = new Dictionary<string, Statblock>(System.StringComparer.Ordinal);
            var problems = new List<ContentProblem>();
            var loaded = new List<Loaded>();
            var read = new List<Package>();

            foreach (string root in roots ?? CampaignFolders.Roots())
                foreach (string folder in CampaignFolders.In(root))
                    read.Add(Package.Read(folder));

            // EVERY FOLDER READ BEFORE ANY OF THEM IS PUT IN PLAY (Phase A). A dependency does not
            // care what order the roots were walked in - a campaign that needs a mini pack loads
            // whether that pack sorts before or after it - so the shelf is assembled whole and
            // then asked which of its entries are playable
            var shelf = Shelf.Of(read);

            problems.AddRange(shelf.Problems);

            foreach (Shelf.Entry entry in shelf.Entries)
            {
                Package package = entry.Package;

                // NAMED WITH THE CAMPAIGN IT CAME FROM, because a report over eight subscribed
                // items is otherwise a wall of file names with no idea whose they are
                foreach (ContentProblem problem in package.Problems)
                    problems.Add(new ContentProblem(
                        package.Id + "/" + problem.File, problem.Where, problem.What, problem.Line));

                // THE ISOLATION BOUNDARY, and it is still one `if` - it has simply learned a
                // second way to be out of play. A folder that failed to read, and one whose
                // dependencies are not installed, are both on the shelf and both contribute
                // nothing: no monsters, no items, no minis, no strings
                if (!entry.InPlay) { loaded.Add(new Loaded(package, 0, entry.Missing)); continue; }

                if (package.Monsters.Ids.Count > 0) rosters.Add(package.Monsters);

                foreach (string id in package.Monsters.Ids) statblocks[id] = package.Monsters.Of(id);

                // AND WHAT IS LYING AROUND IN IT (P1). Gear ids are a shared namespace, so a
                // second campaign that ships a `torch` is describing the same word and the
                // first one loaded keeps it - said out loud rather than decided quietly
                foreach (string taken in items.Absorb(package.Items))
                    problems.Add(new ContentProblem(
                        package.Id + "/" + Package.ItemsFolder, taken,
                        "another campaign already ships an item with this id, and its version " +
                        "is the one in play - gear ids are shared"));

                // AND ITS STRINGS COME WITH IT. A campaign that names its own monsters carries
                // their names, or it is not self-contained (ARCHITECTURE.md section 4) - and
                // without this the turn order beside the map reads `actor.ashfall.ghoul.name`
                loaded.Add(new Loaded(package, CampaignLocale.Register(package.Folder)));
            }

            problems.AddRange(items.Problems);

            var library = new Library(rosters, items, statblocks, loaded, shelf);

            if (!quiet) Report(library, problems);

            return library;
        }

        // DEVELOPER DIAGNOSTICS, exempt from localization like every other GD.Print in this
        // project. A campaign that failed to load has to SAY so - the whole point of the isolation
        // boundary is that one bad folder is visible rather than absent
        static void Report(Library library, IReadOnlyList<ContentProblem> problems)
        {
            GD.Print($"roster  {library.Archetypes}, {library.Items}");
            GD.Print($"minis   {library.Shelf.Minis}");

            foreach (Loaded campaign in library.Campaigns) GD.Print($"        {campaign}");

            foreach (string collision in library.Archetypes.Collisions)
                GD.PushError($"roster: '{collision}' is claimed by two rosters - " +
                             "one of them is not namespaced by its campaign");

            foreach (ContentProblem problem in problems)
                GD.PushError("content: " + problem);
        }
    }
}
