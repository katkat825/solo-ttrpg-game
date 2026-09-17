using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Audio;
using Content.Classes;
using Content.Entities;
using Content.Items;
using Content.Kits;
using Content.Minis;
using Content.Places;
using Content.Quests;
using Content.World;
using Content.Models;
using Content.Companions;
using Content.Dialogue;
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
        // THE WORLD LAYER. A place owns a map and a fight is one of the things that can
        // happen in it, so the folder is named for the room rather than for the brawl. entities/
        // is Bob and the chest and the door; quests/ is the named view over facts; roads/ joins
        // places to each other. PLACES_AND_PERSISTENCE.md.
        public const string PlacesFolder = "places";

        // format 1's spelling of places/, read for exactly as long as ContentFormat.Oldest is 1
        public const string EncountersFolder = "encounters";

        public const string EntitiesFolder = "entities";

        public const string QuestsFolder = "quests";

        public const string RoadsFolder = "roads";
        public const string LocaleFolder = "locale";
        public const string AssetsFolder = "assets";

        public const string ClassesFolder = "classes";

        public const string KitsFolder = "kits";

        public const string MinisFolder = "minis";
        public const string ModelsFolder = "models";
        public const string AudioFolder = "audio";

        // Phase W. The branching lives in dialogue/, the short reactions in barks/, the intents
        // every companion must be able to deliver in beats/, and what a hint escalates through in
        // hints/. companions/ says who is sitting on the table to say any of it.
        public const string DialogueFolder = "dialogue";

        public const string BarksFolder = "barks";

        public const string BeatsFolder = "beats";

        public const string HintsFolder = "hints";

        public const string CompanionsFolder = "companions";

        // Phase R. What the blanks on the character sheet offer.
        public const string SheetFolder = "sheet";

        public const string MapExtension = ".map";

        Package(string folder, string id, Manifest manifest, JsonArchetypeSource monsters,
                ItemCatalogue items, PlaceBook places,
                IReadOnlyDictionary<string, MapLayout> maps, MiniCatalogue minis,
                ClassRoster classes, KitBook kit, List<ContentProblem> problems)
        {
            Folder = folder;
            Id = id;
            Manifest = manifest;
            Monsters = monsters;
            Items = items;
            Places = places;
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

        public PlaceBook Places { get; }

        // keyed by file name without the extension, which is how a place names one
        public IReadOnlyDictionary<string, MapLayout> Maps { get; }

        public ClassRoster Classes { get; }

        public KitBook Kit { get; }

        public MiniCatalogue Minis { get; }

        // Phase W's four folders and Phase R's one. Set after construction rather than through the
        // constructor: a package already takes ten collaborators and a longer list stops being read.
        public Content.Dialogue.DialogueBook Dialogue { get; private set; } =
            Content.Dialogue.DialogueBook.Read(null, "");

        public Content.Dialogue.BarkBook Barks { get; private set; } =
            Content.Dialogue.BarkBook.Read(null);

        public Content.Dialogue.BeatBook Beats { get; private set; } =
            Content.Dialogue.BeatBook.Read(null, "");

        public Content.Dialogue.HintBook Hints { get; private set; } =
            Content.Dialogue.HintBook.Read(null, "");

        public Content.Companions.CompanionBook Companions { get; private set; } =
            Content.Companions.CompanionBook.Read(null, "");

        // Phase R. What the blanks on a character sheet offer (sheet/races/, sheet/backgrounds/)
        public Content.Sheet.SheetOptions Sheet { get; private set; } =
            Content.Sheet.SheetOptions.Read(null, "");

        // The World layer's three, set the same way and for the same reason.
        public EntityBook Entities { get; private set; } = EntityBook.Read(null, null);

        public QuestBook Quests { get; private set; } = QuestBook.Read(null);

        public RoadBook Roads { get; private set; } = RoadBook.Read(null);

        public IReadOnlyList<ContentProblem> Problems { get; }

        // a failed package contributes nothing; all-or-nothing avoids a half-installed campaign
        public bool Failed => Manifest == null;

        // a caution is a sentence somebody should read, not a thing that stopped the loader
        public bool Clean => !Problems.Any(p => p.IsAFault);

        public IEnumerable<ContentProblem> Faults => Problems.Where(p => p.IsAFault);

        public IEnumerable<ContentProblem> Cautions => Problems.Where(p => p.IsACaution);


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
            PlaceBook places = ReadPlaces(folder);
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
            Gather(problems, PlacesIn(folder), places.Problems);

            foreach (ContentProblem problem in classes.Problems)
                problems.Add(new ContentProblem(ClassesFolder + "/" + problem.File, problem.Where,
                                                problem.What, problem.Line, problem.How));

            foreach (ContentProblem problem in kit.Problems)
                problems.Add(new ContentProblem(KitsFolder + "/" + problem.File, problem.Where,
                                                problem.What, problem.Line, problem.How));

            foreach (ContentProblem problem in minis.Problems)
                problems.Add(new ContentProblem(MinisFolder + "/" + problem.File, problem.Where,
                                                problem.What, problem.Line, problem.How));

            var package = new Package(folder, id, manifest, monsters, items, places, maps,
                                      minis, classes, kit, problems)
            {
                ManifestFile = ManifestFileIn(folder),
                Dialogue = Content.Dialogue.DialogueBook.Read(In(folder, DialogueFolder), id),
                Barks = Content.Dialogue.BarkBook.Read(In(folder, BarksFolder)),
                Beats = Content.Dialogue.BeatBook.Read(In(folder, BeatsFolder), id),
                Hints = Content.Dialogue.HintBook.Read(In(folder, HintsFolder), id),
                Companions = Content.Companions.CompanionBook.Read(In(folder, CompanionsFolder), id),
                Sheet = Content.Sheet.SheetOptions.Read(In(folder, SheetFolder), id),
                Entities = EntityBook.Read(In(folder, EntitiesFolder), id),
                Quests = QuestBook.Read(In(folder, QuestsFolder)),
                Roads = RoadBook.Read(In(folder, RoadsFolder)),
            };

            Gather(problems, DialogueFolder, package.Dialogue.Problems);
            Gather(problems, BarksFolder, package.Barks.Problems);
            Gather(problems, BeatsFolder, package.Beats.Problems);
            Gather(problems, HintsFolder, package.Hints.Problems);
            Gather(problems, CompanionsFolder, package.Companions.Problems);
            Gather(problems, SheetFolder, package.Sheet.Problems);
            Gather(problems, EntitiesFolder, package.Entities.Problems);
            Gather(problems, QuestsFolder, package.Quests.Problems);
            Gather(problems, RoadsFolder, package.Roads.Problems);

            // a race and a class sharing an id collide in the class namespace, and one of the two
            // names would silently never be seen
            package.Sheet.MustNotCollideWith(classes, problems);

            package.CrossCheck(problems);

            return package;
        }

        static string In(string folder, string inside) => Path.Combine(folder, inside);

        // places/ if it is there, encounters/ if it is not - a campaign authored before the map
        // became the noun still reads, and its encounters ARE places whose standings are all foes
        static string PlacesIn(string folder) =>
            !Directory.Exists(In(folder, PlacesFolder)) &&
            Directory.Exists(In(folder, EncountersFolder))
                ? EncountersFolder
                : PlacesFolder;

        static PlaceBook ReadPlaces(string folder) => PlaceBook.Read(In(folder, PlacesIn(folder)));

        // a book names its files relative to itself; the campaign puts the folder back on the front
        // so an author reads "dialogue/camp.yarn: line 12" and knows where to look
        static void Gather(List<ContentProblem> problems, string folder,
                           IReadOnlyList<ContentProblem> found)
        {
            foreach (ContentProblem problem in found)
                problems.Add(problem.File.StartsWith(folder + "/", StringComparison.Ordinal)
                    ? problem
                    : new ContentProblem(folder + "/" + problem.File, problem.Where, problem.What,
                                         problem.Line, problem.How));
        }

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
            string book = PlacesIn(Folder) + "/";

            foreach (Place place in Places.All)
            {
                string file = book + place.Id + PlaceBook.Extension;

                if (!Maps.TryGetValue(place.Map, out MapLayout map))
                {
                    problems.Add(new ContentProblem(
                        file, "map",
                        $"'{place.Map}' is not one of this campaign's maps - it has " +
                        $"{Offered(Maps.Keys)}"));
                    map = null;
                }

                int at = 0;

                foreach (Standing standing in place.Standings)
                {
                    string where = $"standing[{at++}]";

                    if (standing.IsAFoe)
                    {
                        if (!Monsters.Has(ContentId.Scoped(Id, standing.Monster)))
                            problems.Add(new ContentProblem(
                                file, where + ".monster",
                                $"'{standing.Monster}' is not one of this campaign's monsters - " +
                                $"it has {Offered(Local(Monsters.Ids))}"));
                    }
                    else if (!Entities.Has(standing.Entity))
                    {
                        problems.Add(new ContentProblem(
                            file, where + ".entity",
                            $"'{standing.Entity}' is not one of this campaign's entities - it " +
                            $"has {Offered(Entities.Ids.Select(ContentId.LocalOf))}"));
                    }

                    Drawn(map, place, standing.Slot, file, where + ".slot", problems);
                }

                at = 0;

                foreach (Exit exit in place.Exits)
                {
                    string where = $"exits[{at++}]";

                    Drawn(map, place, exit.Slot, file, where + ".slot", problems);

                    if (!Places.Has(exit.To))
                    {
                        problems.Add(new ContentProblem(
                            file, where + ".to",
                            $"'{exit.To}' is not one of this campaign's places - it has " +
                            $"{Offered(Places.Ids)}"));
                        continue;
                    }

                    // where you come out has to be a square somebody drew on the map over there
                    if (exit.Arriving > 0)
                    {
                        Place there = Places.Of(exit.To);

                        if (Maps.TryGetValue(there.Map, out MapLayout other) &&
                            other.SpawnAt(exit.Arriving) == null)
                            problems.Add(new ContentProblem(
                                file, where + ".arriving",
                                $"{there.Map} has no spawn {exit.Arriving} drawn on it for you to " +
                                $"arrive on - it has {Spawns(other)}"));
                    }

                    if (!exit.IsAJourney) continue;

                    Road road = Roads.Of(exit.Road);

                    if (road == null)
                    {
                        problems.Add(new ContentProblem(
                            file, where + ".road",
                            $"'{exit.Road}' is not one of this campaign's roads - it has " +
                            $"{Offered(Roads.Ids)}"));
                        continue;
                    }

                    if (!road.Joins(place.Id, exit.To))
                        problems.Add(new ContentProblem(
                            file, where + ".road",
                            $"'{road.Id}' runs between '{road.From}' and '{road.To}', and this " +
                            $"exit walks it from '{place.Id}' to '{exit.To}' - a road joins the " +
                            "two places it says it does"));
                }

                at = 0;

                foreach (Cue cue in place.Cues)
                {
                    string cueWhere = $"cues[{at++}].at";

                    if (cue.Slot > 0)
                        Drawn(map, place, cue.Slot, file, cueWhere, problems,
                              $"for the DM to {cue.Does.Word()} at");
                }

                at = 0;

                foreach (Trigger trigger in place.Triggers)
                {
                    string where = $"triggers[{at++}].place";

                    if (trigger.ThenDo == Then.Goto && !Places.Has(trigger.Place))
                        problems.Add(new ContentProblem(
                            file, where,
                            $"'{trigger.Place}' is not one of this campaign's places - it has " +
                            $"{Offered(Places.Ids)}"));
                }
            }

            // a place no chapter names and no exit leads to can never be reached
            var named = new HashSet<string>(StringComparer.Ordinal);
            int chapter = 0;

            foreach (Chapter one in Manifest.Chapters)
            {
                int index = 0;

                foreach (string place in one.Places)
                {
                    named.Add(place);

                    if (!Places.Has(place))
                        problems.Add(new ContentProblem(
                            ManifestFile,
                            $"chapters[{chapter}].places[{index}]",
                            $"'{place}' is not one of this campaign's places - it has " +
                            $"{Offered(Places.Ids)}"));

                    index++;
                }

                chapter++;
            }

            foreach (Place place in Places.All)
            {
                foreach (Exit exit in place.Exits) named.Add(exit.To);

                foreach (Trigger trigger in place.Triggers)
                    if (trigger.ThenDo == Then.Goto) named.Add(trigger.Place);
            }

            foreach (Road road in Roads.All)
            {
                string file = RoadsFolder + "/" + road.Id + RoadBook.Extension;

                foreach ((string end, string where) in new[] { (road.From, "from"), (road.To, "to") })
                    if (!Places.Has(end))
                        problems.Add(new ContentProblem(
                            file, where,
                            $"'{end}' is not one of this campaign's places - it has " +
                            $"{Offered(Places.Ids)}"));

                int at = 0;

                foreach (Happening happening in road.Wayside)
                {
                    string where = $"wayside[{at++}].place";

                    if (!happening.Stops) continue;

                    named.Add(happening.Place);

                    if (!Places.Has(happening.Place))
                        problems.Add(new ContentProblem(
                            file, where,
                            $"'{happening.Place}' is not one of this campaign's places - it has " +
                            $"{Offered(Places.Ids)}"));
                }
            }

            foreach (string orphan in Places.Ids.OrderBy(i => i, StringComparer.Ordinal))
                if (!named.Contains(orphan))
                    problems.Add(new ContentProblem(
                        book + orphan + PlaceBook.Extension, "id",
                        "no chapter names this place, no exit leads to it and nothing goes to it, " +
                        "so it can never be reached - put it in a chapter, give something a way " +
                        "in, or delete the file"));

            // a road nothing walks is a road that will never be walked
            var walked = new HashSet<string>(
                Places.All.SelectMany(p => p.Exits).Where(e => e.IsAJourney).Select(e => e.Road),
                StringComparer.Ordinal);

            foreach (Road road in Roads.All)
                if (!walked.Contains(road.Id))
                    problems.Add(new ContentProblem(
                        RoadsFolder + "/" + road.Id + RoadBook.Extension, "id",
                        $"no exit walks this road - an exit out of '{road.From}' or '{road.To}' " +
                        "has to name it, or nobody will ever travel it"));

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

            CrossCheckVoices(problems);

            CrossCheckEntities(problems);

            CrossCheckNames(problems);

            CrossCheckFacts(problems);

            if (Manifest.Preview.Length > 0 &&
                !File.Exists(Path.Combine(Folder, Manifest.Preview)))
                problems.Add(new ContentProblem(
                    ManifestFile, "preview",
                    $"there is no '{Manifest.Preview}' in this campaign's folder"));
        }

        void Drawn(MapLayout map, Place place, int slot, string file, string where,
                   List<ContentProblem> problems, string saying = "to stand on")
        {
            if (map == null || map.SpawnAt(slot) != null) return;

            problems.Add(new ContentProblem(
                file, where,
                $"{place.Map} has no spawn {slot} drawn on it {saying} - it has {Spawns(map)}"));
        }

        static string Spawns(MapLayout map) =>
            map == null
                ? "none at all"
                : Offered(map.Spawns.Keys.OrderBy(s => s).Select(s => s.ToString()));

        // Bob is data, and everything he names has to be there: the statblock he fights as if the
        // author let you fight him, the conversation he has, the figure he stands as, the loot in
        // the chest (PLACES_AND_PERSISTENCE.md section 4).
        void CrossCheckEntities(List<ContentProblem> problems)
        {
            foreach (Entity entity in Entities.All)
            {
                string file = EntitiesFolder + "/" + entity.Local + EntityBook.Extension;

                if (entity.Fights && !Monsters.Has(ContentId.Scoped(Id, entity.Monster)))
                    problems.Add(new ContentProblem(
                        file, "monster",
                        $"'{entity.Monster}' is not one of this campaign's monsters - it has " +
                        $"{Offered(Local(Monsters.Ids))}. Something you may attack has to have a " +
                        "statblock to be attacked as"));

                if (entity.Mini.Length > 0 && !Names(entity.Mini))
                    problems.Add(new ContentProblem(
                        file, "mini",
                        $"'{entity.Mini}' is not a mini this pack ships and not one the game " +
                        $"ships ({Offered(SharedMinis.Catalogue.Ids)}) - if it belongs to another " +
                        "pack, write it as '<pack>.<mini>' and name that pack in dependencies"));

                string node = entity.Named(Content.Entities.Interaction.Talk);

                if (node.Length > 0 && !Dialogue.Has(node))
                    problems.Add(new ContentProblem(
                        file, "can.talk",
                        $"'{node}' is not a node in this campaign's {DialogueFolder}/ folder - it " +
                        $"has {Offered(Dialogue.Nodes.OrderBy(n => n, StringComparer.Ordinal))}"));

                foreach (string item in entity.Loot.Entries.Select(e => e.Item).Where(i => i != null))
                    if (!Items.Has(item))
                        problems.Add(new ContentProblem(
                            file, "loot",
                            $"'{item}' is not one of this campaign's items - a campaign has to " +
                            $"ship what it holds. It has {Offered(Items.Ids)}"));
            }
        }

        // TWO IDS, ONE KEY. A campaign's chapters, places, quests and roads all name themselves
        // under quest.<campaign>.*, and its monsters and entities both under actor.<campaign>.*,
        // so two of them sharing an id means one of the two names is written, translated and never
        // seen. This is the same refusal Phase R made for a race and a class (CONVENTIONS.md 7).
        void CrossCheckNames(List<ContentProblem> problems)
        {
            var claimed = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (Chapter chapter in Manifest.Chapters) claimed[chapter.Id] = "a chapter";

            foreach (Place place in Places.All)
                Claim(claimed, place.Id, "a place",
                      PlacesIn(Folder) + "/" + place.Id + PlaceBook.Extension, problems);

            foreach (Quest quest in Quests.All)
                Claim(claimed, quest.Id, "a quest",
                      QuestsFolder + "/" + quest.Id + QuestBook.Extension, problems);

            foreach (Road road in Roads.All)
                Claim(claimed, road.Id, "a road",
                      RoadsFolder + "/" + road.Id + RoadBook.Extension, problems);

            var actors = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (string id in Monsters.Ids) actors[ContentId.LocalOf(id)] = "a monster";

            foreach (Entity entity in Entities.All)
                Claim(actors, entity.Local, "an entity",
                      EntitiesFolder + "/" + entity.Local + EntityBook.Extension, problems);
        }

        static void Claim(Dictionary<string, string> claimed, string id, string what, string file,
                          List<ContentProblem> problems)
        {
            if (claimed.TryGetValue(id, out string already))
            {
                problems.Add(new ContentProblem(
                    file, "id",
                    $"'{id}' is already the id of {already} in this campaign, and both would " +
                    "claim the same key - one of the two names would be written, translated and " +
                    "never seen"));
                return;
            }

            claimed[id] = what;
        }

        // THE REACHABILITY PASS (PLACES_AND_PERSISTENCE.md sections 6 and 10). The World layer
        // cannot be chi-squared - you cannot statistically test a town - so what "correct" means
        // for it is this: every fact anything waits on is a fact something can make true, and every
        // quest is either completable or explicitly allowed to fail.
        void CrossCheckFacts(List<ContentProblem> problems)
        {
            HashSet<string> written = Written();

            foreach (Place place in Places.All)
            {
                string file = PlacesIn(Folder) + "/" + place.Id + PlaceBook.Extension;
                int at = 0;

                foreach (Standing standing in place.Standings)
                    Reachable(standing.Needs.Facts, written, file, $"standing[{at++}]", problems);

                at = 0;

                foreach (Exit exit in place.Exits)
                    Reachable(exit.Needs.Facts, written, file, $"exits[{at++}]", problems);

                at = 0;

                foreach (Trigger trigger in place.Triggers)
                {
                    string where = $"triggers[{at++}].fact";

                    if (trigger.WhenIt == When.Fact)
                        Reachable(new[] { trigger.Fact }, written, file, where, problems);
                }

                at = 0;

                foreach (Cue cue in place.Cues)
                {
                    string where = $"cues[{at++}].fact";

                    if (cue.WhenIt == When.Fact)
                        Reachable(new[] { cue.Fact }, written, file, where, problems);
                }
            }

            foreach (Quest quest in Quests.All)
            {
                string file = QuestsFolder + "/" + quest.Id + QuestBook.Extension;

                Reachable(quest.Offered.Facts, written, file, "offered", problems);
                Reachable(quest.Done.Facts, written, file, "done", problems);

                if (quest.CanFail) Reachable(quest.Failed.Facts, written, file, "failed", problems);

                // THE WARNING SECTION 6 ASKS FOR BY NAME, and it is a warning: if the author wants
                // that consequence, wonderful. It is only that nobody should find out by playing.
                foreach (string fact in quest.Done.Unless)
                {
                    string subject = Content.World.FactName.SubjectOf(
                        fact, Content.World.FactName.DeadAspect);

                    if (subject.Length == 0) continue;

                    Entity entity = Entities.Of(subject);

                    if (entity == null || !entity.Allows(Content.Entities.Interaction.Attack))
                        continue;

                    problems.Add(ContentProblem.Caution(
                        file, "done.unless",
                        $"this quest cannot be finished once '{subject}' is dead, and '{subject}' " +
                        "is attackable - killing them may make it impossible. That is allowed, " +
                        (quest.CanFail
                            ? "and this quest says what failing looks like, so the log will say so"
                            : "but this quest has no 'failed' clause, so the log will sit there " +
                              "forever telling the player to go back to somebody who is dead")));
                }

                if (!quest.CanFail) continue;
            }
        }

        // every fact anything in this campaign can ever make true: the ones the engine derives
        // from what it is asked to do, and the ones an author writes by hand
        HashSet<string> Written()
        {
            var written = new HashSet<string>(StringComparer.Ordinal);

            foreach (Entity entity in Entities.All)
                foreach (Content.Entities.Interaction verb in entity.Verbs)
                {
                    string writes = verb.Writes(entity.Local);

                    if (writes.Length > 0) written.Add(writes);
                }

            foreach (Place place in Places.All)
            {
                written.Add(Content.World.FactName.Visited(place.Id));

                if (place.IsAFight) written.Add(Content.World.FactName.Cleared(place.Id));

                // a standing that is somebody the author let you kill dies as a durable fact
                foreach (Standing standing in place.Standings)
                {
                    if (standing.IsAFoe) continue;

                    Entity entity = Entities.Of(standing.Entity);

                    if (entity != null && entity.Fights)
                        written.Add(Content.World.FactName.Dead(entity.Local));
                }

                foreach (Trigger trigger in place.Triggers)
                    if (trigger.Sets.Length > 0) written.Add(trigger.Sets);

                // a door an author opens with a fact; derived at both ends so they cannot disagree
                if (Maps.TryGetValue(place.Map, out MapLayout map))
                    foreach (Border border in map.Borders)
                        if (map.At(border) == Edge.Door)
                            written.Add(Content.World.Exploring.DoorFact(place.Id, border));
            }

            foreach (Quest quest in Quests.All) written.Add(quest.AcceptedFact);

            foreach (Road road in Roads.All)
                foreach (Happening happening in road.Wayside)
                {
                    if (happening.Once) written.Add(happening.HappenedFact);

                    if (happening.Sets.Length > 0) written.Add(happening.Sets);
                }

            return written;
        }

        void Reachable(IEnumerable<string> facts, HashSet<string> written, string file,
                       string where, List<ContentProblem> problems)
        {
            foreach (string fact in facts)
            {
                if (fact.Length == 0 || written.Contains(fact)) continue;

                problems.Add(new ContentProblem(
                    file, where,
                    $"nothing in this campaign ever makes '{fact}' true, so whatever waits on it " +
                    "waits forever. The engine writes a fact for each thing it is asked to do - " +
                    $"{Offered(Content.World.FactName.Aspects.Select(a => "<something>." + a))} - " +
                    "and a trigger or a wayside event writes the rest"));
            }
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

                if (card.HasACompanion && !Keeps(card.Companion))
                    problems.Add(new ContentProblem(
                        file, "companion",
                        $"'{card.Companion}' is not a companion this pack ships - it has " +
                        $"{Offered(Companions.Ids.Select(ContentId.LocalOf).OrderBy(i => i, StringComparer.Ordinal))}. " +
                        "If it belongs to another pack, write it as '<pack>.<companion>' and name " +
                        "that pack in dependencies"));

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

        // Phase W's half of the cross-check: a spine nobody can deliver, a rung with no beat behind
        // it, and a beat nothing ever reaches. All three compile perfectly and all three are silence.
        void CrossCheckVoices(List<ContentProblem> problems)
        {
            var reached = new HashSet<string>(StringComparer.Ordinal);

            foreach (Hint hint in Hints.All)
            {
                string file = HintsFolder + "/";
                int rung = 0;

                foreach (string id in hint.Rungs)
                {
                    rung++;
                    reached.Add(id);

                    Beat beat = Beats.Of(id);

                    if (beat == null)
                    {
                        problems.Add(new ContentProblem(
                            file, $"{hint.Id}.rungs[{rung - 1}]",
                            $"'{id}' is not one of this campaign's beats - it has " +
                            $"{Offered(Beats.Ids.OrderBy(i => i, StringComparer.Ordinal))}"));
                        continue;
                    }

                    if (beat.Kind == BeatKind.Hint) continue;

                    problems.Add(new ContentProblem(
                        file, $"{hint.Id}.rungs[{rung - 1}]",
                        $"'{id}' is a '{beat.Kind.Word()}' beat and a hint rung has to be a " +
                        $"'{BeatKind.Hint.Word()}' one - the kinds are what tell a reader which " +
                        "lines are answers to a question nobody has to ask"));
                }
            }

            foreach (Place place in Places.All)
            {
                string file = PlacesIn(Folder) + "/" + place.Id + PlaceBook.Extension;
                int at = 0;

                foreach (Cue cue in place.Cues)
                {
                    string where = $"cues[{at++}].beat";

                    if (!cue.Prompts) continue;

                    reached.Add(cue.Beat);

                    if (Beats.Has(cue.Beat)) continue;

                    problems.Add(new ContentProblem(
                        file, where,
                        $"'{cue.Beat}' is not one of this campaign's beats - it has " +
                        $"{Offered(Beats.Ids.OrderBy(i => i, StringComparer.Ordinal))}"));
                }
            }

            // the same argument as an encounter no chapter names: written, translated, never heard
            foreach (Beat beat in Beats.All)
            {
                if (beat.Kind == BeatKind.Colour || reached.Contains(beat.Id)) continue;

                problems.Add(new ContentProblem(
                    BeatsFolder + "/", beat.Id,
                    $"nothing reaches this beat - a '{beat.Kind.Word()}' beat is delivered by a " +
                    "cue that names it or by a rung of a hint, and one with neither is a line " +
                    "every companion will be asked to write and no player will ever hear"));
            }

            // WHO CAN SAY THESE LINES IS NOT A QUESTION THIS FOLDER CAN ANSWER. A voice is
            // deliberately un-prefixed - dialogue.wolf.* is base-game shared vocabulary and belongs
            // to no campaign (CONVENTIONS.md section 7) - so a campaign writing for the wolf has no
            // '<pack>.<voice>' form to write the way a cross-pack mini id does. That makes it a
            // shelf question, and Shelf.CrossCheck asks it where every pack is known.
        }

        // the creatures this pack can speak as, for the shelf to gather up
        public IEnumerable<string> Voices =>
            Companions.Voices
                      .Concat(Barks.Speakers)
                      .Distinct(StringComparer.Ordinal)
                      .OrderBy(v => v, StringComparer.Ordinal);

        // same rule as a mini: a dotted id naming another pack is allowed through, the shelf resolves it
        bool Keeps(string companion) =>
            ContentId.IsScoped(companion)
                ? Companions.Has(companion) || ContentId.CampaignOf(companion) != Id
                : Companions.Has(ContentId.Scoped(Id, companion));

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
                    problem.What, problem.Line, problem.How));
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
                   $"{Maps.Count} maps, {Places.Count} places" +
                   (Entities.Count > 0 ? $", {Entities.Count} entities" : "") +
                   (Quests.Count > 0 ? $", {Quests.Count} quests" : "") +
                   (Roads.Count > 0 ? $", {Roads.Count} roads" : "") +
                   (Minis.Ids.Count > 0 ? $", {Minis.Ids.Count} minis" : "") +
                   (Classes.Ids.Count > 0 ? $", {Classes.Ids.Count} classes" : "") +
                   (Kit.Ids.Count > 0 ? $", {Kit.Ids.Count} abilities" : "") +
                   (Companions.Count > 0 ? $", {Companions.Count} companions" : "") +
                   (Dialogue.Count > 0 ? $", {Dialogue.Count} lines in {Dialogue.Nodes.Count} nodes" : "") +
                   (Barks.Lines > 0 ? $", {Barks.Lines} barks" : "") +
                   (Beats.Count > 0 ? $", {Beats.Count} beats" : "") +
                   (Hints.Count > 0 ? $", {Hints.Count} hints" : "") +
                   (Sheet.Count > 0 ? $", {Sheet}" : "") +
                   (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
        }
    }
}
