using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Campaigns;
using Content.Entities;
using Content.Places;
using Content.Quests;
using Content.Saves;
using Content.Schema;
using Content.World;
using Core.Dice;
using Core.Space;

namespace Game.Diagnostics
{
    public partial class WorldCheck : HeadlessCheck
    {
        protected override string Subject => "world";

        // the campaign to walk; saltmarch is the one written to exercise all of it
        [Export] public string CampaignId { get; set; } = "saltmarch";

        [Export] public int Seed { get; set; } = 4242;

        public override void _Ready()
        {
            foreach (string arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--campaign=", System.StringComparison.Ordinal))
                    CampaignId = arg.Substring("--campaign=".Length).Trim();

                if (arg.StartsWith("--seed=", System.StringComparison.Ordinal) &&
                    int.TryParse(arg.Substring("--seed=".Length).Trim(), out int seed))
                    Seed = seed;
            }

            Package package = Find(CampaignId);

            if (package == null)
            {
                Problem($"there is no campaign called '{CampaignId}' on this machine");
                Finish();
                return;
            }

            Game.Campaigns.CampaignLocale.Register(package.Folder);

            GD.Print($"world check: {package.Folder}");
            GD.Print("");
            GD.Print($"  {package}");
            GD.Print("");

            foreach (ContentProblem problem in package.Problems)
                if (problem.IsACaution) Caution("  " + problem);
                else Problem("  " + problem);

            if (package.Failed)
            {
                Problem("the campaign did not load, so there is no world to walk");
                Finish();
                return;
            }

            if (package.Places.Count == 0)
            {
                Problem("this campaign has no places in it, so there is nowhere to go");
                Finish();
                return;
            }

            Walk(package);

            Finish();
        }

        static Package Find(string id)
        {
            foreach (string root in Game.Campaigns.CampaignFolders.Roots())
                foreach (string folder in Game.Campaigns.CampaignFolders.In(root))
                {
                    Package package = Package.Read(folder);

                    if (package.Id == id) return package;
                }

            return null;
        }


        void Walk(Package package)
        {
            var facts = Facts.For(package.Id);
            var world = new Exploring(package, facts, new SeededRng(Seed));

            // every place, entered in its own right, because every place has to stand on its own
            foreach (Place place in package.Places.All) Visit(package, world, place);

            GD.Print("");

            Travel(package, world);

            GD.Print("");

            Log(package, world);

            GD.Print("");

            RoundTrip(package, world);
        }

        void Visit(Package package, Exploring world, Place place)
        {
            Arrival arrival = world.Enter(place.Id);

            if (arrival.Refused)
            {
                Problem($"could not walk into '{place.Id}' - {arrival.Why}");
                return;
            }

            GD.Print($"  {place.Id} - '{Text(place.NameKey(package.Id))}', in at {arrival.Hero}" +
                     (arrival.First ? ", for the first time" : ""));

            if (!world.Facts.Is(Content.World.FactName.Visited(place.Id)))
                Problem($"'{place.Id}' was entered and does not remember being visited");

            foreach (Cue cue in arrival.Cues) Said(package, cue);

            if (arrival.Moves is { } moves) GD.Print($"      and then {moves}");

            foreach (Present present in world.OnTheTable.OrderBy(p => p.Slot))
            {
                GD.Print($"      spawn {present.Slot} {present.At} - {present.Id}" +
                         (present.IsAFoe ? " (a foe, already laid out)" : "") +
                         (present.Offers.Count > 0
                             ? $", you may {string.Join(", ", present.Offers.Select(v => v.Word()))}"
                             : ""));

                if (world.Map.SpawnAt(present.Slot) != present.At)
                    Problem($"'{present.Id}' is not standing where spawn {present.Slot} is drawn");
            }

            foreach (Exit exit in world.Ways)
                GD.Print($"      way out - {exit}");

            Handle(package, world);

            Fight(package, world, place);
        }

        void Handle(Package package, Exploring world)
        {
            foreach (Present present in world.OnTheTable.OrderBy(p => p.Slot).ToArray())
            {
                if (present.IsAFoe) continue;

                foreach (Interaction verb in present.Offers.ToArray())
                {
                    // the fight is the last thing tried, in Fight below, or nothing else can be
                    if (verb == Interaction.Attack) continue;

                    Doing did = world.Do(present, verb);

                    if (did.Refused)
                    {
                        Problem($"'{present.Id}' offered '{verb.Word()}' and then refused it - " +
                                did.Why);
                        continue;
                    }

                    GD.Print($"      {did}");

                    if (did.Line.Length > 0 && Missing(did.Line))
                        Problem($"'{did.Line}' is a line the DM was told to read and it has no " +
                                "English");

                    if (did.Node.Length > 0 && !package.Dialogue.Has(did.Node))
                        Problem($"'{did.Node}' is a conversation this campaign does not have");

                    foreach (Cue cue in did.Cues) Said(package, cue);

                    // a spent verb is off the menu, and is off it because a fact says so
                    Present again = world.Standing(present.Slot);

                    if (!verb.Repeatable() && again != null && again.Offering(verb))
                        Problem($"'{present.Id}' still offers '{verb.Word()}' after it was done");
                }
            }
        }

        // the explore-to-fight-and-back transition, begun here and not run; the boundary is what this checks
        void Fight(Package package, Exploring world, Place place)
        {
            Present swung = world.OnTheTable.FirstOrDefault(
                p => !p.IsAFoe && p.Offering(Interaction.Attack));

            Episode episode = swung != null
                ? world.Do(swung, Interaction.Attack).Fight
                : place.IsAFight ? world.Begin(null) : null;

            if (episode == null) return;

            GD.Print($"      {episode}");

            if (!world.InAFight) Problem("a fight began and the world does not think it is in one");

            if (!episode.IsReal)
                Problem($"a fight began in '{place.Id}' against nobody");

            foreach (string id in episode.Roster.Where(i => i != null))
                if (!package.Monsters.Has(id))
                    Problem($"'{id}' is in the roster and is not a monster this campaign ships");

            Ending ending = world.Fought(episode, won: true, down: episode.Entities.Keys);

            GD.Print($"      {ending}");

            if (world.InAFight) Problem("the fight ended and the world still thinks it is in one");

            foreach (Cue cue in ending.Cues) Said(package, cue);

            // printed so a chapter that ends silently is not indistinguishable from one that never said so
            if (ending.Moves is { } moves) GD.Print($"      and then {moves}");

            // what the fight changed is facts; what it left on the floor is nobody's business
            foreach (string entity in episode.Entities.Values)
            {
                if (!world.Facts.Is(Content.World.FactName.Dead(entity)))
                    Problem($"'{entity}' was killed and the world does not remember it");

                if (world.OnTheTable.Any(p => p.Id == entity))
                    Problem($"'{entity}' is dead and is still standing on the table");
            }

            if (place.IsAFight && world.OnTheTable.Any(p => p.IsAFoe))
                Problem($"'{place.Id}' was cleared and still has foes standing on it");
        }

        void Travel(Package package, Exploring world)
        {
            foreach (Place place in package.Places.All)
            {
                if (place.Exits.Count == 0) continue;

                world.Enter(place.Id);

                foreach (Exit exit in world.Ways.ToArray())
                {
                    Going going = world.Take(exit);

                    if (going.Refused)
                    {
                        Problem($"the way out of '{place.Id}' refused - {going.Why}");
                        continue;
                    }

                    GD.Print($"  out of {place.Id} - {going}");

                    if (going.LineKey(package.Id) is { } line && Missing(line))
                        Problem($"'{line}' is what happens on the road and it has no English");

                    Arrival there = world.Enter(going.To, going.Arriving);

                    if (there.Refused)
                        Problem($"the way out of '{place.Id}' leads nowhere - {there.Why}");
                    else
                        GD.Print($"      arrived - {there}");
                }
            }
        }

        void Log(Package package, Exploring world)
        {
            if (package.Quests.Count == 0)
            {
                GD.Print("  no quests - a campaign is allowed to be a corridor");
                return;
            }

            GD.Print("  the log, as the facts leave it:");

            foreach ((Quest quest, QuestState state) in world.Log)
            {
                GD.Print($"      {quest.Id} - '{Text(quest.TitleKey(package.Id))}' is " +
                         $"{state.Word()}");

                if (Missing(quest.TitleKey(package.Id)))
                    Problem($"'{quest.Id}' has no English title");
            }

            foreach (Quest quest in package.Quests.All)
                if (quest.StateIn(world.Facts) == QuestState.Unknown)
                    GD.Print($"      {quest.Id} - not offered yet, on this walk through");
        }

        // a save is the fact set plus where you are, so the place must come back the same, down to the menu
        void RoundTrip(Package package, Exploring world)
        {
            var save = new SaveGame { Campaign = package.Id, Place = world.Where?.Id ?? "" };

            foreach (string fact in world.Facts.All) save.Facts.Add(fact);

            Read<SaveGame> back = SaveReader.Parse(SaveWriter.Write(save), "world_check.json");

            foreach (ContentProblem problem in back.Problems) Problem("  save: " + problem);

            if (!back.Any)
            {
                Problem("the save could not be read back at all");
                return;
            }

            var loaded = Facts.For(package.Id);
            loaded.Absorb(back.Value.Facts);

            var again = new Exploring(package, loaded, new SeededRng(Seed));

            GD.Print($"  saved {save.Facts.Count} fact(s) in {save.Place}, read back " +
                     $"{back.Value.Facts.Count}");

            foreach (Place place in package.Places.All)
            {
                world.Enter(place.Id);
                again.Enter(place.Id);

                string before = Shape(world);
                string after = Shape(again);

                if (before == after)
                {
                    GD.Print($"      {place.Id} came back identically - {before}");
                    continue;
                }

                Problem($"'{place.Id}' did not come back the same - it was [{before}] and is " +
                        $"now [{after}]");
            }
        }

        static string Shape(Exploring world) =>
            world.OnTheTable.Count == 0
                ? "nobody on it"
                : string.Join(", ", world.OnTheTable.OrderBy(p => p.Slot).Select(
                      p => $"{p.Slot}:{p.Id}:{string.Join("/", p.Offers.Select(v => v.Word()))}"));

        void Said(Package package, Cue cue)
        {
            string line = cue.LineKey(package.Id);

            GD.Print($"      cue {cue.Id}" +
                     (line == null ? " - no words, just the gesture" : $" - \"{Text(line)}\""));

            if (line != null && Missing(line))
                Problem($"'{line}' is a cue the DM was given and it has no English");
        }

        // the same localizer the game uses, so a missing string shows here as it would on the table
        static string Text(string key)
        {
            string english = TranslationServer.Translate(key);

            return english == key ? "NO ENGLISH FOR THIS KEY" : english;
        }

        static bool Missing(string key) => TranslationServer.Translate(key) == key;
    }
}
