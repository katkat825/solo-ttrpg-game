using System.Collections.Generic;
using System.IO;
using Godot;

namespace Game.Campaigns
{
    // WHERE CAMPAIGNS LIVE, WHICH IS NOT INSIDE THE GAME (CONTENT_PIPELINE.md P4, P8).
    //
    // A campaign is a folder on a disk - hand-editable, diffable, zippable - and that rules out
    // `res://`, because in an exported game `res://` is inside a `.pck` and nothing but Godot can
    // read it. Steam makes the point again from the other end: subscribed Workshop items land in
    // `steamapps/workshop/content/<appid>/<itemid>/`, which is somewhere else entirely and updated
    // by Steam rather than by the game.
    //
    // SO THE LOADER TAKES A LIST OF ROOTS AND NOT A PATH. That is the whole design note from
    // `CONTENT_PIPELINE.md`: "Designed in, this is trivial; hardcoded to one path, it is a
    // rewrite." Today the list has one entry and P8 appends the subscribed ones.
    //
    // TWO ANSWERS FOR ONE QUESTION, and both are about where the game is standing:
    //
    //   in the editor   `res://` globalizes to `.../solo_ttrpg_game/game/`, so campaigns are its
    //                   sibling - beside `core/` and `sim/`, in the repo, in git
    //   exported        beside the executable, which is where a player would look for them and
    //                   where an installer would put them
    //
    // Nothing under here reads a file. It answers "which directories" and hands them to
    // `Content.*`, which is Godot-free and does the reading with `System.IO` - see content.csproj.
    public static class CampaignFolders
    {
        public const string FolderName = "campaigns";

        // every root to look in, best guess first. Missing ones are not a problem - a game with no
        // campaigns installed has to boot (ARCHITECTURE.md section 8)
        public static IReadOnlyList<string> Roots()
        {
            var roots = new List<string>();

            Add(roots, Local());

            // P8 appends every subscribed Workshop item directory here, and nothing else changes

            return roots;
        }

        // the one the developer edits, and the one an installed game ships
        public static string Local() =>
            OS.HasFeature("editor")
                ? Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", FolderName))
                : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".", FolderName);

        static void Add(List<string> roots, string root)
        {
            if (string.IsNullOrWhiteSpace(root)) return;

            // the same folder twice would load every campaign in it twice, and then every id in it
            // would collide with itself - which reads as a namespacing bug and is a wiring one
            foreach (string already in roots)
                if (string.Equals(already, root, System.StringComparison.OrdinalIgnoreCase)) return;

            roots.Add(root);
        }

        // the campaign folders inside a root: every directory in it, sorted, because the order a
        // filesystem hands back its entries is the filesystem's opinion
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
