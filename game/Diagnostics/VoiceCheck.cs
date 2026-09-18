using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Game.Localization;

using GodotFile = Godot.FileAccess;

namespace Game.Diagnostics
{
    // THE BASE GAME ADDRESSES YOU, AND NEVER GIVES YOU A GENDER.
    //
    // It is a standing rule of this project, and it was the one standing rule with nothing
    // holding it. The content was already compliant when this was written, which is exactly why it
    // is worth a check: a rule that is true by habit stays true until the day somebody writes forty
    // barks in an afternoon.
    //
    // WHAT IT IS HELD OVER. The engine's own strings, and the campaigns this game SHIPS - named in
    // the scene rather than discovered, because a subscribed Workshop campaign is somebody else's
    // writing and is not held to this. That list is the whole of the Workshop exemption, and it is
    // one line to add a sixth.
    //
    // The scoping - which keys may say "he" and which may not - is SecondPerson's, and is Godot-free
    // so it can be argued with from a test rather than from a headless run.
    public partial class VoiceCheck : HeadlessCheck
    {
        protected override string Subject => "voice";

        [Export] public string CsvPath { get; set; } = "res://locale/game.csv";

        // the campaigns and packs this game ships. Anything else on the shelf is somebody else's
        [Export] public Godot.Collections.Array<string> BaseGame { get; set; } =
            new Godot.Collections.Array<string> { "ashfall", "greyhollow", "saltmarch" };

        public const string LocaleFolder = "locale";

        int _read;

        int _excused;

        public override void _Ready()
        {
            GD.Print("voice check: the base game speaks to you in the second person");
            GD.Print("");
            GD.Print("english     this reads the 'en' column only - the word lists are English, and");
            GD.Print("            a locale that has gone wrong in another language needs a reader");
            GD.Print("            of that language, not a longer pattern");
            GD.Print("");

            Hold("the engine", CsvPath,
                 new HashSet<string>(Allowed(Path.GetDirectoryName(CsvPath) ?? ""),
                                     System.StringComparer.Ordinal));

            foreach (string campaign in BaseGame)
            {
                IReadOnlyList<string> files = LocaleOf(campaign);

                if (files.Count == 0)
                {
                    GD.Print($"  {campaign} - no {LocaleFolder}/, nothing to read");
                    continue;
                }

                var excused = new HashSet<string>(
                    files.SelectMany(f => Allowed(Path.GetDirectoryName(f) ?? "")),
                    System.StringComparer.Ordinal);

                foreach (string file in files) Hold(campaign, file, excused);
            }

            GD.Print("");
            GD.Print($"read        {_read} line(s) of English across the base game" +
                     (_excused > 0 ? $", {_excused} excused by {SecondPerson.AllowFile}" : ""));

            GD.Print("");
            Finish();
        }

        void Hold(string whose, string path, ISet<string> excused)
        {
            Dictionary<string, string> english = ReadColumn(path, "en");

            if (english == null) return;

            int caught = 0;

            foreach (KeyValuePair<string, string> row in english)
            {
                _read++;

                IReadOnlyList<string> faults = SecondPerson.Faults(row.Key, row.Value);

                if (faults.Count == 0) continue;

                if (excused.Contains(row.Key)) { _excused++; continue; }

                caught++;

                Problem($"{row.Key}: {SecondPerson.Explain(row.Key, faults[0])} - \"{row.Value}\"");
            }

            GD.Print($"  {whose,-12} {Named(path),-28} {english.Count,4} line(s)" +
                     (caught == 0 ? ", all of them second person" : $", {caught} to fix"));
        }

        // an author's own list of keys where a gendered term of address is aimed at somebody in the
        // fiction rather than at the player. Absent is the normal case
        IReadOnlyList<string> Allowed(string folder)
        {
            string path = folder.Length == 0
                ? SecondPerson.AllowFile
                : folder.TrimEnd('/', '\\') + "/" + SecondPerson.AllowFile;

            if (!GodotFile.FileExists(path)) return System.Array.Empty<string>();

            using GodotFile file = GodotFile.Open(path, GodotFile.ModeFlags.Read);

            if (file == null)
            {
                Problem($"cannot open {path}: {GodotFile.GetOpenError()}");
                return System.Array.Empty<string>();
            }

            var keys = new List<string>();

            while (!file.EofReached())
            {
                string line = file.GetLine().Trim();

                if (line.Length == 0 || line.StartsWith('#')) continue;

                keys.Add(line);
            }

            return keys;
        }

        IReadOnlyList<string> LocaleOf(string campaign)
        {
            foreach (string root in Game.Campaigns.CampaignFolders.Roots())
            {
                string folder = Path.Combine(root, campaign, LocaleFolder);

                if (!Directory.Exists(folder)) continue;

                return Directory.GetFiles(folder, "*.csv").OrderBy(f => f).ToArray();
            }

            return System.Array.Empty<string>();
        }

        static string Named(string path) => path.Substring(path.LastIndexOfAny(new[] { '/', '\\' }) + 1);

        // the same shape LocaleAudit reads a CSV in; kept apart rather than shared because this
        // check must keep running when the audit is not, and neither owns the other's reporting
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

            while (!file.EofReached())
            {
                string[] cells = file.GetCsvLine();

                if (cells.Length == 0 || string.IsNullOrWhiteSpace(cells[0])) continue;
                if (cells.Length <= column) continue;

                rows[cells[0]] = cells[column];
            }

            return rows;
        }
    }
}
