using System.Collections.Generic;
using Content.Encounters;
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

            _secret = new SecretRoll(new SeededRng(Seed != 0 ? Seed : (int)Time.GetTicksMsec()));
        }

        public void Perform(EncounterPlan plan, When moment, string campaign)
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

            if (said != null) GD.Print($"        \"{said}\"");
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

            _hands?.Perform(Gesture.Push);

            _performed.Add($"push - \"{said}\"");

            GD.Print($"dm      \"{said}\"");
        }

        static string Text(string key, params object[] args)
        {
            string text = new GodotLocalizer().Get(key);

            return args is { Length: > 0 } ? string.Format(text, args) : text;
        }
    }
}
