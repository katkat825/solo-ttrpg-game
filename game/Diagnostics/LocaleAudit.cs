using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Content.Monsters;
using Core.Characters;
using Core.Localization;
using Game.Localization;

// both namespaces have FileAccess; the Godot one is aliased because it reads res:// and absolute paths alike
using GodotFile = Godot.FileAccess;

namespace Game.Diagnostics
{
    // everything printed is developer diagnostic, exempt from localization
    public partial class LocaleAudit : HeadlessCheck
    {
        protected override string Subject => "locale";

        // the source of truth; the .translation binaries beside it are build output
        [Export] public string CsvPath { get; set; } = "res://locale/game.csv";

        // a required argument, so the audit cannot silently check a placeholder roster and still pass
        public IArchetypeSource Archetypes { get; set; } = Game.Campaigns.Library.Load();

        // a campaign carries its own strings in locale/, or it is not self-contained
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

            CheckCampaigns();

            ShowSample();

            GD.Print("");
            Finish();
        }

        // the checklist is what the rules emit plus what presentation emits; missing half makes real strings read as orphans
        IReadOnlyCollection<string> Checklist()
        {
            // engine roster only: a campaign's monster must not demand its name from game.csv, so campaigns are checked separately
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

            var minis = new SortedSet<string>(GameKeys.MiniNameKeys());

            foreach (string key in minis) keys.Add(key);

            GD.Print($"minis       {string.Join(", ", minis)}");

            var kit = new SortedSet<string>(GameKeys.AbilityKeys());

            foreach (string key in kit) keys.Add(key);

            GD.Print($"kit         {string.Join(", ", Content.Kits.SharedKit.All.Select(a => a.Id))}");

            // DM lines live under combat.*, not dialogue.dm.*: the DM is the voice, the rules the author
            var dm = new SortedSet<string>(Game.Dm.DmLines.All());

            foreach (string key in dm) keys.Add(key);

            GD.Print($"dm          {string.Join(", ", dm)}");

            // the sheet's printed labels and the room's objects (Phase R)
            var room = new SortedSet<string>(Game.Sheet.SheetKeys.All());

            foreach (string key in room) keys.Add(key);

            GD.Print($"room        {string.Join(", ", Game.Room.Props.Words)}");
            GD.Print("");

            return keys;
        }

        // Godot loads only translations listed in Project Settings; forgetting that makes a perfect CSV do nothing
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

        // the base game's own rosters, which keep un-prefixed ids
        IArchetypeSource EngineRoster()
        {
            if (Archetypes is not Game.Campaigns.Library library) return Archetypes;

            var engine = new Rosters();

            // asked of the roster, not its concrete type: a type test broke the moment a second roster kind existed
            foreach (IArchetypeSource source in library.Archetypes.Sources)
                if (source is not Content.Campaigns.IPackRoster pack || pack.Pack.Length == 0)
                    engine.Add(source);

            return engine;
        }

        // each campaign checked against its own locale/; a campaign with no monsters needs none
        void CheckCampaigns()
        {
            if (Archetypes is not Game.Campaigns.Library library) return;

            // Phase W. A beat has to be deliverable by ANY companion the player might turn up
            // with, so the spine is checked against every voice on the shelf and not just the ones
            // this campaign happens to ship. A campaign that writes for somebody else's companion
            // is exactly the case this exists for.
            string[] voices = library.Voices.ToArray();

            GD.Print("");
            GD.Print($"voices      {(voices.Length == 0 ? "none on this shelf" : string.Join(", ", voices))}");

            foreach (Game.Campaigns.Loaded campaign in library.Campaigns)
            {
                var expected = new SortedSet<string>(campaign.Keys(voices));

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

                CheckCoverage(expected, english, files[0], Label(campaign.Id, files),
                              new HashSet<string>(campaign.Spare(voices)));
                CheckGrammar(english);
                CheckPlaceholders(english, library);

                // prove the strings came from the folder by asking the translation server, not the CSV
                CheckTheUnloadPath(campaign, expected);
            }
        }

        // every CSV the campaign ships, not just the first: strings may be split across files
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

        // a key in two of a campaign's CSVs is a problem: one wins silently and the other translation is never seen
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

        // exercise the unload path (it fails if never run) and put the campaign back, since later checks need it
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

        // both directions: a key with no text breaks the screen, text with no key is dead weight
        // spare: keys the file is allowed to carry and is never asked for - the shared spine,
        // where what a campaign owes is "every player can be told this" rather than "every key"
        void CheckCoverage(IReadOnlyCollection<string> expected, Dictionary<string, string> english,
                           string file, string label = null, ISet<string> spare = null)
        {
            var have = new HashSet<string>(english.Keys);
            string named = label ?? Named(file);

            var missing = expected.Where(k => !have.Contains(k)).ToList();
            var orphans = english.Keys
                                 .Where(k => !expected.Contains(k) && spare?.Contains(k) != true)
                                 .ToList();

            GD.Print($"keys        the game emits {expected.Count}, {named} has {english.Count}");

            foreach (string key in missing)
                Problem($"{key} has no English in {named}. It is emitted and the file doesn't cover it.");

            foreach (string key in orphans)
                Problem($"{key} is in {named} and nothing emits it. Either it's a typo or the code that used it is gone.");

            if (missing.Count == 0 && orphans.Count == 0)
                GD.Print("            every key the game emits has English, and nothing is spare");
        }

        // the CSV is hand-edited, the one place a key can be mistyped rather than built
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

        // a name_numbered key missing {0} silently numbers nothing; a stray {0} prints braces on screen
        void CheckPlaceholders(Dictionary<string, string> english,
                               Game.Campaigns.Library shelf = null)
        {
            foreach (KeyValuePair<string, string> row in english)
            {
                // engine keys, DM lines and a companion's readout all format numbers in: any of
                // them losing its {0} would read as a deliberate translation choice
                bool needsOne = EngineKeys.TakesAnArgument(row.Key) ||
                                Game.Dm.DmLines.TakesAnArgument(row.Key) ||
                                Game.Sheet.SheetKeys.TakesAnArgument(row.Key) ||
                                Reads(shelf, row.Key);
                bool hasOne = row.Value.Contains("{0}");

                if (needsOne && !hasOne)
                    Problem($"{row.Key} is formatted with a number but its text has no {{0}}: \"{row.Value}\"");

                if (!needsOne && hasOne)
                    Problem($"{row.Key} contains a {{0}} but nothing formats it, so the braces will show: \"{row.Value}\"");
            }
        }

        // a readout key belonging to any voice on the shelf; the campaign that ships the bank and
        // the campaign that ships the line need not be the same one
        static bool Reads(Game.Campaigns.Library shelf, string key)
        {
            if (shelf == null) return false;

            foreach (Game.Campaigns.Loaded campaign in shelf.InPlay)
                if (campaign.Barks.TakesAnArgument(key)) return true;

            return false;
        }

        // shows the pseudolocale on a few strings, so it is obvious it is wired and what hardcoded English looks like
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

            // sample a campaign string too: engine keys prove the pseudolocale is on but nothing about folder-loaded strings
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

            // Has() must stay truthful with the pseudolocale on, or the gap-finder reports no gaps
            if (!loc.Has(Attr.Might.Key()))
                Problem("GodotLocalizer.Has says a key that exists is missing, with the pseudolocale on");

            if (loc.Has("ui.no_such_key.title"))
                Problem("GodotLocalizer.Has says a key that doesn't exist is present, with the pseudolocale on");

            TranslationServer.PseudolocalizationEnabled = was;
        }

        IEnumerable<string> FromACampaign()
        {
            if (Archetypes is not Game.Campaigns.Library library) yield break;

            bool shown = false;

            foreach (Game.Campaigns.Loaded campaign in library.Campaigns)
            {
                if (campaign.Failed) continue;

                if (!shown)
                {
                    foreach (string key in campaign.Keys().Take(2)) yield return key;

                    shown = true;
                }

                // a class needs its own pass: a class pack is not a campaign, so the loop above proves nothing about the class namespace
                foreach (Content.Classes.ClassCard card in campaign.Classes.All)
                {
                    yield return card.NameKey;
                    yield break;
                }
            }
        }

        // validate the pseudolocale config: a section-relative setting name silently writes one nobody reads
        void CheckPseudolocaleIsConfigured(ILocalizer loc)
        {
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

            double ratio = (double)ProjectSettings.GetSetting("internationalization/pseudolocalization/expansion_ratio", 0.0);

            if (ratio <= 0.0)
                Problem("expansion_ratio is 0, so the pseudolocale never reveals a layout that can't take longer text");
            else if (pseudo.Length <= plain.Length + prefix.Length + suffix.Length)
                Problem($"expansion_ratio is {ratio} but nothing was padded: \"{plain}\" -> \"{pseudo}\"");
        }

        // name a campaign file by its folder, not the full disk path
        static string Named(string file)
        {
            if (file.StartsWith("res://")) return file;

            string folder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(file)) ?? "");

            return folder.Length > 0 ? $"{folder}/{LocaleFolder}/{Path.GetFileName(file)}" : file;
        }

        static string Clip(string s, int width) =>
            (s.Length > width ? s.Substring(0, width - 1) + "…" : s).PadRight(width);

        // read the source CSV, not the imported .translation: the import cannot be enumerated
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
