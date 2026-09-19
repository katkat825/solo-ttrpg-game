using Godot;
using Game.Book;

// Game.Room is both a namespace and a type; alias so Room means the node
using RoomNode = Game.Room.Room;

namespace Game.Access
{
    // THE WAY IN (Phase AX).
    //
    // Everything the room needs in order to be playable by somebody who cannot use a mouse, cannot see
    // it, or cannot hear it - in one object, beside the room rather than inside it. The room is already
    // the largest thing in the game and it is large for a good reason (what touching each object means
    // is written in one place a person can read); putting the hand, the voice, the letters and the
    // keys in there as well would have made it the file nobody opens.
    //
    // It owns the dials, because everything here reads them and because a key reaches them from
    // anywhere - F3 turns the reader on without opening the book, and the book's settings page turns
    // the same object.
    //
    // WHAT IT DOES NOT OWN IS THE LIST OF THINGS IN THE ROOM. It asks the room, every time anything
    // changes, because the room is what knows: a second list of what is reachable would be a second
    // description of the room, and the half that fell behind would be the half a keyboard player
    // depends on.
    [GlobalClass]
    public partial class Reaching : Node
    {
        [Export] public NodePath NarratorPath { get; set; }

        [Export] public NodePath CaptionsPath { get; set; }

        [Export] public NodePath HandsPath { get; set; }

        // where settings.txt lives. Empty means the engine's own user folder, which is where the
        // player's own things belong - a settings file is the machine's, not the character's
        [Export] public string SettingsFolder { get; set; } = "";

        // WHERE YOUR HAND IS, with no mouse under it
        public Pointing Hand { get; } = new Pointing();

        // WHAT THE PLAYER ASKED FOR. Read once at boot and written whenever a dial turns
        public Adjustments How { get; private set; } = new Adjustments();

        // how big the letters are, and how hard
        public Legible Letters { get; } = new Legible();

        RoomNode _room;

        Narrator _reader;

        Captioned _captions;

        Hands _hands;

        public Narrator Reader => _reader;

        public Captioned Captions => _captions;

        public Hands TheHands => _hands;

        public override void _Ready()
        {
            _room = GetParent<RoomNode>();

            _reader = Found<Narrator>(NarratorPath);
            _captions = Found<Captioned>(CaptionsPath);
            _hands = Found<Hands>(HandsPath);

            How = Adjustments.From(Folder());

            int bound = Keyboard.Install(How.Keys);

            if (_captions != null && _room != null)
            {
                int told = _captions.Tells(_room);

                GD.Print($"caption {told} thing(s) that make a noise know where the caption card is");
            }

            Applied();

            // NOT GATHERED HERE. A child is ready before its parent, so at this point the room has
            // not found its own furniture yet and there is nothing to gather - the room calls
            // Gathered() at the end of its own _Ready, and on every change after that.
            GD.Print($"access  {bound} key(s) bound - {How}");

            if (_room == null)
                GD.PushWarning("access: Reaching has to be a child of the Room - there is nothing " +
                               "here to reach");

            // the room tone is under everything and started before anything was wired, so it is
            // captioned on arrival rather than when it began
            if (How.Captions) _captions?.Says(Game.Audio.Sound.Room);
        }

        T Found<T>(NodePath path) where T : Node =>
            path != null && !path.IsEmpty ? GetNodeOrNull<T>(path) : null;

        public string Folder() =>
            SettingsFolder.Length > 0
                ? SettingsFolder
                : ProjectSettings.GlobalizePath("user://");

        // ---- the hand --------------------------------------------------------------------------

        // THE ROOM CHANGED, SO ASK IT AGAIN. Called by the room whenever it lays words out or stands
        // a book somewhere; the hand stays on whatever it was on, and the letters are re-sized because
        // anything just laid out was laid out at its authored size.
        public void Gathered()
        {
            if (_room == null) return;

            Hand.Over(_room.Reachables());

            Letters.Apply(_room, How);

            if (_counted) return;

            _counted = true;

            GD.Print($"access  {Hand.Count} thing(s) reachable, {Hand.Live} of them live, " +
                     $"{Letters.Known} label(s) sized");
        }

        bool _counted;

        // moved, lit, and said out loud - the three things that have to happen together or the hand is
        // somewhere the player cannot tell
        void Moved(bool moved)
        {
            if (!moved) return;

            foreach (Reachable one in Hand.Ring) one.Light(false);

            Hand.On?.Light(true);

            Announce(Spoken.Reading(Hand.On));
        }

        // the mouse went somewhere: the hand goes with it, so the next reach carries on from there
        public void Onto(GodotObject body) => Hand.Onto(body);

        public bool Announce(Spoken what) => _reader?.Say(what) ?? false;


        // ---- the keys --------------------------------------------------------------------------

        public override void _UnhandledInput(InputEvent what)
        {
            // ARMED FIRST, AND IT SWALLOWS EVERYTHING. A page waiting for a key has to be able to
            // take the key that is already bound to something, or you can never swap two of them
            if (How.Keys.Armed != null && Keyboard.Raw(what, out Key pressed, out bool shift))
            {
                Rebound(How.Keys.Pressed(pressed, shift));

                GetViewport().SetInputAsHandled();

                return;
            }

            if (Keyboard.Pressed(what) is not { } act) return;

            switch (act)
            {
                case Act.ReachNext:
                    Moved(Hand.Next());
                    break;

                case Act.ReachBack:
                    Moved(Hand.Back());
                    break;

                case Act.Touch:
                    if (!Hand.Press()) return;
                    break;

                case Act.Help:
                    _room?.Asks();
                    break;

                case Act.WhereAreWe:
                    Announce(_room?.Whereabouts());
                    break;

                case Act.ReadAloud:
                    Turned(Setting.ReadAloud);
                    break;

                // the tray's, and it has had that key since M1
                case Act.ThrowDice:
                    return;
            }

            GetViewport().SetInputAsHandled();
        }

        void Rebound(Bindings.Took took)
        {
            if (took == Bindings.Took.Bound)
            {
                Keyboard.Install(How.Keys);
                How.To(Folder());
            }

            GD.Print($"access  {took.ToString().ToLowerInvariant()} - {How.Keys}");

            // the page is showing the old key, so it is laid again either way
            _room?.Again();
        }


        // ---- the dials -------------------------------------------------------------------------

        // ONE LINE TURNED, WHEREVER IT WAS TURNED FROM. The settings page calls this and so does the
        // key that reaches the reader from anywhere, so there is one path from a choice to the thing
        // it changes and nothing can be applied in one of two ways.
        public bool Turned(Setting setting)
        {
            if (!setting.Turn(How)) return false;

            Applied();

            How.To(Folder());

            GD.Print($"access  {setting.Word()} is {setting.Value(How)}");

            // the page showing it is showing the old value, wherever the turn came from
            _room?.Again();

            return true;
        }

        // WHAT EVERY DIAL DOES, in one place. Called after any of them moves and once at boot, so the
        // room that comes up is the room the player left.
        public void Applied()
        {
            _hands?.Wears(How);

            if (_captions != null)
            {
                _captions.How = How;

                if (!How.Captions) _captions.Clear();
            }

            if (_reader != null)
            {
                _reader.Rate = How.Rate;

                _reader.Listening(How.ReadAloud);
            }

            if (_room != null) Letters.Apply(_room, How);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"access: {Hand}, {(_reader == null ? "no reader" : _reader.ToString())}";
    }
}
