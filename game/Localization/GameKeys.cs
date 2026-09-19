using System.Collections.Generic;
using Content.Minis;
using Game.Tray;

namespace Game.Localization
{
    public static class GameKeys
    {
        public static IEnumerable<string> All()
        {
            foreach (KeyValuePair<string, string> skin in TrayNameKeys())
                if (!string.IsNullOrWhiteSpace(skin.Value))
                    yield return skin.Value;

            foreach (string key in MiniNameKeys()) yield return key;

            foreach (string key in AbilityKeys()) yield return key;

            foreach (string key in Game.Dm.DmLines.All()) yield return key;

            // Phase R. The words printed on the sheet and the names of the objects in the room -
            // the engine's, because every campaign is played on the same sheet in the same room
            foreach (string key in Game.Sheet.SheetKeys.All()) yield return key;

            // and the words printed on an initiative card, for the same reason
            foreach (string key in Game.Fight.CardKeys.All()) yield return key;

            // V2. The turn itself: the marker you nudge to end yours, the note asking whether you
            // push on, and what a Nerve buys this instant, pencilled on the sheet
            foreach (string key in Game.Fight.TurnKeys.All()) yield return key;

            // Phase T. The words on a card the moment lays out, the names of the objects ON the
            // table as against the ones in the room, and the three checks you reach for yourself
            foreach (string key in Game.Explore.VerbKeys.All()) yield return key;

            foreach (string key in Game.Room.TableProps.Keys()) yield return key;

            foreach (string key in Content.Sheet.Checks.Keys()) yield return key;

            // Phase BK. The two books - their contents pages, the rules chapters, the settings
            // headings - the bookcase they stand on, the blank objects that are the Workshop door,
            // and how a line of the story so far reads
            foreach (string key in Game.Book.BookKeys.All()) yield return key;

            // Phase AX. What every key on the keyboard does, said in words on the page where you
            // rebind it; what the sounds are, for a player who cannot hear them; and the answer to
            // "where are we", which is the same sentences whether it is read out or written down
            foreach (string key in Game.Access.Acts.Keys()) yield return key;

            foreach (string key in Game.Audio.Sounds.Keys()) yield return key;

            foreach (string key in Game.Access.Whereabouts.Keys()) yield return key;
        }

        // the base game's shared abilities only; a pack's abilities are named in the pack's own locale
        public static IEnumerable<string> AbilityKeys()
        {
            foreach (Content.Kits.Ability ability in Content.Kits.SharedKit.All)
                foreach (string key in ability.Keys())
                    yield return key;
        }

        // the shared roster's minis only; a pack's minis are named in the pack's own locale
        public static IEnumerable<string> MiniNameKeys()
        {
            foreach (MiniManifest mini in SharedMinis.All) yield return mini.NameKey;
        }

        // empty entries come back too: a skin with no NameKey is a thing to report, not to skip
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
