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
