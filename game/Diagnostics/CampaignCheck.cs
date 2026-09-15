using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Campaigns;
using Content.Encounters;
using Content.Minis;
using Content.Schema;
using Core.Characters;
using Core.Dice;

namespace Game.Diagnostics
{
    // everything printed is developer and author diagnostic, exempt from localization
    public partial class CampaignCheck : HeadlessCheck
    {
        protected override string Subject => "campaign";

        // no campaigns is not a failure (fresh install); turn this on where one is expected
        [Export] public bool ExpectSome { get; set; }

        public override void _Ready()
        {
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg == "--expect-some") ExpectSome = true;

            var roots = Game.Campaigns.CampaignFolders.Roots();
            var read = new List<Package>();
            int found = 0;

            foreach (string root in roots)
            {
                GD.Print($"campaign check: {root}");
                GD.Print("");

                foreach (string folder in Game.Campaigns.CampaignFolders.In(root))
                {
                    found++;
                    read.Add(Package.Read(folder));
                }

                if (found == 0) GD.Print("  none installed");
            }

            // a Shelf side by side, because cross-pack checks are shelf questions, not folder ones
            Shelf shelf = Shelf.Of(read);

            foreach (Shelf.Entry entry in shelf.Entries) Check(entry, shelf);

            foreach (ContentProblem problem in shelf.Problems) Problem("  " + problem);

            GD.Print("");
            GD.Print($"minis   {shelf.Minis}");

            foreach (MiniManifest mini in shelf.Minis.All) GD.Print($"        {mini}");

            GD.Print("");

            if (roots.Count == 0) Problem("there is nowhere for a campaign to live on this machine");

            if (found == 0 && ExpectSome)
                Problem("no campaigns were found, and this build expects at least one");

            GD.Print($"shelf   {found} campaign(s) in {roots.Count} root(s)");

            Finish();
        }

        void Check(Shelf.Entry entry, Shelf shelf)
        {
            Package package = entry.Package;

            // folder name first, even for a campaign too broken to know its id: it is all an author can act on
            GD.Print($"  {package.Id}");

            foreach (ContentProblem problem in package.Problems) Problem("  " + problem);

            // missing deps is waiting, not broken: a subscription not yet made, not a bug
            if (entry.Missing.Count > 0)
            {
                GD.Print($"        did not load - it needs {string.Join(", ", entry.Missing)}, " +
                         "and nothing of it is in play until they are installed");
                GD.Print("");
                return;
            }

            if (package.Failed)
            {
                // a failed campaign stays on the shelf but contributes nothing, so one bad item does not sink the rest
                GD.Print("        did not load - nothing in it is in play, and the rest of the " +
                         "shelf is unaffected");
                GD.Print("");
                return;
            }

            Manifest manifest = package.Manifest;

            // register its strings, or the title below is just its key; the game does the same, so a missing CSV shows in both
            int strings = Game.Campaigns.CampaignLocale.Register(package.Folder);

            GD.Print($"        format {manifest.Format}, needs engine {manifest.Engine} " +
                     $"(this is {Core.EngineVersion.Current})" +
                     (manifest.Author.Length > 0 ? $", by {manifest.Author}" : ""));

            if (manifest.Tags.Count > 0)
                GD.Print($"        tags: {string.Join(", ", manifest.Tags)}");

            // the title is a key, not a field; printing both shows an author the CSV being read is theirs
            GD.Print($"        titled '{Text(manifest.NameKey)}' ({manifest.NameKey}), " +
                     $"{strings} strings");

            GD.Print($"        {package.Monsters.Ids.Count} monsters, {package.Items.Ids.Count} " +
                     $"items, {package.Maps.Count} maps, {package.Encounters.Ids.Count} encounters" +
                     (package.Minis.Ids.Count > 0 ? $", {package.Minis.Ids.Count} minis" : "") +
                     (package.Classes.Ids.Count > 0 ? $", {package.Classes.Ids.Count} classes" : "") +
                     (package.Kit.Ids.Count > 0 ? $", {package.Kit.Ids.Count} abilities" : ""));

            foreach (Content.Classes.ClassCard card in package.Classes.All)
            {
                Actor hero = package.Classes.Create(card.Id);

                GD.Print($"        class {card.Id} - titled '{Text(card.NameKey)}', " +
                         $"vigor {hero.MaxVigor}, defense {hero.Defense}, {hero.NerveCap} nerve");

                GD.Print($"          {Rated(hero)}");

                GD.Print($"          holding {Held(card, hero)}, standing as " +
                         (card.MiniId.Length > 0 ? card.MiniId : "whatever its tier gives it"));

                foreach (string local in card.Kit)
                {
                    Content.Kits.Ability ability = package.Kit.Find(local);

                    GD.Print($"          kit {local} -> " +
                             (ability == null ? "NOTHING - no such ability" : ability.ToString()) +
                             (ability == null ? "" : $", titled '{Text(ability.NameKey)}'"));
                }

                foreach (Content.Classes.Growth step in card.Growth)
                    GD.Print($"          growth {step} - carried; who hands it out is Phase R");
            }

            if (manifest.Dependencies.Count > 0)
                GD.Print($"        needs: {string.Join(", ", manifest.Dependencies)}");

            foreach (MiniManifest mini in package.Minis.All)
            {
                Mounted mounted = shelf.Mount(package.Id, ContentId.LocalOf(mini.Id));

                GD.Print($"        mini {mini.Id} -> " +
                         (mounted == null ? "NOTHING - it will be a placeholder box"
                                          : mounted.ToString()) +
                         $", titled '{Text(mini.NameKey)}'");
            }

            foreach (Chapter chapter in manifest.Chapters)
                GD.Print($"        chapter {chapter.Id}" +
                         (chapter.Id == manifest.Start ? " (starts here)" : "") +
                         $": {string.Join(", ", chapter.Encounters)}");

            foreach (EncounterPlan plan in package.Encounters.All) Describe(package, plan);

            GD.Print("");
        }

        void Describe(Package package, EncounterPlan plan)
        {
            GD.Print($"        {plan.Id} on {plan.Map}:");

            foreach (Placement placement in plan.Placements.OrderBy(p => p.Slot))
            {
                string where = package.Maps.TryGetValue(plan.Map, out var map)
                               && map.SpawnAt(placement.Slot) is { } cell
                    ? cell.ToString()
                    : "nowhere";

                GD.Print($"          spawn {placement.Slot} {where} - {placement.Monster}");
            }

            // triggers printed though not yet run: a silent slot is indistinguishable from a broken one
            foreach (Trigger trigger in plan.Triggers)
                GD.Print($"          trigger {trigger} - carried; the runner is Phase R");

            foreach (Cue cue in plan.Cues)
                GD.Print($"          cue {cue}" +
                         (cue.LineKey(package.Id) is { } line
                             ? $" - \"{Text(line)}\""
                             : " - no words, just the gesture"));
        }

        // derived from the enums in sheet order, so a sixth skill needs no edit here
        static string Rated(Actor hero)
        {
            var rated = new List<string>();

            foreach (Attr a in System.Enum.GetValues<Attr>())
                if (hero.Attribute(a).IsReal())
                    rated.Add($"{Text(a.Key())} {hero.Attribute(a).Label()}");

            foreach (Skill s in System.Enum.GetValues<Skill>())
                if (hero.SkillDie(s).IsReal())
                    rated.Add($"{Text(s.Key())} {hero.SkillDie(s).Label()}");

            return rated.Count == 0 ? "no dice at all" : string.Join(", ", rated);
        }

        static string Held(Content.Classes.ClassCard card, Actor hero) =>
            card.Wields == null
                ? "nothing"
                : $"{Text(hero.WeaponKey)} {hero.Weapon.Label()} ({card.Wields})";

        // the same localizer the game uses, so a missing string looks here as it would on the table
        static string Text(string key)
        {
            string english = Godot.TranslationServer.Translate(key);

            return english == key ? "NO ENGLISH FOR THIS KEY" : english;
        }
    }
}
