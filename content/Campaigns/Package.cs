using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Audio;
using Content.Encounters;
using Content.Items;
using Content.Minis;
using Content.Models;
using Content.Monsters;
using Content.Schema;
using Core.Space;

namespace Content.Campaigns
{
    // A CAMPAIGN FOLDER, READ WHOLE (CONTENT_PIPELINE.md P4).
    //
    //     ashfall/
    //       pack.json         who this is, what KIND, what version, which chapters   (A4)
    //         or campaign.json - the same file under the name every folder written
    //         before Phase A uses, and it keeps working
    //       maps/             the rooms                        (P2)
    //       encounters/       who is in them, and what happens (P4)
    //       monsters/         statblocks                       (P0)
    //       items/            gear                             (P1)
    //       minis/            what its monsters stand as       (Phase A)
    //       models/           the figures those minis point at (Phase A)
    //       audio/            and what they sound like         (Phase A)
    //       locale/           this pack's strings              (P0/P5)
    //
    // THE ISOLATION BOUNDARY IS THIS CLASS, and it is a boundary because reading a folder is a
    // VALUE and never an exception. `Read` cannot throw: a folder with a corrupt manifest, a
    // monster that will not parse, a map with a wall in the wrong place or an encounter naming a
    // ghoul nobody ships comes back as a `Package` whose `Problems` say so, and `Failed` says
    // whether anything in it is playable. One bad campaign on a shelf of eight is one bad entry on
    // the shelf - `ARCHITECTURE.md` section 9: "a broken third-party campaign is the normal case,
    // not a bug".
    //
    // AND IT IS THE VALIDATOR. P4 asks for "a content validator the loader runs over a campaign,
    // reporting every schema problem at once with the file and line". That is not a second program:
    // it is this read, with nothing thrown away. The loader runs it and plays what loaded; the
    // check script runs it and prints the list. One code path, so the validator cannot pass
    // something the loader then refuses.
    //
    // IN `content/` AND NOT IN `game/`, which is what makes it testable headlessly - a campaign
    // folder is ordinary files on an ordinary disk (`System.IO`, not `res://`), and the Godot side
    // adds exactly one thing on top: registering the locale CSVs, which needs a translation server.
    public sealed class Package
    {
        public const string MonstersFolder = "monsters";
        public const string ItemsFolder = "items";
        public const string MapsFolder = "maps";
        public const string EncountersFolder = "encounters";
        public const string LocaleFolder = "locale";
        public const string AssetsFolder = "assets";

        // THE ART FOLDERS (MINIS_AND_ART.md, MODDING.md section 2). `minis/` is a folder of JSON
        // manifests exactly as `monsters/` is; `models/` and `audio/` are what those manifests
        // point AT, and neither is enumerated - a file nobody names is a file nobody loads, which
        // is what keeps a pack's spare exports from being parsed on the off chance
        public const string MinisFolder = "minis";
        public const string ModelsFolder = "models";
        public const string AudioFolder = "audio";

        public const string MapExtension = ".map";

        Package(string folder, string id, Manifest manifest, JsonArchetypeSource monsters,
                ItemCatalogue items, EncounterBook encounters,
                IReadOnlyDictionary<string, MapLayout> maps, MiniCatalogue minis,
                List<ContentProblem> problems)
        {
            Folder = folder;
            Id = id;
            Manifest = manifest;
            Monsters = monsters;
            Items = items;
            Encounters = encounters;
            Maps = maps;
            Minis = minis ?? MiniCatalogue.Of(null);
            Problems = problems;
        }

        public string Folder { get; }

        // WHICH OF THE TWO NAMES THIS FOLDER USED, so a problem points at the file the author
        // actually has rather than at the one they might have had
        public string ManifestFile { get; private set; } = ManifestReader.FileName;

        // THE FOLDER'S NAME, always - even when the manifest is the thing that failed. A shelf
        // entry that cannot say which folder it is would be no better than the folder being absent
        public string Id { get; }

        // null when `campaign.json` could not be read, which is the one failure that stops the rest
        public Manifest Manifest { get; }

        public JsonArchetypeSource Monsters { get; }

        public ItemCatalogue Items { get; }

        public EncounterBook Encounters { get; }

        // by file name without the extension, which is how an encounter names one
        public IReadOnlyDictionary<string, MapLayout> Maps { get; }

        // WHAT ITS MONSTERS STAND AS (Phase A). Empty for every campaign written before this
        // phase and for most written after it - a campaign with no minis of its own stands its
        // monsters on the shared roster's figures, which is what `greyhollow` does
        public MiniCatalogue Minis { get; }

        // EVERY PROBLEM IN THE FOLDER, AT ONCE. Not the first one - authoring against a loader
        // that stops at the first mistake is a load-fix-load loop, and against one that reports
        // all of them it is a list to work through (`ContentProblem`)
        public IReadOnlyList<ContentProblem> Problems { get; }

        // A FAILED PACKAGE CONTRIBUTES NOTHING - no monsters in the roster, no items in the
        // catalogue, no strings in the table. Anything less than all-or-nothing is a campaign
        // half-installed, which is the state that produces a bug report nobody can reproduce
        public bool Failed => Manifest == null;

        public bool Clean => Problems.Count == 0;

        // ---- reading one ----

        public static Package Read(string folder)
        {
            var problems = new List<ContentProblem>();
            string name = Path.GetFileName(folder?.TrimEnd(Path.DirectorySeparatorChar,
                                                           Path.AltDirectorySeparatorChar) ?? "");

            Manifest manifest = ReadManifest(folder, name, problems);

            if (manifest == null)
                return new Package(folder, name, null, null, null, null,
                                   new Dictionary<string, MapLayout>(), null, problems);

            string id = manifest.Id;

            JsonArchetypeSource monsters = JsonArchetypeSource.Read(In(folder, MonstersFolder), id);
            ItemCatalogue items = ItemCatalogue.Read(In(folder, ItemsFolder));
            EncounterBook encounters = EncounterBook.Read(In(folder, EncountersFolder));
            IReadOnlyDictionary<string, MapLayout> maps = ReadMaps(folder, problems);
            MiniCatalogue minis = MiniCatalogue.Read(In(folder, MinisFolder), id);

            problems.AddRange(monsters.Problems);
            problems.AddRange(items.Problems);
            problems.AddRange(encounters.Problems);

            foreach (ContentProblem problem in minis.Problems)
                problems.Add(new ContentProblem(MinisFolder + "/" + problem.File, problem.Where,
                                                problem.What, problem.Line));

            var package = new Package(folder, id, manifest, monsters, items, encounters, maps,
                                      minis, problems)
            {
                ManifestFile = ManifestFileIn(folder),
            };

            package.CrossCheck(problems);

            return package;
        }

        static string In(string folder, string inside) => Path.Combine(folder, inside);

        static string ManifestFileIn(string folder)
        {
            foreach (string candidate in ManifestReader.FileNames)
                if (File.Exists(Path.Combine(folder ?? "", candidate))) return candidate;

            return ManifestReader.FileName;
        }

        static Manifest ReadManifest(string folder, string name, List<ContentProblem> problems)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                problems.Add(new ContentProblem(name, "", "there is no such folder"));
                return null;
            }

            // EITHER NAME, PREFERRING THE NEW ONE (A4). A folder with BOTH is a fork somebody
            // half-finished, and picking one quietly would mean the game and the author disagree
            // about which file is being read - which is the worst kind of authoring bug, because
            // edits appear to do nothing
            string file = null;

            foreach (string candidate in ManifestReader.FileNames)
            {
                if (!File.Exists(Path.Combine(folder, candidate))) continue;

                if (file != null)
                {
                    problems.Add(new ContentProblem(
                        candidate, "",
                        $"'{name}' has both a {file} and a {candidate} - they are the same file " +
                        "under two names, so keep one. pack.json is the one that can say what " +
                        "kind of pack this is"));
                    return null;
                }

                file = candidate;
            }

            if (file == null)
            {
                // WORTH ITS OWN SENTENCE, because it is what every folder looked like before P4
                // and what a half-copied one looks like now
                problems.Add(new ContentProblem(
                    ManifestReader.FileName, "",
                    $"'{name}' has no {ManifestReader.PackFileName} or {ManifestReader.FileName}, " +
                    "so it is a folder and not a pack - a pack declares its id, its content " +
                    "format and the oldest engine that can play it"));
                return null;
            }

            string path = Path.Combine(folder, file);

            string text;

            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception could)
            {
                problems.Add(new ContentProblem(file, "", "could not be read - " + could.Message));
                return null;
            }

            Read<Manifest> read = ManifestReader.Parse(text, file, name);

            if (read.Ok) return read.Value;

            problems.AddRange(read.Problems);

            return null;
        }

        static IReadOnlyDictionary<string, MapLayout> ReadMaps(string folder,
                                                              List<ContentProblem> problems)
        {
            var maps = new Dictionary<string, MapLayout>(StringComparer.Ordinal);
            string inside = In(folder, MapsFolder);

            if (!Directory.Exists(inside)) return maps;

            foreach (string path in Directory.EnumerateFiles(inside, "*" + MapExtension,
                                                             SearchOption.TopDirectoryOnly)
                                             .OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                string file = MapsFolder + "/" + Path.GetFileName(path);
                string text;

                try
                {
                    text = File.ReadAllText(path);
                }
                catch (Exception could)
                {
                    problems.Add(new ContentProblem(file, "", "could not be read - " + could.Message));
                    continue;
                }

                // THE SAME READER THE BOARD USES, which is what makes this a validator rather than
                // a second opinion - it passes for exactly the maps the board can load
                if (!MapReader.TryRead(text, out MapLayout map, out string problem))
                {
                    problems.Add(new ContentProblem(file, "", problem));
                    continue;
                }

                maps[Path.GetFileNameWithoutExtension(path)] = map;
            }

            return maps;
        }

        // ---- and the questions no single file can answer ----

        // EVERY REFERENCE ONE FILE MAKES TO ANOTHER, FOLLOWED. This is the half of validation that
        // `EncounterReader` explicitly cannot do: a reader sees one file, and "does this campaign
        // ship a ghoul" is a question about the folder. Done here, once, with everything open.
        //
        // The failures below are the ones that actually happen while authoring - a renamed map, a
        // monster that moved to another campaign, a chapter pointing at an encounter that was
        // deleted. Every one of them would otherwise be a fight that musters nobody, and every one
        // of them is one sentence here
        void CrossCheck(List<ContentProblem> problems)
        {
            string book = EncountersFolder + "/";

            foreach (EncounterPlan plan in Encounters.All)
            {
                string file = book + plan.Id + EncounterBook.Extension;

                if (!Maps.TryGetValue(plan.Map, out MapLayout map))
                {
                    problems.Add(new ContentProblem(
                        file, "map",
                        $"'{plan.Map}' is not one of this campaign's maps - it has " +
                        $"{Offered(Maps.Keys)}"));
                    map = null;
                }

                int at = 0;

                foreach (Placement placement in plan.Placements)
                {
                    string where = $"placements[{at++}]";

                    if (!Monsters.Has(ContentId.Scoped(Id, placement.Monster)))
                        problems.Add(new ContentProblem(
                            file, where + ".monster",
                            $"'{placement.Monster}' is not one of this campaign's monsters - it " +
                            $"has {Offered(Local(Monsters.Ids))}"));

                    if (map != null && map.SpawnAt(placement.Slot) == null)
                        problems.Add(new ContentProblem(
                            file, where + ".slot",
                            $"{plan.Map} has no spawn {placement.Slot} drawn on it - it has " +
                            $"{Offered(map.Spawns.Keys.OrderBy(s => s).Select(s => s.ToString()))}"));
                }

                at = 0;

                foreach (Trigger trigger in plan.Triggers)
                {
                    string where = $"triggers[{at++}].encounter";

                    if (trigger.ThenDo == Then.Goto && !Encounters.Has(trigger.Encounter))
                        problems.Add(new ContentProblem(
                            file, where,
                            $"'{trigger.Encounter}' is not one of this campaign's encounters - it " +
                            $"has {Offered(Encounters.Ids)}"));
                }
            }

            // AND THE CHAPTERS, which is the reference that decides whether anything is reachable
            // at all: an encounter no chapter names is a file that will never be played
            var named = new HashSet<string>(StringComparer.Ordinal);
            int chapter = 0;

            foreach (Chapter one in Manifest.Chapters)
            {
                int index = 0;

                foreach (string encounter in one.Encounters)
                {
                    named.Add(encounter);

                    if (!Encounters.Has(encounter))
                        problems.Add(new ContentProblem(
                            ManifestFile,
                            $"chapters[{chapter}].encounters[{index}]",
                            $"'{encounter}' is not one of this campaign's encounters - it has " +
                            $"{Offered(Encounters.Ids)}"));

                    index++;
                }

                chapter++;
            }

            foreach (string orphan in Encounters.Ids.OrderBy(i => i, StringComparer.Ordinal))
                if (!named.Contains(orphan))
                    problems.Add(new ContentProblem(
                        book + orphan + EncounterBook.Extension, "id",
                        "no chapter names this encounter, so nothing can ever play it - put it in " +
                        "a chapter, or delete the file"));

            // WHAT A MONSTER DROPS HAS TO BE SOMETHING THE CAMPAIGN SHIPS. Gear ids are a shared
            // namespace with the engine's (ItemReader), so a drop naming `axe` would quietly
            // resolve to the engine's - which works on this machine and stops working on one where
            // the campaign that shipped it is not installed. Self-contained means self-contained
            foreach (string id in Monsters.Ids.OrderBy(i => i, StringComparer.Ordinal))
            {
                Statblock block = Monsters.Of(id);

                if (block?.Loot == null) continue;

                string file = MonstersFolder + "/" + ContentId.LocalOf(id) + ".json";

                // a null item is "nothing this time", which is most of a loot table (LootTable)
                foreach (string item in block.Loot.Entries.Select(e => e.Item).Where(i => i != null))
                    if (!Items.Has(item))
                        problems.Add(new ContentProblem(
                            file, "loot",
                            $"'{item}' is not one of this campaign's items - a campaign has to " +
                            $"ship what it drops. It has {Offered(Items.Ids)}"));
            }

            CrossCheckArt(problems);

            // AND THE PICTURE ON THE BOX, if it named one. Workshop requires a preview image, and
            // a manifest naming a file that is not there is a shelf entry with a hole in it
            if (Manifest.Preview.Length > 0 &&
                !File.Exists(Path.Combine(Folder, Manifest.Preview)))
                problems.Add(new ContentProblem(
                    ManifestFile, "preview",
                    $"there is no '{Manifest.Preview}' in this campaign's folder"));
        }

        // EVERY FILE THE ART SIDE NAMES, OPENED AND MEASURED (MINIS_AND_ART.md A2, A3).
        //
        // This is the model half of "the validator extends to models: run it over a pack and every
        // over-cap texture, missing clip and unresolved id is reported at once, before load, with
        // the file named". It runs at LOAD as well as from the check script, because they are the
        // same code path - which is the property that keeps a validator from passing something the
        // game then refuses.
        //
        // NOTHING HERE IS FATAL TO THE PACK. A mini whose model is over a cap is one placeholder
        // box with a sentence beside it; the pack's other minis, its monsters, its rooms and its
        // encounters all load. That is the isolation boundary drawn one level finer than a folder
        // (`MINIS_AND_ART.md`: "the placeholder box is a feature, not a stopgap")
        void CrossCheckArt(List<ContentProblem> problems)
        {
            foreach (MiniManifest mini in Minis.All)
            {
                string file = MinisFolder + "/" + ContentId.LocalOf(mini.Id) + MiniReader.Extension;

                // A VARIANT OF A MINI IN THIS PACK is the one link a single folder can follow;
                // a bare name is one of the shared roster's, and a dotted one belongs to another
                // pack and is the shelf's question (`Shelf`)
                if (mini.IsVariant) CheckVariant(mini, file, problems);

                if (mini.HasModel) CheckModel(mini, file, problems);

                foreach (KeyValuePair<Motion, string> set in mini.Foley)
                    CheckFoley(set, file, problems);
            }

            // AND WHAT THE MONSTERS ASKED TO STAND AS. A statblock naming a mini nobody ships is
            // the commonest way this phase breaks, and it breaks into a box rather than a crash -
            // so it has to be SAID, here, where the author can still fix it
            foreach (string id in Monsters.Ids.OrderBy(i => i, StringComparer.Ordinal))
            {
                Statblock block = Monsters.Of(id);

                if (block == null || string.IsNullOrEmpty(block.MiniId)) continue;

                if (Names(block.MiniId)) continue;

                problems.Add(new ContentProblem(
                    MonstersFolder + "/" + ContentId.LocalOf(id) + ".json", "mini",
                    $"'{block.MiniId}' is not a mini this pack ships and not one the game ships " +
                    $"({Offered(SharedMinis.Catalogue.Ids)}) - if it belongs to another pack, " +
                    "write it as '<pack>.<mini>' and name that pack in dependencies"));
            }
        }

        // does THIS pack, or the base game, have a mini by that name? A dotted id naming another
        // pack is deliberately allowed through - the shelf resolves it and reports it if missing,
        // because a folder on its own cannot know what else is installed
        bool Names(string mini) =>
            ContentId.IsScoped(mini)
                ? Minis.Has(mini) || ContentId.CampaignOf(mini) != Id
                : Minis.Has(ContentId.Scoped(Id, mini)) || SharedMinis.Has(mini);

        void CheckVariant(MiniManifest mini, string file, List<ContentProblem> problems)
        {
            if (Names(mini.Variant)) return;

            problems.Add(new ContentProblem(
                file, "variant",
                $"'{mini.Variant}' is not a mini this pack ships and not one the game ships " +
                $"({Offered(SharedMinis.Catalogue.Ids)})"));
        }

        void CheckModel(MiniManifest mini, string file, List<ContentProblem> problems)
        {
            Read<ModelFacts> read = ModelReader.Inspect(Folder, mini.Model);

            if (!read.Ok)
            {
                foreach (ContentProblem problem in read.Problems)
                    problems.Add(new ContentProblem(problem.File, problem.Where, problem.What,
                                                   problem.Line));
                return;
            }

            // THE CLIP THAT IS NOT THERE, NAMED ALONGSIDE THE ONES THAT ARE. This is A1's "rename
            // a clip wrong and confirm it degrades to the procedural motion with a named warning,
            // not an exception" - and the list of what the file DOES have is what turns the
            // warning from a complaint into an answer
            foreach (KeyValuePair<Motion, string> clip in mini.Clips)
            {
                if (read.Value.Has(clip.Value)) continue;

                problems.Add(new ContentProblem(
                    file, $"clips.{clip.Key.ToString().ToLowerInvariant()}",
                    $"'{mini.Model}' has no clip called '{clip.Value}' - it has " +
                    $"{Offered(read.Value.Clips)}. The piece will still slide, get struck and be " +
                    "laid on its side; it will do it with the engine's own motion instead"));
            }
        }

        void CheckFoley(KeyValuePair<Motion, string> set, string file,
                        List<ContentProblem> problems)
        {
            FoleySet foley = FoleySet.Read(
                Path.Combine(Folder, set.Value.Replace('/', Path.DirectorySeparatorChar)),
                set.Value);

            foreach (ContentProblem problem in foley.Problems)
                problems.Add(new ContentProblem(
                    problem.File.Length > 0 ? problem.File : file,
                    problem.Where.Length > 0 ? problem.Where
                                             : $"foley.{set.Key.ToString().ToLowerInvariant()}",
                    problem.What, problem.Line));
        }

        IEnumerable<string> Local(IEnumerable<string> scoped) => scoped
            .Select(ContentId.LocalOf)
            .OrderBy(i => i, StringComparer.Ordinal);

        static string Offered(IEnumerable<string> words)
        {
            string[] list = words.ToArray();

            return list.Length == 0 ? "none at all" : Vocabulary.Offer(list);
        }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString()
        {
            if (Failed) return $"{Id}: FAILED, {Problems.Count} problems";

            return $"{Id}: {Monsters.Ids.Count} monsters, {Items.Ids.Count} items, " +
                   $"{Maps.Count} maps, {Encounters.Ids.Count} encounters" +
                   (Minis.Ids.Count > 0 ? $", {Minis.Ids.Count} minis" : "") +
                   (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
        }
    }
}
