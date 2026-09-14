using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

// System.IO and Godot both have a FileAccess and this file needs Directory and Path from one and
// the CSV reader from the other - see LocaleAudit for the same two lines and the same reason
using GodotFile = Godot.FileAccess;

namespace Game.Campaigns
{
    // A CAMPAIGN'S OWN STRINGS, LOADED WITH IT AND UNLOADED WITH IT (ARCHITECTURE.md section 4,
    // CONTENT_PIPELINE.md P5).
    //
    // "A campaign ships its own `locale/` because it names its own monsters, quests and speakers.
    // Those strings have to travel with the folder or the campaign isn't self-contained." So a
    // ghoul's name is in the ghoul's campaign, and `game/locale/game.csv` never grows a key that
    // belongs to somebody's campaign.
    //
    // ONE CSV PER LANGUAGE SET, same format as the engine's: a `keys` column and one column per
    // locale. A campaign may ship several - `monsters.csv` and `quests.csv` beside each other is a
    // perfectly good way to keep a big one readable - and every one of them is read. Each is handed
    // to `TranslationServer` as an ordinary `Translation`, so `GodotLocalizer` keeps being the ONE
    // place a key becomes text and nothing downstream learns that some strings came from a folder.
    //
    // WHY THIS IS NOT IMPORTED THE WAY game.csv IS. Godot imports `res://locale/game.csv` at build
    // time into a `.translation` binary. A campaign is not in `res://` - it is a folder on a disk
    // that may have arrived from Steam ten minutes ago - so there is nothing to import it and it is
    // read at runtime instead. That is also why it must be read with Godot's own CSV reader: the
    // format has to stay identical to the engine's, or an author would have two CSV dialects to
    // learn.
    //
    // REGISTERED BY CAMPAIGN, NOT BY FILE, and that is what makes an unload possible (P5). The
    // `TranslationServer` is a global and adding the same file twice would double every string in
    // it, so something has to remember what has already been handed over. Remembering it per
    // CAMPAIGN means the note can also be used in reverse: unsubscribe from a Workshop item and
    // its strings go with it, rather than sitting in the table naming monsters nobody can fight.
    // This is not state the game reasons with - it is a note about what has been said to Godot.
    public static class CampaignLocale
    {
        public const string Folder = "locale";

        public const string KeyColumn = "keys";

        // what a campaign handed over, so it can be taken back. Keyed by the campaign's folder,
        // normalised, because that is the one name both `Library` and a check have in hand
        sealed class Shelf
        {
            public readonly List<Translation> Translations = new List<Translation>();

            public readonly List<string> Files = new List<string>();

            public int Strings;
        }

        static readonly Dictionary<string, Shelf> Registered =
            new Dictionary<string, Shelf>(StringComparer.OrdinalIgnoreCase);

        // how many strings a campaign brought, or 0 for one that brought none. Not an error:
        // a campaign that names nothing of its own needs no locale.
        //
        // IDEMPOTENT. Several composition roots call `Library.Load`, and each of them would
        // otherwise add the same CSV again - so a campaign already on the shelf reports what it
        // brought the first time and hands over nothing
        public static int Register(string campaignFolder)
        {
            string key = Key(campaignFolder);

            if (key == null) return 0;

            if (Registered.TryGetValue(key, out Shelf already)) return already.Strings;

            string folder = Path.Combine(campaignFolder, Folder);

            if (!Directory.Exists(folder)) return 0;

            var shelf = new Shelf();

            string[] files = Directory.GetFiles(folder, "*.csv");
            Array.Sort(files, StringComparer.Ordinal);

            foreach (string file in files)
            {
                shelf.Strings += Read(file, shelf);
                shelf.Files.Add(file);
            }

            // a locale/ folder with no CSV in it leaves nothing to take back later, and a shelf
            // entry for it would make `Unregister` claim to have done something
            if (shelf.Translations.Count == 0) return 0;

            Registered[key] = shelf;

            return shelf.Strings;
        }

        // AND THE WAY BACK. `CONTENT_PIPELINE.md` P5: "the loader registers a campaign's locale/
        // CSVs when the campaign loads, and unregisters them when it unloads". Unsubscribing from
        // a Workshop item, switching campaigns, or a validator wanting to prove a string really
        // did come from the folder it says it did - all the same call.
        //
        // False when the campaign was never registered, which is not an error: a campaign that
        // brought no strings has none to take away
        public static bool Unregister(string campaignFolder)
        {
            string key = Key(campaignFolder);

            if (key == null || !Registered.TryGetValue(key, out Shelf shelf)) return false;

            foreach (Translation translation in shelf.Translations)
                TranslationServer.RemoveTranslation(translation);

            Registered.Remove(key);

            return true;
        }

        public static bool IsRegistered(string campaignFolder)
        {
            string key = Key(campaignFolder);

            return key != null && Registered.ContainsKey(key);
        }

        // one folder, one name, however it was spelled on the way in - a trailing slash or a
        // relative path would otherwise register the same campaign twice
        static string Key(string campaignFolder)
        {
            if (string.IsNullOrWhiteSpace(campaignFolder)) return null;

            try
            {
                return Path.GetFullPath(campaignFolder)
                           .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception)
            {
                // a path the OS will not even normalise is not a campaign folder
                return null;
            }
        }

        // one CSV, every locale column in it. A campaign translated into four languages is one
        // file with four columns, exactly as the engine's is
        static int Read(string file, Shelf shelf)
        {
            using GodotFile csv = GodotFile.Open(file, GodotFile.ModeFlags.Read);

            if (csv == null)
            {
                GD.PushError($"campaign locale: cannot open {file} - {GodotFile.GetOpenError()}");
                return 0;
            }

            string[] header = csv.GetCsvLine();

            if (header.Length < 2 || header[0] != KeyColumn)
            {
                GD.PushError($"campaign locale: {file} needs a '{KeyColumn}' column and at least " +
                             "one language column - its header is: " + string.Join(", ", header));
                return 0;
            }

            var languages = new Translation[header.Length];

            for (int column = 1; column < header.Length; column++)
                languages[column] = new Translation { Locale = header[column] };

            int added = 0;

            while (!csv.EofReached())
            {
                string[] cells = csv.GetCsvLine();

                if (cells.Length == 0 || string.IsNullOrWhiteSpace(cells[0])) continue;

                for (int column = 1; column < header.Length && column < cells.Length; column++)
                {
                    if (string.IsNullOrEmpty(cells[column])) continue;

                    languages[column].AddMessage(cells[0], cells[column]);
                    added++;
                }
            }

            for (int column = 1; column < header.Length; column++)
            {
                TranslationServer.AddTranslation(languages[column]);
                shelf.Translations.Add(languages[column]);
            }

            return added;
        }

        // what has been handed over, for a report and for a check that wants to know
        public static IReadOnlyCollection<string> Files =>
            Registered.Values.SelectMany(s => s.Files).ToArray();
    }
}
