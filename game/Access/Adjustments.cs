using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Companion;

namespace Game.Access
{
    // HOW BIG THE LETTERS ARE
    public enum Lettering
    {
        Small,

        Normal,

        Large,

        Largest,
    }

    // HOW FAST ANYBODY TALKS. Every word in this game is text, so "speech speed" is how long a line
    // stays up before it recedes - the dwell time Reading already computes, exposed as a choice
    // as a choice
    public enum Pace
    {
        Slow,

        Normal,

        Quick,
    }

    // EVERY DIAL THE PLAYER CAN TURN, AND WHAT EACH ONE MEANS.
    //
    // One object, held by the room, written to one small file. The settings page in the book is a
    // surface over this and knows nothing except how to print a line and turn it; this knows nothing
    // about the book. That is what lets the same dials be reached by a key from anywhere - F3 turns
    // the reader on without opening anything - and it is why the meanings live here rather than at
    // the point of use, where four callers would each have their own idea of what "large" is.
    //
    // Godot-free, file included: it is plain text through System.IO, so a test writes one to a temp
    // folder and reads it back without an engine.
    public sealed class Adjustments
    {
        public Lettering Lettering { get; set; } = Lettering.Normal;

        public bool HighContrast { get; set; }

        public Pace Speech { get; set; } = Pace.Normal;

        public bool Captions { get; set; }

        // off until asked, the same call the narrator itself makes
        public bool ReadAloud { get; set; }

        public Arm Skin { get; set; } = Arm.Almond;

        public Shown Showing { get; set; } = Shown.Both;

        // the keys are a dial like any other and are kept in the same file
        public Bindings Keys { get; } = new Bindings();

        public void Plainly()
        {
            Lettering = Lettering.Normal;
            HighContrast = false;
            Speech = Pace.Normal;
            Captions = false;
            ReadAloud = false;
            Skin = Arm.Almond;
            Showing = Shown.Both;

            Keys.Plainly();
        }


        // ---- what each one means -------------------------------------------------------------

        // WHAT A FONT SIZE IS MULTIPLIED BY. Godot's Label3D sizes are points against a pixel size,
        // so scaling the number is the whole of resizable text - and the top step is a third again
        // rather than double, because a card on a table has an edge and text that overflows it is
        // less legible rather than more (which is what the pseudolocale's 30% padding is for).
        public float TextScale => Lettering switch
        {
            Lettering.Small => 0.85f,
            Lettering.Large => 1.2f,
            Lettering.Largest => 1.35f,
            _ => 1f,
        };

        // CHARACTERS A SECOND, fed to Reading. Slow leaves a line up half again as long, quick takes
        // a third off, and neither touches the floor and ceiling Reading already clamps to.
        public double Speed => Speech switch
        {
            Pace.Slow => Reading.Pace * 0.66,
            Pace.Quick => Reading.Pace * 1.4,
            _ => Reading.Pace,
        };

        // and the platform voice moves with it, so slowing the bubbles down does not leave the
        // reader racing them
        public float Rate => Speech switch
        {
            Pace.Slow => 0.8f,
            Pace.Quick => 1.3f,
            _ => 1f,
        };

        // how long this line stays up, with the player's own pace applied
        public double Time(string line) => Reading.At(line, Speed);


        // ---- written down ---------------------------------------------------------------------

        public const string FileName = "settings.txt";

        public const char Between = '=';

        // the bindings share the file and say so, so an act and a dial can never be read as each
        // other however either list grows
        public const string KeyPrefix = "key.";

        public IEnumerable<string> Lines()
        {
            yield return "lettering" + Between + Lettering.ToString().ToLowerInvariant();
            yield return "contrast" + Between + Said(HighContrast);
            yield return "speech" + Between + Speech.ToString().ToLowerInvariant();
            yield return "captions" + Between + Said(Captions);
            yield return "read_aloud" + Between + Said(ReadAloud);
            yield return "arm" + Between + Skin.Word();
            yield return "arms" + Between + Showing.Word();

            foreach (string line in Keys.Lines()) yield return KeyPrefix + line;
        }

        public const string On = "on";

        public const string Off = "off";

        static string Said(bool on) => on ? On : Off;

        // A LINE THIS BUILD DOES NOT UNDERSTAND IS SKIPPED, not refused. A settings file is the
        // player's, it outlives any one version of the game, and losing every dial because one line
        // came from a later build is the worst possible reading of it.
        public int Read(IEnumerable<string> lines)
        {
            int took = 0;

            foreach (string line in lines ?? Array.Empty<string>())
            {
                if (line == null) continue;

                string said = line.Trim();

                if (said.Length == 0 || said.StartsWith("#", StringComparison.Ordinal)) continue;

                if (said.StartsWith(KeyPrefix, StringComparison.Ordinal))
                {
                    took += Keys.Read(new[] { said.Substring(KeyPrefix.Length) });
                    continue;
                }

                int at = said.IndexOf(Between);

                if (at <= 0) continue;

                if (Took(said.Substring(0, at).Trim().ToLowerInvariant(),
                         said.Substring(at + 1).Trim().ToLowerInvariant())) took++;
            }

            return took;
        }

        bool Took(string dial, string value) => dial switch
        {
            "lettering" => Enum.TryParse(value, true, out Lettering lettering) &&
                           Set(() => Lettering = lettering),
            "contrast" => Yes(value, out bool contrast) && Set(() => HighContrast = contrast),
            "speech" => Enum.TryParse(value, true, out Pace speech) && Set(() => Speech = speech),
            "captions" => Yes(value, out bool captions) && Set(() => Captions = captions),
            "read_aloud" => Yes(value, out bool aloud) && Set(() => ReadAloud = aloud),
            "arm" => Access.Arms.TryWord(value, out Arm arm) && Set(() => Skin = arm),
            "arms" => Access.Arms.TryWord(value, out Shown shown) && Set(() => Showing = shown),
            _ => false,
        };

        static bool Set(Action what) { what(); return true; }

        static bool Yes(string value, out bool on)
        {
            on = value == On;

            return value == On || value == Off;
        }

        // READ AND WRITTEN WHERE THE ENGINE KEEPS THE PLAYER'S THINGS, not beside the saves: a
        // settings file is the machine's rather than the character's, and it survives a save folder
        // being carried somewhere else.
        public static Adjustments From(string folder)
        {
            var how = new Adjustments();

            string path = Path.Combine(folder ?? "", FileName);

            try
            {
                if (File.Exists(path)) how.Read(File.ReadAllLines(path));
            }
            catch (IOException)
            {
                // an unreadable settings file is a player who gets the defaults, not a player who
                // cannot start the game
            }
            catch (UnauthorizedAccessException)
            {
            }

            return how;
        }

        public bool To(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);

                File.WriteAllLines(Path.Combine(folder, FileName), Lines());

                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            string.Join(", ", Lines().Where(l => !l.StartsWith(KeyPrefix, StringComparison.Ordinal))) +
            $", {Keys.Changed} key(s) rebound";
    }
}
