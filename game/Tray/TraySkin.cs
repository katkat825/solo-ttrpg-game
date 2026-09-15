using System.Collections.Generic;
using Godot;

namespace Game.Tray
{
    // every skin needs its own fairness sweep: bounce decides how a die settles, and a felt floor with wooden walls is a third system neither pure result covers
    [GlobalClass]
    public partial class TraySkin : Resource
    {
        // where skins live, looked up by bare name; the surfaces they're built from sit one level down so this folder lists as whole trays
        public const string Folder = "res://Tray/skins/";

        // a key (gear.tray_gamblers.name), not display text; it's in game.csv and the locale audit's business even though nothing shows it yet
        [Export] public string NameKey { get; set; } = "";

        [Export] public TraySurface Floor { get; set; }

        [Export] public TraySurface Walls { get; set; }

        // null and loud if missing: a silent fallback would be an untextured tray with the wrong physics, which looks like a render bug and measures like a real result
        public static TraySkin Load(string name)
        {
            // whitelist, not sanitisation: All() knows every legitimate name, so ask it rather than spot a bad one
            // a .tres can carry script_class, so loading an arbitrary one runs arbitrary code - closed now before a campaign can name a skin
            if (name == null || !All().Contains(name))
            {
                // developer diagnostic, not player-facing text
                GD.PushError($"tray skin: '{name}' is not a known skin - have {string.Join(", ", All())}");
                return null;
            }

            string path = $"{Folder}{name}.tres";

            var skin = GD.Load<TraySkin>(path);

            // developer diagnostic, not player-facing text
            if (skin == null) GD.PushError($"tray skin: {path} did not load");

            return skin;
        }

        // every skin by bare name, sorted; the folder is the list, so adding a tray is dropping a .tres in
        // two spellings, like ImpactPool: x.tres in source, x.tres.remap in an export
        public static SortedSet<string> All()
        {
            var names = new SortedSet<string>();

            using DirAccess dir = DirAccess.Open(Folder);

            if (dir == null)
            {
                GD.PushError($"tray skin: cannot open {Folder} - {DirAccess.GetOpenError()}");
                return names;
            }

            foreach (string entry in dir.GetFiles())
            {
                string name = entry;

                if (name.EndsWith(".remap")) name = name.GetBaseName();

                if (name.EndsWith(".tres")) names.Add(name.GetBaseName());
            }

            return names;
        }
    }
}
