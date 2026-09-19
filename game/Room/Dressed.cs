using System.Collections.Generic;
using Godot;

namespace Game.Room
{
    // THE TABLE, DRESSED - the props on it swapped for other versions of themselves.
    //
    // Dressing is which version of each prop is on the table tonight; this is the node that makes
    // that true of the objects. Two ways a prop is swapped, and no third:
    //
    //   - the thing already knows how to wear a skin, and is told to. The dice tray is the one
    //     that does today: it loads its own felt out of a folder and has since M-something
    //   - the thing IS the version, and is replaced by another scene standing where it stood.
    //     That is the scaffolding, ahead of the art: a screen is a screen, and a campaign's own
    //     screen is the same object with different art on it
    //
    // COSMETIC, ALWAYS. Nothing here reads a number and nothing here writes one. A finer tray is
    // finer; the moment it is BETTER, every session becomes a question about your inventory
    // instead of the dungeon, and that is the standing rule this node exists inside rather than
    // beside.
    [GlobalClass]
    public partial class Dressed : Node3D
    {
        // the dice tray, which is the one prop that already wears skins for real
        [Export] public NodePath TrayPath { get; set; }

        // where each of the others stands, so a swapped one can be put in the same place. A prop
        // with no path here simply cannot be swapped yet, which is most of them until there is art
        [Export] public Godot.Collections.Dictionary<string, NodePath> Props { get; set; } =
            new Godot.Collections.Dictionary<string, NodePath>();

        // "<prop>/<skin>" -> the scene that IS that version of it. Empty is the shipped table
        [Export] public Godot.Collections.Dictionary<string, PackedScene> Wardrobe { get; set; } =
            new Godot.Collections.Dictionary<string, PackedScene>();

        [Signal] public delegate void SwappedEventHandler(string prop, string skin);

        readonly Dressing _dressing = new Dressing();

        readonly Dictionary<TableProp, Node3D> _standing = new Dictionary<TableProp, Node3D>();

        Game.Tray.DiceTray _tray;

        public Dressing Wearing => _dressing;

        public override void _Ready()
        {
            if (TrayPath != null && !TrayPath.IsEmpty)
                _tray = GetNodeOrNull<Game.Tray.DiceTray>(TrayPath);

            foreach (TableProp prop in System.Enum.GetValues<TableProp>())
            {
                if (!Props.TryGetValue(prop.Word(), out NodePath where) || where.IsEmpty) continue;

                Node3D standing = GetNodeOrNull<Node3D>(where);

                if (standing == null)
                {
                    GD.PushWarning($"dressed: there is no {prop.Word()} at '{where}', so it " +
                                   "cannot be swapped for another one");
                    continue;
                }

                _standing[prop] = standing;
            }

            GD.Print($"dressed {_standing.Count} prop(s) can be swapped, " +
                     $"{Wardrobe.Count} in the wardrobe" +
                     (_tray == null ? "" : $", the tray wearing {_tray.SkinName}"));
        }

        public static string Hanger(TableProp prop, string skin) => prop.Word() + "/" + skin;

        // false when nothing changed: the prop is already wearing it, or there is no such version
        public bool Wear(TableProp prop, string skin)
        {
            if (!_dressing.Wear(prop, skin)) return false;

            bool swapped = Swap(prop, _dressing.Wearing(prop));

            if (!swapped)
            {
                // the loadout still says so. A prop with no art for that skin yet is a prop the
                // table will dress the day the art lands, and nothing else about it changes
                GD.Print($"dressed {prop.Word()} is down as '{skin}' and there is nothing to put " +
                         "on it yet");
            }

            EmitSignal(SignalName.Swapped, prop.Word(), _dressing.Wearing(prop));

            return true;
        }

        bool Swap(TableProp prop, string skin)
        {
            // the one that wears its own skins; a felt is not a different tray, it is this one
            if (prop == TableProp.Tray && _tray != null) return _tray.Wear(skin);

            if (!Wardrobe.TryGetValue(Hanger(prop, skin), out PackedScene version) ||
                version == null)
                return false;

            if (!_standing.TryGetValue(prop, out Node3D standing) || !IsInstanceValid(standing))
                return false;

            var wearing = version.Instantiate<Node3D>();

            if (wearing == null)
            {
                GD.PushError($"dressed: '{Hanger(prop, skin)}' is not a 3D scene, so the " +
                             $"{prop.Word()} keeps the one it has");
                return false;
            }

            // IN THE SAME PLACE, under the same name. Where a prop stands is the table's business
            // and never the prop's, which is what has made every object on this table swappable
            wearing.Name = standing.Name;
            wearing.Transform = standing.Transform;

            Node parent = standing.GetParent();
            int at = standing.GetIndex();

            parent.RemoveChild(standing);
            standing.QueueFree();

            parent.AddChild(wearing);
            parent.MoveChild(wearing, at);

            _standing[prop] = wearing;

            GD.Print($"dressed {prop.Word()} is wearing '{skin}'");

            return true;
        }

        public void Plainly()
        {
            foreach (TableProp prop in System.Enum.GetValues<TableProp>())
                Wear(prop, Dressing.Plain);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() => _dressing.ToString();
    }
}
