using System.Collections.Generic;
using Content.Places;
using Core.Dice;
using Core.Localization;
using Game.Localization;
using Godot;

namespace Game.Dm
{
    [GlobalClass]
    public partial class Dm : Node3D
    {
        [Export] public NodePath ScreenPath { get; set; } = "Screen";

        [Export] public NodePath HandsPath { get; set; } = "Hands";

        // the board's squares, so a cue placing a mini at a slot reaches the right one; empty makes it happen at the table centre, wrong but visible
        [Export] public NodePath BoardPath { get; set; }

        DmScreen _screen;
        DmHands _hands;
        DmVoice _voice;
        Board.Board _board;

        SecretRoll _secret;

        // the DM's own rng stream: rolling for nothing must not move the dice a swing is about to get, and a seeded run must stay reproducible
        [Export] public int Seed { get; set; }

        // HOW OFTEN THE DM ROLLS FOR NOTHING, exported because the only way to tune a presence is
        // to sit at the table moving the number (DEFERRED.md says exactly this about every DM
        // timing). The eye check found the shipped pair - a 9 second rest and a 0.18 chance per
        // second, so a rattle every ~15 seconds - was a nervous tic rather than a presence, which
        // is the failure SecretRoll's own comment warns about. These two average one rattle a
        // minute or so: Rest + 1/Chance seconds between them.
        [Export] public float RollsForNothingEvery { get; set; } = 30f;

        [Export] public float RollsForNothingChance { get; set; } = 0.025f;

        // where a pushed note comes to rest, in the DM's own space: across the table toward the
        // player, short of the board, where you would reach for it
        [Export] public Vector3 NoteLands { get; set; } = new Vector3(0.02f, 0.045f, 0.72f);

        Game.Companion.Bubble _note;

        // transcript of what the DM did, for a headless check; developer diagnostics, not localized
        readonly List<string> _performed = new List<string>();

        public IReadOnlyList<string> Performed => _performed;

        public override void _Ready()
        {
            _screen = GetNodeOrNull<DmScreen>(ScreenPath);
            _hands = GetNodeOrNull<DmHands>(HandsPath);

            if (_screen == null)
            {
                _screen = new DmScreen { Name = "Screen" };
                AddChild(_screen);
            }

            if (_hands == null)
            {
                _hands = new DmHands { Name = "Hands" };
                AddChild(_hands);
            }

            _voice = GetNodeOrNull<DmVoice>("Voice");

            if (_voice == null)
            {
                _voice = new DmVoice { Name = "Voice" };
                AddChild(_voice);
            }

            if (BoardPath != null && !BoardPath.IsEmpty) _board = GetNodeOrNull<Board.Board>(BoardPath);

            _note = GetNodeOrNull<Game.Companion.Bubble>("Note");

            if (_note == null)
            {
                // THE WORDS THE DM PUSHES ACROSS. Until the eye check there was no such object:
                // Says() moved a hand, printed the line to the console and put a blank card on the
                // table - which is the "empty white-ish rectangle" the check found, and it had no
                // words because nothing was ever asked to carry any.
                _note = new Game.Companion.Bubble
                {
                    Name = "Note",
                    Position = NoteLands,

                    // a scrap torn off a pad, not the companion's card: smaller, greyer, and it
                    // stays up long enough to be read across a table
                    Width = 0.26f,
                    Card = new Color(0.86f, 0.83f, 0.74f, 0.97f),
                    Ink = new Color("#241f18"),
                };

                AddChild(_note);
            }

            _secret = new SecretRoll(new SeededRng(Seed != 0 ? Seed : (int)Time.GetTicksMsec()),
                                     RollsForNothingEvery, RollsForNothingChance);
        }

        public void Perform(Content.Places.Place plan, When moment, string campaign)
        {
            if (plan == null) return;

            foreach (Cue cue in plan.CuesFor(moment)) Perform(cue, campaign);
        }

        public void Perform(Cue cue, string campaign)
        {
            if (cue == null) return;

            Vector3? at = Where(cue);

            _hands?.Perform(cue.Does, cue.Hesitant, at);

            string line = cue.LineKey(campaign);

            string said = line == null ? null : Text(line);

            _performed.Add(cue.Does.Word() + (cue.Hesitant ? " (hesitant)" : "") +
                           (cue.Slot > 0 ? $" at slot {cue.Slot}" : "") +
                           (said == null ? "" : $" - \"{said}\""));

            GD.Print($"dm      {cue.Id}: {(cue.Hesitant ? "hesitantly " : "")}{cue.Does.Word()}" +
                     (cue.Slot > 0 ? $" at slot {cue.Slot}" : ""));

            // AND THE WORDS GO ON THE TABLE. This was the other half of the blank card the eye
            // check found: a cue whose gesture carries a line resolved that line, printed it to
            // the console, pushed an empty hand across and left the player looking at nothing. A
            // gesture that tells is a gesture with something to read at the end of it.
            if (said != null)
            {
                GD.Print($"        \"{said}\"");

                Reads(said, line);
            }
        }

        // where a gesture happens, in the hands' own space: a cue names a slot, the map knows where, and no slot returns null
        Vector3? Where(Cue cue)
        {
            if (cue.Slot <= 0 || _board?.Map == null || _hands == null) return null;

            Core.Space.Cell? cell = _board.Map.SpawnAt(cue.Slot);

            if (cell == null) return null;

            return _hands.ToLocal(_board.ToGlobal(_board.Metrics.Centre(cell.Value)));
        }

        // A GESTURE WITH NO LINE BEHIND IT (R1). Most of what the DM does carries words on a
        // pushed note, and a Cue is the right shape for that. Picking up the character sheet,
        // turning it round and looking at it is not - "no text required, the gesture carries it" -
        // and going through a Cue for it would build a narration key for a line nobody wrote.
        public void Does(params Gesture[] gestures)
        {
            foreach (Gesture gesture in gestures ?? System.Array.Empty<Gesture>())
            {
                _hands?.Perform(gesture);

                _performed.Add(gesture.Word());

                GD.Print($"dm      {gesture.Word()}");
            }
        }

        public void RollsForSomething()
        {
            _secret?.Meant();
            _voice?.Rattles();

            _performed.Add("secret roll");

            GD.Print("dm      a rattle behind the screen, and a pause");
        }

        public bool MightRollForNothing(double delta)
        {
            if (_secret == null || !_secret.ForNothing(delta)) return false;

            _voice?.Rattles();

            _performed.Add("secret roll for nothing");

            GD.Print("dm      a rattle behind the screen - and nothing happens");

            return true;
        }

        public void Reacts()
        {
            _voice?.Reacts();

            _performed.Add("hm");
        }

        public void Says(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return;

            string said = Text(key, args);

            // NOTHING TO SAY IS NOT A BLANK NOTE. A key with no words behind it used to push an
            // empty card across the table; now it pushes nothing, and check-locale.ps1 is still
            // the thing that stops a key having no words in the first place.
            if (string.IsNullOrWhiteSpace(said) || said == key)
            {
                GD.PushWarning($"dm: '{key}' has no words behind it, so the note stayed on the pad");
                return;
            }

            _hands?.Perform(Gesture.Push);

            Reads(said, key);

            _performed.Add($"push - \"{said}\"");

            GD.Print($"dm      \"{said}\"");
        }

        // one place a line of the DM's becomes a thing on the table, so a cue and a readout cannot
        // end up doing it two different ways
        void Reads(string said, string key)
        {
            if (string.IsNullOrWhiteSpace(said) || said == key) return;

            _note?.Say(said);
        }

        // what is on the note right now, for a headless check to read back; the words came from
        // the localizer and this is a copy of them, never a second source of truth
        public string Note => _note?.Said ?? "";

        static string Text(string key, params object[] args)
        {
            string text = new GodotLocalizer().Get(key);

            return args is { Length: > 0 } ? string.Format(text, args) : text;
        }
    }
}
