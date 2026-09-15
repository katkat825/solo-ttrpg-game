using System.Collections.Generic;
using System.IO;
using Godot;

namespace Game.Campaigns
{
    public static class CampaignFolders
    {
        public const string FolderName = "campaigns";

        public static IReadOnlyList<string> Roots()
        {
            var roots = new List<string>();

            Add(roots, Local());

            return roots;
        }

        public static string Local() =>
            OS.HasFeature("editor")
                ? Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", FolderName))
                : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".", FolderName);

        static void Add(List<string> roots, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return;

            // a root added twice would load every campaign in it twice and collide each id with itself
            foreach (string already in roots)
                if (string.Equals(already, root, System.StringComparison.OrdinalIgnoreCase)) return;

            roots.Add(root);
        }

        // sorted because filesystem enumeration order is otherwise unstable
        public static IReadOnlyList<string> In(string root)
        {
            var folders = new List<string>();

            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return folders;

            foreach (string folder in Directory.EnumerateDirectories(root))
                folders.Add(folder);

            folders.Sort(System.StringComparer.Ordinal);

            return folders;
        }
    }
}
