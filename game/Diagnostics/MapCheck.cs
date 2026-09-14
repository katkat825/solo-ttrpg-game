using System.Collections.Generic;
using Godot;
using Core.Space;

namespace Game.Diagnostics
{
    // checks every shipped map parses. A map that half-loads is a room with a wall missing and
    // nothing to tell you: MapReader refuses a broken one and names the line, but Board.Load then
    // falls back to an empty room and carries on - so a malformed map that SHIPPED is silent in the
    // game, a black-ish fallback nobody asked for rather than an error anybody sees. This is the
    // machine that makes it loud: read every res://maps/*.map, run it through the same MapReader the
    // board uses, and fail naming any that don't parse.
    //
    // The board's analog of check-locale, and the "board sanity sweep" HeadlessCheck already names.
    // Phase P points it at a campaign's own maps/ folder too (ARCHITECTURE.md 4) - MapReader takes
    // TEXT, so the only thing that changes is which folder is walked. That is P2, and it turned out
    // to be exactly what was predicted: every campaign on disk gets its maps/ walked with the same
    // reader and reported the same way, and a stranger's broken room is named rather than silently
    // replaced with the empty-room fallback. Run it with check-maps.ps1.
    //
    // everything printed is developer diagnostic, exempt from localization like Actor.DebugName
    public partial class MapCheck : HeadlessCheck
    {
        protected override string Subject => "maps";

        // where the shipped maps live. one folder today; Phase P adds a campaign's own
        [Export] public string MapsFolder { get; set; } = "res://maps/";

        public override void _Ready()
        {
            string folder = MapsFolder.EndsWith("/") ? MapsFolder : MapsFolder + "/";

            GD.Print($"map check: {folder}");
            GD.Print("");

            using DirAccess dir = DirAccess.Open(folder);

            if (dir == null)
            {
                Problem($"cannot open {folder} - {DirAccess.GetOpenError()}");
                GD.Print("");
                Finish();
                return;
            }

            int found = 0;

            foreach (string name in Names(dir))
            {
                found++;

                string path = folder + name;

                if (!FileAccess.FileExists(path))
                {
                    Problem($"{name} - listed by the folder but cannot be opened");
                    continue;
                }

                string text = FileAccess.GetFileAsString(path);

                // the same reader the game runs, so this passes for exactly the maps the board can
                // load and fails for exactly the ones it would fall back on
                if (MapReader.TryRead(text, out MapLayout map, out string problem))
                    GD.Print($"  ok    {name} - {map}");
                else
                    Problem($"{name} - {problem}");
            }

            // a maps folder with nothing in it is not a pass: the board has nothing to load and
            // would ship the empty-room fallback as the whole game
            if (found == 0)
                Problem($"no .map files in {folder} - the board has nothing to load");

            CheckCampaigns();

            GD.Print("");
            Finish();
        }

        // EVERY CAMPAIGN'S OWN ROOMS (P2). Read with System.IO rather than DirAccess, because a
        // campaign folder is outside the .pck and is an ordinary directory on an ordinary disk -
        // the same reason `Content.*` reads its JSON that way.
        //
        // A campaign with no maps/ is not a problem: it may be using the ones that ship, and P4
        // has a campaign.json to say which. A campaign with a BROKEN one is a problem, and it is
        // named with the campaign it came from so a report over eight subscribed items is readable
        void CheckCampaigns()
        {
            foreach (string root in Game.Campaigns.CampaignFolders.Roots())
            {
                foreach (string campaign in Game.Campaigns.CampaignFolders.In(root))
                {
                    string maps = System.IO.Path.Combine(campaign, MapsInACampaign);

                    if (!System.IO.Directory.Exists(maps)) continue;

                    string name = System.IO.Path.GetFileName(campaign);
                    var found = new SortedSet<string>(System.IO.Directory.GetFiles(maps, "*.map"));

                    GD.Print("");
                    GD.Print($"map check: {name}/{MapsInACampaign}/");
                    GD.Print("");

                    if (found.Count == 0)
                    {
                        Problem($"{name} has a {MapsInACampaign}/ folder with no .map files in it");
                        continue;
                    }

                    foreach (string path in found) Check(name, path);
                }
            }
        }

        void Check(string campaign, string path)
        {
            string name = System.IO.Path.GetFileName(path);
            string text;

            try
            {
                text = System.IO.File.ReadAllText(path);
            }
            catch (System.Exception could)
            {
                Problem($"{campaign}/{name} - could not be read, {could.Message}");
                return;
            }

            if (!MapReader.TryRead(text, out MapLayout map, out string problem))
            {
                Problem($"{campaign}/{name} - {problem}");
                return;
            }

            // and what an encounter has to work with. A room with no spawns in it is a room nothing
            // can be placed in except the hero, which is a perfectly good room to walk through and
            // worth saying out loud rather than discovering when a fight mustered nobody
            string spawns = map.Spawns.Count == 0
                ? "no spawn slots"
                : $"spawns {string.Join(", ", Slots(map))}";

            GD.Print($"  ok    {name} - {map}, {spawns}");
        }

        static IEnumerable<string> Slots(MapLayout map)
        {
            var slots = new SortedSet<int>(map.Spawns.Keys);

            foreach (int slot in slots) yield return $"{slot} at {map.Spawns[slot]}";
        }

        // NOT A SECOND OPINION ABOUT WHERE MAPS LIVE. `Package` is what a campaign folder IS
        // (P4), so the layout is described there and read from there - a check that walked a
        // different folder from the loader would be a check of nothing
        public const string MapsInACampaign = Content.Campaigns.Package.MapsFolder;

        // every .map in the folder, sorted so the report is stable and diffs cleanly. subfolders
        // are left alone - a campaign's maps are a Phase P folder of their own, not a nesting here.
        // three spellings are folded together, the same way ImpactPool folds sample names: in the
        // source tree a map is x.map, and in an exported build a non-resource file is listed as
        // x.map.remap - handling one and not the other works right up until the export
        static SortedSet<string> Names(DirAccess dir)
        {
            var names = new SortedSet<string>();

            foreach (string entry in dir.GetFiles())
            {
                string name = entry;

                if (name.EndsWith(".remap") || name.EndsWith(".import")) name = name.GetBaseName();

                if (name.EndsWith(".map")) names.Add(name);
            }

            return names;
        }
    }
}
