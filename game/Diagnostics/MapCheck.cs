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
    // TEXT, so the only thing that changes is which folder is walked. Run it with check-maps.ps1.
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

            GD.Print("");
            Finish();
        }

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
