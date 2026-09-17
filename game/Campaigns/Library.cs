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
    public sealed class Library : IArchetypeSource
    {
        Library(Rosters archetypes, ItemCatalogue items,
                IReadOnlyDictionary<string, Statblock> statblocks,
                IReadOnlyDictionary<string, Content.Kits.Ability> abilities,
                IReadOnlyList<Loaded> campaigns, Shelf shelf)
        {
            Archetypes = archetypes;
            Items = items;
            Campaigns = campaigns;
            Shelf = shelf;
            _statblocks = statblocks;
            _abilities = abilities;
        }

        readonly IReadOnlyDictionary<string, Statblock> _statblocks;

        readonly IReadOnlyDictionary<string, Content.Kits.Ability> _abilities;

        public IReadOnlyList<Loaded> Campaigns { get; }

        public Rosters Archetypes { get; }

        public ItemCatalogue Items { get; }

        public Shelf Shelf { get; }

        public Mounted Mount(string pack, string mini) => Shelf?.Mount(pack, mini);

        public Loaded Campaign(string id) =>
            id == null ? null : Campaigns.FirstOrDefault(c => c.Id == id);

        public IReadOnlyCollection<string> Ids => Archetypes.Ids;

        public bool Has(string id) => Archetypes.Has(id);

        public Actor Create(string id) => Archetypes.Create(id);

        public LootTable LootFor(string id) =>
            id != null && _statblocks.TryGetValue(id, out Statblock block) ? block.Loot : LootTable.Nothing;

        public string MiniFor(string id) =>
            id != null && _statblocks.TryGetValue(id, out Statblock block) ? block.MiniId : "";

        public Content.Classes.ClassCard ClassOf(string id)
        {
            if (id == null) return null;

            foreach (Loaded campaign in Campaigns)
            {
                if (campaign.Failed || campaign.Waiting) continue;

                Content.Classes.ClassCard card = campaign.Classes.Of(id);

                if (card != null) return card;
            }

            return null;
        }

        // Phase W. The voices on the shelf, whichever pack shipped them.
        //
        // A companion is looked up across every campaign in play rather than out of one, because a
        // class pack can ship the wolf and a campaign can ship the wolf's barks, and neither knows
        // about the other. That is the same argument the mini shelf makes, and it is what lets a
        // campaign write for somebody else's companion.
        public Content.Companions.CompanionCard CompanionOf(string id)
        {
            if (id == null) return null;

            foreach (Loaded campaign in InPlay)
            {
                Content.Companions.CompanionCard card = campaign.Companions.Of(id);

                if (card != null) return card;
            }

            return null;
        }

        // barks are NOT pack-scoped: the wolf ships in the base game and belongs to no campaign
        // (CONVENTIONS.md section 7), so a campaign writing more of them adds to one bank
        public Content.Dialogue.BarkBank BarksFor(string speaker)
        {
            if (speaker == null) return null;

            foreach (Loaded campaign in InPlay)
            {
                Content.Dialogue.BarkBank bank = campaign.Barks.Of(speaker);

                if (bank != null) return bank;
            }

            return null;
        }

        // every creature anything on this shelf can speak as. The spine check asks for exactly this:
        // a beat has to be deliverable by any companion the player might turn up with.
        public IEnumerable<string> Voices =>
            InPlay.SelectMany(c => c.Companions.Voices.Concat(c.Barks.Speakers))
                  .Distinct()
                  .OrderBy(v => v, System.StringComparer.Ordinal);

        public IEnumerable<Loaded> InPlay =>
            Campaigns.Where(c => !c.Failed && !c.Waiting);

        public Content.Dialogue.DialogueBook DialogueOf(string campaign) =>
            Campaign(campaign)?.Dialogue;

        public Content.Kits.Ability Ability(string id) =>
            id != null && _abilities.TryGetValue(id, out Content.Kits.Ability ability)
                ? ability
                : Content.Kits.SharedKit.Of(id);

        public IEnumerable<Content.Kits.Ability> KitOf(Content.Classes.ClassCard card)
        {
            if (card == null) yield break;

            string pack = Content.Campaigns.ContentId.IsScoped(card.Id)
                ? Content.Campaigns.ContentId.CampaignOf(card.Id)
                : "";

            foreach (string local in card.Kit)
            {
                // try the pack-scoped id before the shared kit, matching KitBook.Find - or the validator passes a class the game then silently drops an ability from
                Content.Kits.Ability ability =
                    (Content.Campaigns.ContentId.IsCampaign(pack)
                        ? Ability(Content.Campaigns.ContentId.Scoped(pack, local))
                        : null)
                    ?? Content.Kits.SharedKit.Of(local);

                if (ability != null) yield return ability;
            }
        }

        public static Library Load(IReadOnlyList<string> roots = null, bool quiet = false)
        {
            var rosters = new Rosters(new BuiltInArchetypes());
            var items = new ItemCatalogue();
            var statblocks = new Dictionary<string, Statblock>(System.StringComparer.Ordinal);
            var abilities = new Dictionary<string, Content.Kits.Ability>(System.StringComparer.Ordinal);
            var problems = new List<ContentProblem>();
            var loaded = new List<Loaded>();
            var read = new List<Package>();

            foreach (string root in roots ?? CampaignFolders.Roots())
                foreach (string folder in CampaignFolders.In(root))
                    read.Add(Package.Read(folder));

            // every folder read before any is put in play, so a campaign's dependency loads regardless of walk order
            var shelf = Shelf.Of(read);

            problems.AddRange(shelf.Problems);

            foreach (Shelf.Entry entry in shelf.Entries)
            {
                Package package = entry.Package;

                // the severity travels with it; re-wrapping a caution as a fault is how "your
                // quest-giver is attackable" would have started looking like a broken campaign
                foreach (ContentProblem problem in package.Problems)
                    problems.Add(new ContentProblem(
                        package.Id + "/" + problem.File, problem.Where, problem.What, problem.Line,
                        problem.How));

                if (!entry.InPlay) { loaded.Add(new Loaded(package, 0, entry.Missing)); continue; }

                if (package.Monsters.Ids.Count > 0) rosters.Add(package.Monsters);

                if (package.Classes.Ids.Count > 0) rosters.Add(package.Classes);

                foreach (Content.Kits.Ability ability in package.Kit.All)
                    abilities[ability.Id] = ability;

                foreach (string id in package.Monsters.Ids) statblocks[id] = package.Monsters.Of(id);

                foreach (string taken in items.Absorb(package.Items))
                    problems.Add(new ContentProblem(
                        package.Id + "/" + Package.ItemsFolder, taken,
                        "another campaign already ships an item with this id, and its version " +
                        "is the one in play - gear ids are shared"));

                loaded.Add(new Loaded(package, CampaignLocale.Register(package.Folder)));
            }

            problems.AddRange(items.Problems);

            var library = new Library(rosters, items, statblocks, abilities, loaded, shelf);

            if (!quiet) Report(library, problems);

            return library;
        }

        static void Report(Library library, IReadOnlyList<ContentProblem> problems)
        {
            GD.Print($"roster  {library.Archetypes}, {library.Items}");
            GD.Print($"minis   {library.Shelf.Minis}");

            foreach (Loaded campaign in library.Campaigns) GD.Print($"        {campaign}");

            foreach (string collision in library.Archetypes.Collisions)
                GD.PushError($"roster: '{collision}' is claimed by two rosters - " +
                             "one of them is not namespaced by its campaign");

            // a caution is a sentence an author should read once, not a thing that went wrong -
            // pushing it as an error would make "your quest-giver is attackable" indistinguishable
            // from a campaign that failed to load (PLACES_AND_PERSISTENCE.md section 6)
            foreach (ContentProblem problem in problems)
                // a caution is PRINTED, not pushed: Godot prints a full C# backtrace behind a
                // pushed warning, and a stack trace under "your quest-giver is attackable" makes a
                // deliberate authoring choice look like a crash
                if (problem.IsACaution) GD.Print("content: " + problem);
                else GD.PushError("content: " + problem);
        }
    }
}
