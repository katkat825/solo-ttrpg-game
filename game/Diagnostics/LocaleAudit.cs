using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Content.Monsters;
using Core.Characters;
using Core.Localization;
using Game.Localization;

// System.IO and Godot both have a FileAccess, and this file now needs Directory and Path from one
// and the reader from the other. Aliasing the Godot one is the smaller change: it reads res:// as
// well as an absolute path, which is exactly what is needed to open game.csv inside a .pck AND a
// campaign's CSV out on a disk with one method
using GodotFile = Godot.FileAccess;

namespace Game.Diagnostics
{
    // checks the locale file against the keys the engine actually emits
    // a missing string does not crash and does not look broken - it puts skill.larceny.name
    // on the screen and waits for someone to open a character sheet and notice
    // run it with check-locale.ps1: exit 0 if the locale is complete, 1 if it isn't
    // everything printed is developer diagnostic, exempt from localization like Actor.DebugName
    public partial class LocaleAudit : HeadlessCheck
    {
        protected override string Subject => "locale";

        // the source of truth. the .translation binaries beside it are build output
        [Export] public string CsvPath { get; set; } = "res://locale/game.csv";

        // THE ROSTER THE CHECKLIST IS TAKEN FROM, named here and printed in the report (F4)
        //
        // EngineKeys.All used to default to BuiltInArchetypes when handed nothing, so this file
        // could audit a placeholder roster while a campaign was loaded and still say "passed".
        // it is a required argument now, which puts the choice at the composition root where it
        // belongs - this line, and Main.cs's. when statblocks become campaign data (Phase P) this
        // is the one line that changes, and the report says out loud what it checked so a stale
        // answer is visible rather than assumed
        public IArchetypeSource Archetypes { get; set; } = Game.Campaigns.Library.Load();

        // WHERE A CAMPAIGN'S OWN STRINGS LIVE (ARCHITECTURE.md section 4). One CSV per language
        // set, inside the campaign folder, because a campaign that names its own monsters has to
        // carry their names with it or it is not self-contained
        public const string LocaleFolder = "locale";

        public override void _Ready()
        {
            GD.Print($"locale audit: {CsvPath}");
            GD.Print("");

            Dictionary<string, string> english = ReadColumn(CsvPath, "en");

            if (english == null)
            {
                GD.Print("");
                Finish();
                return;
            }

            var expected = Checklist();

            CheckRegistered();
            CheckCoverage(expected, english, CsvPath);
            CheckGrammar(english);
            CheckPlaceholders(english);

            // and every loaded campaign against its own locale, which is a different file with a
            // different owner and exactly the same two questions
            CheckCampaigns();

            ShowSample();

            GD.Print("");
            Finish();
        }

        // the whole checklist: what the rules emit, plus what the presentation layer emits
        //
        // it was the first half alone until F4, which is why the two tray skins' names read as
        // orphans - the file had them, the game emitted them, and the checklist had never heard
        // of either. a checklist that covers less than the game is the same failure as one that
        // covers a different game, just from the other side
        IReadOnlyCollection<string> Checklist()
        {
            // THE ENGINE'S OWN ROSTER, AND NOT THE CAMPAIGNS' (P0). `EngineKeys` says what it is
            // for on its own first line - "every key the ENGINE itself can put in front of a
            // player... campaigns ship their own strings" - and handing it a roster with
            // `ashfall.ghoul` in it would demand that ghoul's name from game/locale/game.csv,
            // which is the one file CONVENTIONS.md section 7 says must never grow a campaign's
            // keys. So the loaded rosters are split: the engine's go here, and each campaign's go
            // to that campaign's own file in CheckCampaigns below
            IArchetypeSource engine = EngineRoster();

            var keys = new SortedSet<string>(EngineKeys.All(engine));

            GD.Print($"roster      {engine.GetType().Name}, " +
                     $"{engine.Ids.Count} archetypes: {string.Join(", ", engine.Ids)}");

            SortedDictionary<string, string> skins = GameKeys.TrayNameKeys();

            foreach (KeyValuePair<string, string> skin in skins)
            {
                if (string.IsNullOrWhiteSpace(skin.Value))
                {
                    Problem($"tray skin '{skin.Key}' has no NameKey, so it can never be named on screen");
                    continue;
                }

                keys.Add(skin.Value);
            }

            GD.Print($"tray skins  {string.Join(", ", skins.Keys)}");

            // AND THE FIGURES THE BASE GAME SHIPS (Phase A). Added here for exactly the reason F4
            // added the tray skins: the game emits them, game.csv carries them, and a checklist
            // that has never heard of them calls both an orphan
            var minis = new SortedSet<string>(GameKeys.MiniNameKeys());

            foreach (string key in minis) keys.Add(key);

            GD.Print($"minis       {string.Join(", ", minis)}");
            GD.Print("");

            return keys;
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

        // the rosters that are nobody's campaign - the shared vocabulary that ships in the base
        // game and keeps its un-prefixed ids (CONVENTIONS.md section 7)
        IArchetypeSource EngineRoster()
        {
            if (Archetypes is not Game.Campaigns.Library library) return Archetypes;

            var engine = new Rosters();

            foreach (IArchetypeSource source in library.Archetypes.Sources)
                if (source is not JsonArchetypeSource campaign || campaign.Campaign.Length == 0)
                    engine.Add(source);

            return engine;
        }

        // EVERY LOADED CAMPAIGN AGAINST ITS OWN `locale/`, both directions, exactly as the engine's
        // is checked against game.csv. This is the half of the checklist that could not exist
        // before statblocks were data: `EngineKeys` lives in core/ and cannot see a folder, and
        // `LocaleAudit` runs inside Godot and can (CONTENT_PIPELINE.md P5).
        //
        // A campaign with no monsters of its own needs no locale and is not asked for one
        void CheckCampaigns()
        {
            if (Archetypes is not Game.Campaigns.Library library) return;

            foreach (Game.Campaigns.Loaded campaign in library.Campaigns)
            {
                var expected = new SortedSet<string>(campaign.Keys());

                GD.Print("");
                GD.Print($"campaign    {campaign}");

                if (expected.Count == 0) continue;

                IReadOnlyList<string> files = LocaleOf(campaign.Id);

                if (files.Count == 0)
                {
                    Problem($"campaign '{campaign.Id}' names {expected.Count} things and has no " +
                            $"{LocaleFolder}/ - none of them can be shown in any language");
                    continue;
                }

                Dictionary<string, string> english = Merged(files, campaign.Id);

                if (english == null) continue;

                CheckCoverage(expected, english, files[0], Label(campaign.Id, files));
                CheckGrammar(english);
                CheckPlaceholders(english);

                // AND THAT THE STRINGS REALLY CAME FROM THAT FOLDER (P5). Everything above reads
                // the CSV; this asks the translation server, which is the thing the table actually
                // draws from - and the way to prove a string came from a campaign is to take the
                // campaign away and watch the string go with it
                CheckTheUnloadPath(campaign, expected);
            }
        }

        // EVERY CSV THE CAMPAIGN SHIPS, not the first one. "One CSV per language set" means one
        // FILE holds a set of languages as columns - it does not mean a campaign may only have
        // one. Splitting a big campaign's strings into `monsters.csv` and `quests.csv` is an
        // ordinary thing to want, `CampaignLocale` already registers all of them, and an audit
        // that read only the first would report every key in the second as missing
        IReadOnlyList<string> LocaleOf(string campaign)
        {
            foreach (string root in Game.Campaigns.CampaignFolders.Roots())
            {
                string folder = Path.Combine(root, campaign, LocaleFolder);

                if (!Directory.Exists(folder)) continue;

                string[] files = Directory.GetFiles(folder, "*.csv");

                if (files.Length == 0) continue;

                System.Array.Sort(files, System.StringComparer.Ordinal);

                return files;
            }

            return System.Array.Empty<string>();
        }

        // them all as one table. A key in two of a campaign's own files is a problem worth its own
        // sentence: one of them wins by file name, silently, and the loser is a translation
        // somebody wrote and nobody will ever see
        Dictionary<string, string> Merged(IReadOnlyList<string> files, string campaign)
        {
            var english = new Dictionary<string, string>();

            foreach (string file in files)
            {
                Dictionary<string, string> rows = ReadColumn(file, "en");

                if (rows == null) continue;

                foreach (KeyValuePair<string, string> row in rows)
                    if (!english.TryAdd(row.Key, row.Value))
                        Problem($"{row.Key} is in two of {campaign}'s CSVs, and {Named(file)} is " +
                                "not the one that wins - whichever sorts first does, silently");
            }

            return english;
        }

        static string Label(string campaign, IReadOnlyList<string> files) =>
            files.Count == 1
                ? Named(files[0])
                : $"{campaign}/{LocaleFolder}/ ({files.Count} files)";

        // TAKE THE CAMPAIGN AWAY AND ITS STRINGS GO WITH IT. `CONTENT_PIPELINE.md` P5 asks the
        // loader to unregister a campaign's CSVs when it unloads, and an unload path that is never
        // exercised is an unload path that does not work - it would not be noticed until somebody
        // unsubscribed from a Workshop item and kept reading its monsters' names.
        //
        // Put back afterwards, always, because everything after this line in the run still expects
        // the campaign to be loaded
        void CheckTheUnloadPath(Game.Campaigns.Loaded campaign, IReadOnlyCollection<string> expected)
        {
            string key = expected.FirstOrDefault();

            if (key == null) return;

            var loc = new GodotLocalizer();

            if (!loc.Has(key))
            {
                Problem($"{key} is in {campaign.Id}'s locale and the translation server does not " +
                        "have it - the campaign's CSVs were never registered");
                return;
            }

            if (!Game.Campaigns.CampaignLocale.Unregister(campaign.Folder))
            {
                Problem($"campaign '{campaign.Id}' brought {campaign.Strings} strings and cannot " +
                        "be unloaded - nothing is keeping track of what it handed over");
                return;
            }

            bool gone = !loc.Has(key);

            Game.Campaigns.CampaignLocale.Register(campaign.Folder);

            if (!gone)
                Problem($"campaign '{campaign.Id}' was unloaded and {key} still resolves - its " +
                        "strings outlive it, so two campaigns could not both be unloaded safely");
            else if (!loc.Has(key))
                Problem($"campaign '{campaign.Id}' was unloaded and could not be loaded again - " +
                        $"{key} is gone for the rest of this run");
            else
                GD.Print($"unload      took {campaign.Id}'s strings away and put them back; " +
                         $"{key} went with it and came back");
        }

        // both directions, and both matter
        // a key with no text breaks the screen; text with no key is dead weight a translator is paid for
        // "the game" is the rules and the presentation layer together - see Checklist()
        //
        // <paramref name="label"/> is how the file is named in the report, for the campaign case
        // where there may be several of them
        void CheckCoverage(IReadOnlyCollection<string> expected, Dictionary<string, string> english,
                           string file, string label = null)
        {
            var have = new HashSet<string>(english.Keys);
            string named = label ?? Named(file);

            var missing = expected.Where(k => !have.Contains(k)).ToList();
            var orphans = english.Keys.Where(k => !expected.Contains(k)).ToList();

            GD.Print($"keys        the game emits {expected.Count}, {named} has {english.Count}");

            foreach (string key in missing)
                Problem($"{key} has no English in {named}. It is emitted and the file doesn't cover it.");

            foreach (string key in orphans)
                Problem($"{key} is in {named} and nothing emits it. Either it's a typo or the code that used it is gone.");

            if (missing.Count == 0 && orphans.Count == 0)
                GD.Print("            every key the game emits has English, and nothing is spare");
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
        void ShowSample()
        {
            var loc = new GodotLocalizer();

            var sample = new List<string>
            {
                Attr.Might.Key(),
                Skill.Channeling.Key(),
                Condition.Winded.DescriptionKey(),
                KeyConventions.ActorNameNumbered("rabble"),
            };

            // AND ONE OF A CAMPAIGN'S OWN (P5). "Switch to the pseudolocale and every campaign
            // string mangles (nothing hardcoded)" - a sample of engine keys proves the
            // pseudolocale is on, and proves nothing at all about the strings that arrived at
            // runtime out of a folder, which are exactly the ones a new loader could have got
            // wrong
            foreach (string key in FromACampaign()) sample.Add(key);

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

        // the first campaign's own name and the first thing it names, when there is a campaign
        // loaded. Two rather than one because they come from different halves of the checklist -
        // a title out of `campaign.json`'s derived keys and a monster out of its roster
        IEnumerable<string> FromACampaign()
        {
            if (Archetypes is not Game.Campaigns.Library library) yield break;

            foreach (Game.Campaigns.Loaded campaign in library.Campaigns)
            {
                if (campaign.Failed) continue;

                foreach (string key in campaign.Keys().Take(2)) yield return key;

                yield break;
            }
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

        // the file as a person would name it, which for a campaign is the folder it is in rather
        // than the whole path to somebody's disk
        static string Named(string file)
        {
            if (file.StartsWith("res://")) return file;

            string folder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file)) ?? "");

            return folder.Length > 0 ? $"{folder}/{LocaleFolder}/{Path.GetFileName(file)}" : file;
        }

        static string Clip(string s, int width) =>
            (s.Length > width ? s.Substring(0, width - 1) + "…" : s).PadRight(width);

        // read the source CSV rather than the imported .translation
        // the imported one is a hash table that cannot be enumerated, and the CSV is the file
        // a person would go and fix
        Dictionary<string, string> ReadColumn(string path, string locale)
        {
            using GodotFile file = GodotFile.Open(path, GodotFile.ModeFlags.Read);

            if (file == null)
            {
                Problem($"cannot open {path}: {GodotFile.GetOpenError()}");
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
    }
}
