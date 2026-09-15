using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;

// System.IO and Godot both define FileAccess; this aliases Godot's
using GodotFile = Godot.FileAccess;

namespace Game.Campaigns
{
    public static class CampaignLocale
    {
        public const string Folder = "locale";

        public const string KeyColumn = "keys";

        sealed class Shelf
        {
            public readonly List<Translation> Translations = new List<Translation>();

            public readonly List<string> Files = new List<string>();

            public int Strings;
        }

        static readonly Dictionary<string, Shelf> Registered =
            new Dictionary<string, Shelf>(StringComparer.OrdinalIgnoreCase);

        // idempotent: several roots call Load, so re-registering would add the same csv twice
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

            // no shelf entry for an empty locale/, or Unregister would falsely report a removal
            if (shelf.Translations.Count == 0) return 0;

            Registered[key] = shelf;

            return shelf.Strings;
        }

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

        // normalise so a trailing slash or relative path can't register the same campaign twice
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
                return null;
            }
        }

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

        public static IReadOnlyCollection<string> Files =>
            Registered.Values.SelectMany(s => s.Files).ToArray();
    }
}
