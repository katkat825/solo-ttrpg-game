using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Campaigns;
using Content.Encounters;
using Content.Minis;
using Content.Schema;

namespace Game.Diagnostics
{
    // THE CONTENT VALIDATOR, RUN OVER EVERY CAMPAIGN ON DISK (CONTENT_PIPELINE.md P4).
    //
    // "A content validator the loader runs over a campaign, reporting every schema problem at once
    // with the file and line - the thing that makes hand-authoring a campaign, yours or a
    // player's, tolerable rather than a load-crash-fix loop."
    //
    // IT IS NOT A SECOND PROGRAM. `Content.Campaigns.Package.Read` is the validator, and it is
    // also exactly what the game runs when it loads a shelf - so this check cannot pass something
    // the game then refuses, and cannot refuse something the game then plays. All this adds is a
    // console, a verdict and an exit code.
    //
    // WHAT IT REPORTS WHEN NOTHING IS WRONG MATTERS AS MUCH AS WHAT IT REPORTS WHEN SOMETHING IS.
    // An author who has just written a campaign wants to see it read back: the id it registered
    // under, the monsters, the rooms, the encounters, who stands on which slot, and what nothing
    // has got round to running yet. A validator that only speaks up to complain is a validator
    // nobody runs until they are already stuck.
    //
    // Run it with check-campaign.ps1. Everything printed is developer and author diagnostic,
    // exempt from localization like `Actor.DebugName`.
    public partial class CampaignCheck : HeadlessCheck
    {
        protected override string Subject => "campaign";

        // A GAME WITH NO CAMPAIGNS INSTALLED IS NOT A FAILURE - that is a fresh install, and
        // ARCHITECTURE.md section 8 is explicit that it has to boot and play. Turn this on in a
        // build where a campaign is expected to be there
        [Export] public bool ExpectSome { get; set; }

        public override void _Ready()
        {
            // the same shape every other check uses: only what was actually asked for on the
            // command line, so the export above stays the single place the default lives
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

            // EVERY FOLDER SIDE BY SIDE, WHICH IS THE ONLY WAY TWO OF THE CHECKS CAN BE MADE
            // (MINIS_AND_ART.md A4). "Is the pack this one depends on installed" and "does this
            // mini id resolve across every root" are questions about the shelf rather than about
            // a folder, so the validator assembles one - the same `Shelf` the game assembles when
            // it loads, for the same reason the read is the same read
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

            // THE FOLDER NAME FIRST AND ALWAYS, even for a campaign that failed so early it never
            // learned its own id - "which folder" is the only thing an author can act on
            GD.Print($"  {package.Id}");

            foreach (ContentProblem problem in package.Problems) Problem("  " + problem);

            // WAITING IS NOT BROKEN, and the shelf has to say which it is. One of these is a bug
            // report and the other is a subscription somebody has not made yet (A4)
            if (entry.Missing.Count > 0)
            {
                GD.Print($"        did not load - it needs {string.Join(", ", entry.Missing)}, " +
                         "and nothing of it is in play until they are installed");
                GD.Print("");
                return;
            }

            if (package.Failed)
            {
                // THE ISOLATION BOUNDARY, SAID OUT LOUD. A failed campaign is still on the shelf
                // and contributes nothing, which is the behaviour that keeps one bad subscribed
                // item from taking the other seven with it
                GD.Print("        did not load - nothing in it is in play, and the rest of the " +
                         "shelf is unaffected");
                GD.Print("");
                return;
            }

            Manifest manifest = package.Manifest;

            // ITS STRINGS, REGISTERED, because the title below is a key and a key with no
            // translation server behind it is just the key again. `Library` does this when the
            // game loads a shelf; the validator does it here for the same reason and with the
            // same call, so a campaign whose CSV is missing reads as missing in both
            int strings = Game.Campaigns.CampaignLocale.Register(package.Folder);

            GD.Print($"        format {manifest.Format}, needs engine {manifest.Engine} " +
                     $"(this is {Core.EngineVersion.Current})" +
                     (manifest.Author.Length > 0 ? $", by {manifest.Author}" : ""));

            if (manifest.Tags.Count > 0)
                GD.Print($"        tags: {string.Join(", ", manifest.Tags)}");

            // THE TITLE IS A KEY AND NOT A FIELD (Manifest), so what a shelf would actually show
            // is what the locale says - and printing both is how an author sees that the CSV they
            // wrote is the one being read
            GD.Print($"        titled '{Text(manifest.NameKey)}' ({manifest.NameKey}), " +
                     $"{strings} strings");

            GD.Print($"        {package.Monsters.Ids.Count} monsters, {package.Items.Ids.Count} " +
                     $"items, {package.Maps.Count} maps, {package.Encounters.Ids.Count} encounters" +
                     (package.Minis.Ids.Count > 0 ? $", {package.Minis.Ids.Count} minis" : ""));

            if (manifest.Dependencies.Count > 0)
                GD.Print($"        needs: {string.Join(", ", manifest.Dependencies)}");

            // WHAT EACH MINI ACTUALLY RESOLVED TO, which is the one thing an author of a variant
            // cannot see from the file they wrote: "oldbones as rabble, 82 mm, tinted #D8CFB8" is
            // the whole of A0 read back to them
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

            // READ, CARRIED, AND NOT YET RUN. Printed rather than hidden, because a slot that
            // silently does nothing is indistinguishable from a slot that is broken
            foreach (Trigger trigger in plan.Triggers)
                GD.Print($"          trigger {trigger} - carried; the runner is Phase R");

            foreach (Cue cue in plan.Cues)
                GD.Print($"          cue {cue} - carried; the DM learns it in Phase D");
        }

        // through the same localizer the game draws its marks with, so a missing string looks here
        // exactly the way it would look on the table
        static string Text(string key)
        {
            string english = Godot.TranslationServer.Translate(key);

            return english == key ? "NO ENGLISH FOR THIS KEY" : english;
        }
    }
}
