using System.Collections.Generic;
using Godot;

namespace Game.Companion
{
    // W6, and W6 is a DOOR, not a feature. There is no voiced dialogue in this game - not the
    // companion, not the DM, not an NPC (THE_TABLE.md section 9 Q1, resolved; CONVENTIONS.md "text
    // before voice"). Text carries every word and only atmospheric audio ships.
    //
    // What is built here is the whole of what keeping that door open costs: a lookup from a line's
    // KEY to a clip that is almost certainly not there. Because every word in the game is keyed
    // already, a human-performed layer could later be dropped into
    // audio/samples/dialogue/<creature>/ and played alongside the text without one line of writing
    // being revised - and with nothing to do if the clip is missing, which is the normal case and
    // the shipping case.
    //
    // The two rules that make it additive rather than load-bearing, and they are enforced here:
    // the text is shown whether or not a clip exists, and its time on screen is never shortened by
    // one. Switch language and the words switch while the voice drops to nothing.
    public sealed class Voiceover
    {
        // folder-is-the-list, from M7, one folder per speaker
        public const string Samples = "res://audio/samples/dialogue/";

        // cached per speaker: a folder that is empty stays empty, and asking the disk every bark
        // for a layer nobody has recorded is the kind of cost that never shows up in a profile
        static readonly Dictionary<string, Dictionary<string, string>> Folders =
            new Dictionary<string, Dictionary<string, string>>();

        public static string FolderFor(string speaker) => Samples + speaker + "/";

        // whether anyone has recorded anything for this voice at all
        public static bool Any(string speaker) => Clips(speaker).Count > 0;

        // the clip for a line key, or null - which is the answer in every shipping build
        public static AudioStream For(string speaker, string key)
        {
            if (string.IsNullOrEmpty(speaker) || string.IsNullOrEmpty(key)) return null;

            Dictionary<string, string> clips = Clips(speaker);

            return clips.TryGetValue(key, out string file) ? GD.Load<AudioStream>(file) : null;
        }

        // a clip is named for the key it carries, so the file and the line cannot drift apart:
        // dialogue.wolf.bark.snag.017.wav
        static Dictionary<string, string> Clips(string speaker)
        {
            if (Folders.TryGetValue(speaker, out Dictionary<string, string> already)) return already;

            var found = new Dictionary<string, string>();
            string folder = FolderFor(speaker);

            if (DirAccess.DirExistsAbsolute(folder))
            {
                foreach (string file in DirAccess.GetFilesAt(folder))
                {
                    // .import beside each asset in an exported build; the stem is the key either way
                    string name = file.EndsWith(".import") ? file[..^".import".Length] : file;

                    string key = System.IO.Path.GetFileNameWithoutExtension(name);

                    if (key.Length > 0) found[key] = folder + name;
                }
            }

            Folders[speaker] = found;

            return found;
        }

        // for a check that wants to re-read the disk after a recording session
        public static void Forget() => Folders.Clear();
    }
}
