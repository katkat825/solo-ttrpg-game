using Godot;
using Core.Localization;
using Game.Localization;

namespace Game.Room
{
    // One object in the room, standing where it stands and doing what it is.
    //
    // "Each object is interactive in place - click the door to quit, the corkboard for the quest
    // state, the dice case for what you own. NO OBJECT OPENS A MENU; each IS the thing"
    // (ROOM_AND_SHEET.md R3). So this node has no Open(), no panel and no scene to push - it has a
    // Touched signal and the room decides what that means in the room.
    [GlobalClass]
    public partial class Furniture : Node3D
    {
        [Export] public Prop Is { get; set; } = Prop.Shelf;

        [Export] public Vector3 Size { get; set; } = new Vector3(0.4f, 0.6f, 0.2f);

        [Export] public Color Wood { get; set; } = new Color("#54452f");

        // how it lights up when the cursor is over it. The only affordance the room has, because
        // the objects are the interface and a tooltip would be a panel
        [Export] public float Lift { get; set; } = 0.22f;

        [Signal] public delegate void TouchedEventHandler(int prop);

        MeshInstance3D _body;

        StandardMaterial3D _finish;

        StaticBody3D _touch;

        Color _plain;

        readonly ILocalizer _text = new GodotLocalizer();

        // what a player would call it, in their own language. Read once at _Ready, because the room
        // does not switch locale mid-glance and a Label3D per prop is a tooltip in disguise
        public string Called { get; private set; } = "";

        public override void _Ready()
        {
            if (_body != null) return;

            _plain = Wood;

            _finish = new StandardMaterial3D { AlbedoColor = _plain, Roughness = 0.9f };

            _body = new MeshInstance3D
            {
                Name = "Body",
                Mesh = new BoxMesh { Size = Size },
                MaterialOverride = _finish,
            };

            AddChild(_body);

            _touch = new StaticBody3D { Name = "Touch" };

            _touch.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = Size } });

            AddChild(_touch);

            Called = _text.Get(Props.NameKey(Is));
        }

        public bool Owns(GodotObject what) => _touch != null && ReferenceEquals(_touch, what);

        public void Light(bool lit)
        {
            if (_finish == null) return;

            _finish.AlbedoColor = lit ? _plain.Lightened(Lift) : _plain;
        }

        public void Touch() => EmitSignal(SignalName.Touched, (int)Is);

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"{Is.Word()} - replaces {Is.Replaces()}";
    }
}
