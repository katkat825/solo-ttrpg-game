using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Characters;
using Core.Localization;

// checks the locale file against the keys the engine actually emits
// a missing string does not crash and does not look broken - it puts skill.larceny.name
// on the screen and waits for someone to open a character sheet and notice
// run it with check-locale.ps1: exit 0 if the locale is complete, 1 if it isn't
// everything printed is developer diagnostic, exempt from localization like Actor.DebugName

public partial class LocaleAudit : Node
{
    // the source of truth. the .translation binaries beside it are build output
    [Export] public string CsvPath { get; set; } = "res://locale/game.csv";

    [Export] public bool QuitWhenDone { get; set; } = true;

    int _problems;

    void Problem(string message)
    {
        _problems++;
        GD.Print("  FAIL  " + message);
    }

    public override void _Ready()
    {
        GD.Print($"locale audit: {CsvPath}");
        GD.Print("");

        Dictionary<string, string> english = ReadColumn(CsvPath, "en");

        if (english == null)
        {
            Finish();
            return;
        }

        var expected = EngineKeys.All().ToList();

        CheckRegistered();
        CheckCoverage(expected, english);
        CheckGrammar(english);
        CheckPlaceholders(english);

        ShowSample(expected);
        Finish();
    }

    // a perfect CSV that nobody registered is still a screen full of keys
    // Godot only loads what is listed in Project Settings, and forgetting that line is the
    // easiest way to have all of this quietly do nothing
    void CheckRegistered()
    {
        var registered = ProjectSettings.GetSetting("internationalization/locale/translations")
            .AsStringArray();

        string[] loaded = TranslationServer.GetLoadedLocales();

        GD.Print($"registered  {(registered.Length == 0 ? "NOTHING" : string.Join(", ", registered))}");
        GD.Print($"loaded      {(loaded.Length == 0 ? "NOTHING" : string.Join(", ", loaded))}");
        GD.Print($"current     {TranslationServer.GetLocale()}");
        GD.Print("");

        if (registered.Length == 0)
            Problem("no translations are registered in Project Settings, so none of this file is loaded");

        if (loaded.Length == 0)
            Problem("the translation server has no locales loaded");
    }

    // both directions, and both matter
    // a key with no text breaks the screen; text with no key is dead weight a translator is paid for
    void CheckCoverage(IReadOnlyCollection<string> expected, Dictionary<string, string> english)
    {
        var have = new HashSet<string>(english.Keys);

        var missing = expected.Where(k => !have.Contains(k)).ToList();
        var orphans = english.Keys.Where(k => !expected.Contains(k)).ToList();

        GD.Print($"keys        engine emits {expected.Count}, file has {english.Count}");

        foreach (string key in missing)
            Problem($"{key} has no English. The engine emits it and the file doesn't cover it.");

        foreach (string key in orphans)
            Problem($"{key} is in the file but nothing emits it. Either it's a typo or the code that used it is gone.");

        if (missing.Count == 0 && orphans.Count == 0)
            GD.Print("            every key the engine emits has English, and nothing is spare");
    }

    // the file is hand-edited, so it is the one place a key can be typed rather than built
    void CheckGrammar(Dictionary<string, string> english)
    {
        foreach (KeyValuePair<string, string> row in english)
        {
            if (!KeyConventions.IsWellFormed(row.Key))
                Problem($"{row.Key} breaks the key grammar: {KeyConventions.Explain(row.Key)}");

            if (string.IsNullOrWhiteSpace(row.Value))
                Problem($"{row.Key} has no text against it");
        }
    }

    // a name_numbered key without its {0} produces "Rabble" for every mook and reports nothing
    // a {0} that nobody formats prints the braces on screen
    // both are invisible until someone looks
    void CheckPlaceholders(Dictionary<string, string> english)
    {
        foreach (KeyValuePair<string, string> row in english)
        {
            bool needsOne = EngineKeys.TakesAnArgument(row.Key);
            bool hasOne = row.Value.Contains("{0}");

            if (needsOne && !hasOne)
                Problem($"{row.Key} is formatted with a number but its text has no {{0}}: \"{row.Value}\"");

            if (!needsOne && hasOne)
                Problem($"{row.Key} contains a {{0}} but nothing formats it, so the braces will show: \"{row.Value}\"");
        }
    }

    // what the pseudolocale does to a handful of strings, so it is obvious it is wired up
    // and obvious what to look for when hunting hardcoded English by eye
    void ShowSample(IReadOnlyCollection<string> keys)
    {
        var loc = new GodotLocalizer();

        string[] sample =
        {
            Attr.Might.Key(),
            Skill.Channeling.Key(),
            Condition.Winded.DescriptionKey(),
            KeyConventions.ActorNameNumbered("rabble"),
        };

        GD.Print("");
        GD.Print("sample      key                             english                    pseudolocale");

        bool was = TranslationServer.PseudolocalizationEnabled;

        foreach (string key in sample)
        {
            TranslationServer.PseudolocalizationEnabled = false;
            string plain = loc.Get(key);

            TranslationServer.PseudolocalizationEnabled = true;
            string pseudo = loc.Get(key);

            GD.Print($"            {Clip(key, 30)}  {Clip(plain, 25)}  {pseudo}");
        }

        TranslationServer.PseudolocalizationEnabled = true;

        CheckPseudolocaleIsConfigured(loc);

        // Has() has to keep telling the truth with the pseudolocale on, or the one tool for
        // finding gaps starts reporting that there are none
        if (!loc.Has(Attr.Might.Key()))
            Problem("GodotLocalizer.Has says a key that exists is missing, with the pseudolocale on");

        if (loc.Has("ui.no_such_key.title"))
            Problem("GodotLocalizer.Has says a key that doesn't exist is present, with the pseudolocale on");

        TranslationServer.PseudolocalizationEnabled = was;
    }

    // a pseudolocale that doesn't mark or pad anything is worse than none
    // it runs, it prints, it finds nothing, and it looks like a clean bill of health
    // every one of these has already been wrong once - settings inside a section are named
    // relative to it, so the full path in there writes a setting nobody reads
    void CheckPseudolocaleIsConfigured(ILocalizer loc)
    {
        // long enough that a 30% expansion is unambiguous
        // a five-letter word can round to no padding and prove nothing either way
        string sample = Condition.Winded.DescriptionKey();

        TranslationServer.PseudolocalizationEnabled = false;
        string plain = loc.Get(sample);

        TranslationServer.PseudolocalizationEnabled = true;
        string pseudo = loc.Get(sample);

        if (pseudo == plain)
        {
            Problem("the pseudolocale changes nothing, so it cannot find hardcoded English");
            return;
        }

        string prefix = (string)ProjectSettings.GetSetting("internationalization/pseudolocalization/prefix", "");
        string suffix = (string)ProjectSettings.GetSetting("internationalization/pseudolocalization/suffix", "");

        if (prefix.Length == 0 || !pseudo.StartsWith(prefix) || !pseudo.EndsWith(suffix))
            Problem($"the pseudolocale isn't bracketed, so padded text is hard to spot: \"{pseudo}\"");

        // padding is what shows you the overflow a translator would find
        double ratio = (double)ProjectSettings.GetSetting("internationalization/pseudolocalization/expansion_ratio", 0.0);

        if (ratio <= 0.0)
            Problem("expansion_ratio is 0, so the pseudolocale never reveals a layout that can't take longer text");
        else if (pseudo.Length <= plain.Length + prefix.Length + suffix.Length)
            Problem($"expansion_ratio is {ratio} but nothing was padded: \"{plain}\" -> \"{pseudo}\"");
    }

    static string Clip(string s, int width) =>
        (s.Length > width ? s.Substring(0, width - 1) + "…" : s).PadRight(width);

    // read the source CSV rather than the imported .translation
    // the imported one is a hash table that cannot be enumerated, and the CSV is the file
    // a person would go and fix
    Dictionary<string, string> ReadColumn(string path, string locale)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read);

        if (file == null)
        {
            Problem($"cannot open {path}: {FileAccess.GetOpenError()}");
            return null;
        }

        string[] header = file.GetCsvLine();
        int column = System.Array.IndexOf(header, locale);

        if (column < 1)
        {
            Problem($"{path} has no '{locale}' column. Its header is: {string.Join(", ", header)}");
            return null;
        }

        var rows = new Dictionary<string, string>();

        for (int line = 2; !file.EofReached(); line++)
        {
            string[] cells = file.GetCsvLine();

            if (cells.Length == 0 || string.IsNullOrWhiteSpace(cells[0])) continue;

            if (cells.Length <= column)
            {
                Problem($"{path} line {line}: '{cells[0]}' has no '{locale}' cell");
                continue;
            }

            if (!rows.TryAdd(cells[0], cells[column]))
                Problem($"{path} line {line}: '{cells[0]}' appears twice. The later one wins silently.");
        }

        return rows;
    }

    void Finish()
    {
        GD.Print("");
        GD.Print(_problems == 0
            ? "locale check passed"
            : $"LOCALE CHECK FAILED with {_problems} problem{(_problems == 1 ? "" : "s")}");

        if (QuitWhenDone) GetTree().Quit(_problems == 0 ? 0 : 1);
    }
}
