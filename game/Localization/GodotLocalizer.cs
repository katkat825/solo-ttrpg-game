using Godot;
using Core.Localization;

// the ONLY place a key turns into text
// forwards to Godot's translation system, so we inherit CSV/PO import, locale fallback
// and live locale switching
// resolving a key anywhere inside core/ means something has gone wrong

public sealed class GodotLocalizer : ILocalizer
{
    public string Get(string key) => TranslationServer.Translate(key);

    public string Format(string key, params object[] args)
    {
        string pattern = TranslationServer.Translate(key);
        if (args == null || args.Length == 0) return pattern;

        return string.Format(pattern, args);
    }

    // Godot echoes the key back when a translation is missing, so a miss is loud
    // the pseudolocale complicates that quietly: it mangles whatever Translate returns
    // INCLUDING the echoed key, so "I got something other than the key back" becomes true for
    // every key in existence and this starts insisting the locale is complete
    // so compare against what a miss would actually look like right now
    public bool Has(string key) => TranslationServer.Translate(key) != Missing(key);

    // what Godot hands back for a key it doesn't have, under the current settings
    static string Missing(string key) =>
        TranslationServer.PseudolocalizationEnabled
            ? TranslationServer.Pseudolocalize(key)
            : key;
}
