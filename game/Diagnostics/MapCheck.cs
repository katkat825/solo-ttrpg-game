using System.Collections.Generic;
using Godot;
using Core.Space;

namespace Game.Diagnostics
{
    // everything printed is developer diagnostic, exempt from localization
    public partial class MapCheck : HeadlessCheck
    {
        protected override string Subject => "maps";

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

                // the same reader the game runs, so it passes exactly the maps the board can load
                if (MapReader.TryRead(text, out MapLayout map, out string problem))
                    GD.Print($"  ok    {name} - {map}");
                else
                    Problem($"{name} - {problem}");
            }

            // an empty maps folder is not a pass: the board would ship the empty-room fallback as the whole game
            if (found == 0)
                Problem($"no .map files in {folder} - the board has nothing to load");

            CheckCampaigns();

            GD.Print("");
            Finish();
        }

        // System.IO, not DirAccess: a campaign folder is outside the .pck, an ordinary directory
        // no maps/ is fine (it may use the shipped ones); a broken one is named with its campaign
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

            // a room with no spawns holds only the hero, worth saying so before a fight musters nobody
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

        // from Package, so the check walks the same folder the loader does
        public const string MapsInACampaign = Content.Campaigns.Package.MapsFolder;

        // sorted for a stable diff; folds x.map.remap and x.map.import too, or it passes in-source and fails after export
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
