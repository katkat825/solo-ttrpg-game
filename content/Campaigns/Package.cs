using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Audio;
using Content.Classes;
using Content.Encounters;
using Content.Items;
using Content.Kits;
using Content.Minis;
using Content.Models;
using Content.Monsters;
using Content.Schema;
using Core.Space;

namespace Content.Campaigns
{
    public sealed class Package
    {
        public const string MonstersFolder = "monsters";
        public const string ItemsFolder = "items";
        public const string MapsFolder = "maps";
        public const string EncountersFolder = "encounters";
        public const string LocaleFolder = "locale";
        public const string AssetsFolder = "assets";

        public const string ClassesFolder = "classes";

        public const string KitsFolder = "kits";

        public const string MinisFolder = "minis";
        public const string ModelsFolder = "models";
        public const string AudioFolder = "audio";

        public const string MapExtension = ".map";

        Package(string folder, string id, Manifest manifest, JsonArchetypeSource monsters,
                ItemCatalogue items, EncounterBook encounters,
                IReadOnlyDictionary<string, MapLayout> maps, MiniCatalogue minis,
                ClassRoster classes, KitBook kit, List<ContentProblem> problems)
        {
            Folder = folder;
            Id = id;
            Manifest = manifest;
            Monsters = monsters;
            Items = items;
            Encounters = encounters;
            Maps = maps;
            Minis = minis ?? MiniCatalogue.Of(null);
            Classes = classes ?? ClassRoster.Of(id);
            Kit = kit ?? KitBook.Of(id);
            Problems = problems;
        }

        public string Folder { get; }

        // which of the two names this folder used, so a problem points at the file the author has
        public string ManifestFile { get; private set; } = ManifestReader.FileName;

        // the folder name, always, even when the manifest is the thing that failed
        public string Id { get; }

        // null when the manifest could not be read; the one failure that stops the rest
        public Manifest Manifest { get; }

        public JsonArchetypeSource Monsters { get; }

        public ItemCatalogue Items { get; }

        public EncounterBook Encounters { get; }

        // keyed by file name without the extension, which is how an encounter names one
        public IReadOnlyDictionary<string, MapLayout> Maps { get; }

        public ClassRoster Classes { get; }

        public KitBook Kit { get; }

        public MiniCatalogue Minis { get; }

        public IReadOnlyList<ContentProblem> Problems { get; }

        // a failed package contributes nothing; all-or-nothing avoids a half-installed campaign
        public bool Failed => Manifest == null;

        public bool Clean => Problems.Count == 0;


        public static Package Read(string folder)
        {
            var problems = new List<ContentProblem>();
            string name = Path.GetFileName(folder?.TrimEnd(Path.DirectorySeparatorChar,
                                                           Path.AltDirectorySeparatorChar) ?? "");

            Manifest manifest = ReadManifest(folder, name, problems);

            if (manifest == null)
                return new Package(folder, name, null, null, null, null,
                                   new Dictionary<string, MapLayout>(), null, null, null,
                                   problems);

            string id = manifest.Id;

            JsonArchetypeSource monsters = JsonArchetypeSource.Read(In(folder, MonstersFolder), id);
            ItemCatalogue items = ItemCatalogue.Read(In(folder, ItemsFolder));
            EncounterBook encounters = EncounterBook.Read(In(folder, EncountersFolder));
            IReadOnlyDictionary<string, MapLayout> maps = ReadMaps(folder, problems);
            MiniCatalogue minis = MiniCatalogue.Read(In(folder, MinisFolder), id);
            ClassRoster classes = ClassRoster.Read(In(folder, ClassesFolder), id);
            KitBook kit = KitBook.Read(In(folder, KitsFolder), id);

            // pack items plus shared gear; the pack's own wins where both have the id
            var equipment = new ItemCatalogue();
            equipment.Absorb(items);
            equipment.Absorb(SharedGear.Catalogue);
            classes.Equips(equipment);

            problems.AddRange(monsters.Problems);
            problems.AddRange(items.Problems);
            problems.AddRange(encounters.Problems);

            foreach (ContentProblem problem in classes.Problems)
                problems.Add(new ContentProblem(ClassesFolder + "/" + problem.File, problem.Where,
                                                problem.What, problem.Line));

            foreach (ContentProblem problem in kit.Problems)
                problems.Add(new ContentProblem(KitsFolder + "/" + problem.File, problem.Where,
                                                problem.What, problem.Line));

            foreach (ContentProblem problem in minis.Problems)
                problems.Add(new ContentProblem(MinisFolder + "/" + problem.File, problem.Where,
                                                problem.What, problem.Line));

            var package = new Package(folder, id, manifest, monsters, items, encounters, maps,
                                      minis, classes, kit, problems)
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

            // a folder with both files is refused; picking one quietly would make edits appear to do nothing
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

                // the same reader the board uses, so this validates exactly what the board can load
                if (!MapReader.TryRead(text, out MapLayout map, out string problem))
                {
                    problems.Add(new ContentProblem(file, "", problem));
                    continue;
                }

                maps[Path.GetFileNameWithoutExtension(path)] = map;
            }

            return maps;
        }


        // every cross-file reference, followed here where the whole folder is open
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

                foreach (Cue cue in plan.Cues)
                {
                    string cueWhere = $"cues[{at++}].at";

                    if (cue.Slot > 0 && map != null && map.SpawnAt(cue.Slot) == null)
                        problems.Add(new ContentProblem(
                            file, cueWhere,
                            $"{plan.Map} has no spawn {cue.Slot} drawn on it for the DM to " +
                            $"{cue.Does.Word()} at - it has " +
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

            // an encounter no chapter names can never be played
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

            // a drop must be the campaign's own item; a shared-namespace axe breaks where the campaign isn't installed
            foreach (string id in Monsters.Ids.OrderBy(i => i, StringComparer.Ordinal))
            {
                Statblock block = Monsters.Of(id);

                if (block?.Loot == null) continue;

                string file = MonstersFolder + "/" + ContentId.LocalOf(id) + ".json";

                // a null item is "nothing this time", which is most of a loot table
                foreach (string item in block.Loot.Entries.Select(e => e.Item).Where(i => i != null))
                    if (!Items.Has(item))
                        problems.Add(new ContentProblem(
                            file, "loot",
                            $"'{item}' is not one of this campaign's items - a campaign has to " +
                            $"ship what it drops. It has {Offered(Items.Ids)}"));
            }

            CrossCheckClasses(problems);

            CrossCheckArt(problems);

            if (Manifest.Preview.Length > 0 &&
                !File.Exists(Path.Combine(Folder, Manifest.Preview)))
                problems.Add(new ContentProblem(
                    ManifestFile, "preview",
                    $"there is no '{Manifest.Preview}' in this campaign's folder"));
        }

        // shared gear counts here, unlike a monster's loot: the engine's axe is as portable as the class itself
        void CrossCheckClasses(List<ContentProblem> problems)
        {
            foreach (ClassCard card in Classes.All)
            {
                string file = ClassesFolder + "/" + ContentId.LocalOf(card.Id) + ClassRoster.Extension;

                CheckGear(card.Wields, "wields", file, problems);
                CheckGear(card.Wears, "wears", file, problems);

                if (card.MiniId.Length > 0 && !Names(card.MiniId))
                    problems.Add(new ContentProblem(
                        file, "mini",
                        $"'{card.MiniId}' is not a mini this pack ships and not one the game " +
                        $"ships ({Offered(SharedMinis.Catalogue.Ids)}) - if it belongs to " +
                        "another pack, write it as '<pack>.<mini>' and name that pack in " +
                        "dependencies"));

                int at = 0;

                foreach (string ability in card.Kit)
                {
                    if (Kit.Find(ability) != null) { at++; continue; }

                    problems.Add(new ContentProblem(
                        file, $"kit[{at++}]",
                        $"'{ability}' is not an ability this pack's {KitsFolder}/ folder ships " +
                        $"and not one the game ships ({Offered(SharedKit.All.Select(a => a.Id))})"));
                }

                foreach (Content.Classes.Growth step in card.Growth)
                {
                    if (step.Ability == null || Kit.Find(step.Ability) != null) continue;

                    problems.Add(new ContentProblem(
                        file, $"growth[{step.Id}].ability",
                        $"'{step.Ability}' is not an ability this pack's {KitsFolder}/ folder " +
                        $"ships and not one the game ships - a growth step that unlocks nothing " +
                        "is a reward with nothing in it"));
                }
            }
        }

        void CheckGear(string id, string where, string file, List<ContentProblem> problems)
        {
            if (id == null || Items.Has(id) || SharedGear.Has(id)) return;

            problems.Add(new ContentProblem(
                file, where,
                $"'{id}' is not an item this pack ships and not one the game ships " +
                $"({Offered(SharedGear.Catalogue.Ids)}) - a class has to start you with " +
                "something that exists"));
        }

        // runs at load too, same code path; nothing here is fatal, a bad model is a placeholder box
        void CrossCheckArt(List<ContentProblem> problems)
        {
            foreach (MiniManifest mini in Minis.All)
            {
                string file = MinisFolder + "/" + ContentId.LocalOf(mini.Id) + MiniReader.Extension;

                // a variant in this pack is the one link a folder can follow; a dotted id is the shelf's question
                if (mini.IsVariant) CheckVariant(mini, file, problems);

                if (mini.HasModel) CheckModel(mini, file, problems);

                foreach (KeyValuePair<Motion, string> set in mini.Foley)
                    CheckFoley(set, file, problems);
            }

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

        // a dotted id naming another pack is deliberately allowed through; the shelf resolves it
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

        public override string ToString()
        {
            if (Failed) return $"{Id}: FAILED, {Problems.Count} problems";

            return $"{Id}: {Monsters.Ids.Count} monsters, {Items.Ids.Count} items, " +
                   $"{Maps.Count} maps, {Encounters.Ids.Count} encounters" +
                   (Minis.Ids.Count > 0 ? $", {Minis.Ids.Count} minis" : "") +
                   (Classes.Ids.Count > 0 ? $", {Classes.Ids.Count} classes" : "") +
                   (Kit.Ids.Count > 0 ? $", {Kit.Ids.Count} abilities" : "") +
                   (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
        }
    }
}
