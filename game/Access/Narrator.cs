using System.Collections.Generic;
using Godot;

namespace Game.Access
{
    // THE THING THAT SAYS IT OUT LOUD (AX2).
    //
    // A screen reader, driven from inside the game rather than from outside it. A 3D table is not a
    // widget tree, so there is nothing for an external reader to walk: the objects have names and
    // states and no accessibility layer to hang them off. What there is instead is a hand that knows
    // what it is on (Pointing) and a list of finished sentences (Spoken), and this speaks them.
    //
    // It uses the platform's own voice through DisplayServer, which needs
    // audio/general/text_to_speech switched on in the project or there are no voices to ask for -
    // that setting is the whole of the wiring and it is in project.godot beside the locales.
    //
    // IT KEEPS A TRANSCRIPT, and that is not a debugging convenience: "a screen reader narrates the
    // room, the sheet, a fight and a conversation with nothing silent" is AX2's verify line, and a
    // machine can only hold that if what was said is readable back. A headless machine has no voices
    // at all, so the transcript is also the only thing check-access.ps1 can measure.
    [GlobalClass]
    public partial class Narrator : Node
    {
        // OFF UNTIL ASKED. A game that starts talking at somebody who did not want it is a game they
        // turn off; the setting is on the settings page and the F3 key reaches it from anywhere
        [Export] public bool Aloud { get; set; }

        // 1 is the platform's own pace. The speech-speed setting moves this, so slowing the
        // companion's bubbles down slows the voice with them (AX4)
        [Export] public float Rate { get; set; } = 1f;

        [Export] public int Volume { get; set; } = 50;

        // how much is kept. Long enough for a check to read a whole scene back, short enough that a
        // four-hour sitting does not accumulate one
        public const int Remembered = 96;

        readonly List<string> _said = new List<string>();

        string _voice = "";

        public IReadOnlyList<string> Everything => _said;

        public string Last => _said.Count == 0 ? "" : _said[_said.Count - 1];

        // is there a voice on this machine at all. False headless, and false on a box with no TTS
        public bool Able => _voice.Length > 0;

        public string Voice => _voice;

        public override void _Ready()
        {
            _voice = Pick();

            GD.Print(Able
                ? $"reader  {_voice} - {(Aloud ? "on" : "off until asked")}"
                : "reader  no voice on this machine, so nothing is spoken - what would have been " +
                  "said is still printed and still counted");
        }

        // the first voice for the language the game is actually showing, so switching locale
        // switches the voice with it rather than reading French in an English accent
        static string Pick()
        {
            if (!(bool)ProjectSettings.GetSetting("audio/general/text_to_speech", false)) return "";

            string language = TranslationServer.GetLocale();

            int dash = language.IndexOf('-');

            string[] fitting = DisplayServer.TtsGetVoicesForLanguage(
                dash > 0 ? language.Substring(0, dash) : language);

            if (fitting.Length > 0) return fitting[0];

            Godot.Collections.Array<Godot.Collections.Dictionary> voices =
                DisplayServer.TtsGetVoices();

            return voices.Count > 0 ? voices[0]["id"].AsString() : "";
        }

        // SAID, COUNTED AND PRINTED WHETHER OR NOT ANYBODY IS LISTENING. The transcript is what the
        // check reads, so a run with the reader switched off still proves nothing went silent.
        public bool Say(Spoken what)
        {
            if (what == null || !what.Any) return false;

            foreach (string line in what.Lines)
            {
                _said.Add(line);

                if (_said.Count > Remembered) _said.RemoveAt(0);
            }

            GD.Print($"read    {what}");

            if (!Aloud || !Able) return false;

            DisplayServer.TtsSpeak(what.Aloud, _voice, Volume, 1f, Mathf.Clamp(Rate, 0.1f, 10f),
                                   0, what.Interrupts);

            return true;
        }

        public void Hush()
        {
            if (Able) DisplayServer.TtsStop();
        }

        // switched on or off by the settings page and by the one key that reaches it from anywhere.
        // Turning it on says so in the voice being turned on, which is the only confirmation that
        // actually confirms anything
        public bool Listening(bool aloud)
        {
            if (Aloud == aloud) return Aloud;

            Aloud = aloud;

            if (!aloud) Hush();

            return Aloud;
        }

        public void Forget() => _said.Clear();

        public override void _ExitTree() => Hush();

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"reader: {(Able ? _voice : "no voice")}, {(Aloud ? "on" : "off")}, " +
            $"{_said.Count} line(s) said";
    }
}
