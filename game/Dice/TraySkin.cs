using System.Collections.Generic;
using Godot;

// a whole tray as the thing you own - a floor surface and a walls surface
// the unit of collection is the tray, not the surface, so the name key sits here
//
// EVERY SKIN NEEDS ITS OWN FAIRNESS SWEEP BEFORE IT SHIPS
// friction and bounce are allowed to differ, but bounce is what decides how a die settles,
// so a skin is only cosmetic while it still rolls uniform - and that is not automatic
// it is the COMBINATION that has to be swept: felt is fine and wood is fine,
// and felt floor with wooden walls is a third physical system neither result covers
//
//     .\check-fairness.ps1 -Tray gamblers -Dice 3
//
// a tray that quietly favours a face is a hidden mechanical difference and nothing throws
// the alternative is one shared PhysicsMaterial for every skin, which means felt bounces
// like a plank - let the physics vary, verify it

[GlobalClass]
public partial class TraySkin : Resource
{
    // where skins live, and where --tray= looks them up by bare name
    // only skins - the surfaces they are built from sit in skins/surfaces/, one level down,
    // so this folder can be listed and every answer is a whole tray, see All()
    public const string Folder = "res://Dice/skins/";

    // a KEY - gear.tray_gamblers.name, never "Gambler's Tray"
    //
    // deliberately NOT in game/locale/game.csv, and nothing displays it yet
    // the locale audit derives its checklist from rules/, so a key with no engine
    // behind it fails as an orphan - same trap M6 hit with ui.tray.impact
    [Export] public string NameKey { get; set; } = "";

    [Export] public TraySurface Floor { get; set; }

    [Export] public TraySurface Walls { get; set; }

    // wood loads res://Dice/skins/wood.tres
    // null and loud if it isn't there - the tray falls back to whatever the scene was saved
    // with, which is still playable, but silent would be worse: an untextured tray with the
    // wrong physics looks like a rendering bug and measures like a real result
    public static TraySkin Load(string name)
    {
        // whitelist, not sanitisation: All() already knows every legitimate name, so ask it
        // rather than trying to spot a bad one
        //
        // today the only caller is our own command line, so this is close to theatre - but
        // ARCHITECTURE 4 has campaigns as droppable folders and modding as a deliberate
        // outcome, and a .tres can carry script_class, which means loading an arbitrary one
        // is running arbitrary code. the moment a campaign can name a skin, an unchecked
        // path here stops being theatre. cheaper to close now than to remember later
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

    // every skin there is, by bare name, sorted
    // the folder is the list, exactly as for ImpactPool - adding a tray is dropping
    // a .tres in and sweeping it, never editing an array
    // two spellings, same as ImpactPool.Files: x.tres in source, x.tres.remap in an export
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
