using Godot;
using Core.Localization;

namespace Game.Localization
{
    // the only place a key turns into text
    public sealed class GodotLocalizer : ILocalizer
    {
        public string Get(string key) => TranslationServer.Translate(key);

        public string Format(string key, params object[] args)
        {
            string pattern = TranslationServer.Translate(key);
            if (args == null || args.Length == 0) return pattern;

            return string.Format(pattern, args);
        }

        // Godot echoes the key back on a miss; but the pseudolocale mangles even that echo, so compare against what a miss actually looks like now, not against the key
        public bool Has(string key) => TranslationServer.Translate(key) != Missing(key);

        // what Godot hands back for a key it doesn't have, under the current settings
        static string Missing(string key) =>
            TranslationServer.PseudolocalizationEnabled
                ? TranslationServer.Pseudolocalize(key)
                : key;
    }
}
