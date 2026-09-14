using System.Collections.Generic;
using Content.Minis;
using Game.Tray;

namespace Game.Localization
{
    // every localization key the PRESENTATION layer can put in front of a player, as opposed to
    // Core.Characters.EngineKeys, which covers the ones the rules emit. together they are the
    // whole checklist game/locale/game.csv is held to
    //
    // DERIVED, NEVER LISTED, same rule as EngineKeys - a hand-written list here would be a second
    // description of the skins folder, free to drift from it
    //
    // this exists because the checklist was one-sided and quietly wrong (F4). game.csv carried
    // gear.tray_wood.name and gear.tray_gamblers.name, TraySkin emitted them from two .tres files,
    // and the audit derived its expectations from core/ alone - so it reported both as orphans and
    // check-locale.ps1 failed for a reason that had nothing to do with anything being missing.
    // the audit was describing something other than the game that loads
    //
    // it cannot live in game.tests: TraySkin.All() lists a folder and Load() reads a Resource, so
    // this needs an engine behind it. the locale audit is what exercises it, headless, from
    // check-locale.ps1 - which is the same instrument that would catch a mistake here anyway
    public static class GameKeys
    {
        public static IEnumerable<string> All()
        {
            foreach (KeyValuePair<string, string> skin in TrayNameKeys())
                if (!string.IsNullOrWhiteSpace(skin.Value))
                    yield return skin.Value;

            foreach (string key in MiniNameKeys()) yield return key;
        }

        // THE FIGURES THE BASE GAME SHIPS (MINIS_AND_ART.md A1). A mini is a thing a player picks
        // and sees listed, so it has a name, so the name is a key - `mini.rabble.name`, derived
        // from the id exactly as `actor.rabble.name` is (`MiniManifest`).
        //
        // THE SHARED ROSTER'S ONLY, AND THAT IS THE SAME SPLIT `EngineKeys` MAKES about actors: a
        // PACK's minis are the pack's to name, in the pack's own locale/ folder, and demanding
        // them of game.csv would be the one thing CONVENTIONS.md section 7 forbids of that file.
        // `Game.Campaigns.Loaded.Keys` is the other half, per pack
        //
        // DERIVED FROM `SharedMinis`, NEVER LISTED - the same rule that made the tray skins a
        // folder listing rather than a hand-written array, and for the same reason F4 wrote down:
        // a checklist that covers less than the game is as wrong as one that covers a different
        // game
        public static IEnumerable<string> MiniNameKeys()
        {
            foreach (MiniManifest mini in SharedMinis.All) yield return mini.NameKey;
        }

        // skin bare name -> the key it names itself with, for every skin in the folder
        //
        // the empty ones come back too rather than being dropped, because a skin with no NameKey
        // is a thing to report and not a thing to skip past - see LocaleAudit
        public static SortedDictionary<string, string> TrayNameKeys()
        {
            var keys = new SortedDictionary<string, string>();

            foreach (string name in TraySkin.All())
            {
                TraySkin skin = TraySkin.Load(name);

                // Load already pushed an error saying which file and why
                keys[name] = skin?.NameKey ?? "";
            }

            return keys;
        }
    }
}
