using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Content.Saves;
using Core.Localization;
using Game.Localization;

namespace Game.Room
{
    // THE ROOM (R3-R5, THE_TABLE.md section 6).
    //
    // The room is the entire UI. There are no menu screens anywhere in this game - not a main menu,
    // not a save menu, not a character screen, not a level-up modal, not an inventory panel. Every
    // place a normal RPG opens one, this has an object, and Prop is the list.
    //
    // THE TABLE SITS INSIDE IT. The room is added AROUND the table that already works, rather than
    // the table being moved into a new scene, so nothing that was playable before this milestone
    // stopped being playable during it.
    //
    // What this node does is small on purpose: it owns the light, it raycasts a click onto whatever
    // furniture is under it, and it says what touching each thing means. Everything a touch reaches
    // is another object - the sheet on the table, a box on the shelf, the door - and never a scene
    // pushed over the top.
    [GlobalClass]
    public partial class Room : Node3D
    {
        [Export] public NodePath TablePath { get; set; } = "Table";

        [Export] public NodePath SheetPath { get; set; }

        // where saves live. Empty looks beside the executable, which is where the game writes them
        [Export] public string SavesFolder { get; set; } = "";

        public const string FolderName = "saves";

        // THE ONE LIGHT IN THE ROOM. Exported and actually applied - until the eye check these
        // two were declared here, documented as the room owning the light, and read by nothing.
        [Export] public NodePath LampPath { get; set; } = "Lamp";

        [Export] public Color Lamp { get; set; } = new Color("#ffd9a8");

        [Export] public float LampEnergy { get; set; } = 3.2f;

        [Signal] public delegate void TouchedEventHandler(int prop);

        // the door is the quit, and quitting asks - it is the one irreversible object in the room
        [Signal] public delegate void LeavingEventHandler();

        readonly List<Furniture> _furniture = new List<Furniture>();

        readonly ILocalizer _text = new GodotLocalizer();

        Node3D _table;

        Game.Sheet.Sheet _sheet;

        Furniture _under;

        SaveShelf _shelf;

        public IReadOnlyList<Furniture> Furnishings => _furniture;

        public SaveShelf Shelf => _shelf;

        // which box the player last took down, so a resumed game knows what it resumed
        public Box Held { get; private set; }

        public override void _Ready()
        {
            _table = TablePath != null && !TablePath.IsEmpty ? GetNodeOrNull<Node3D>(TablePath) : null;

            _sheet = SheetPath != null && !SheetPath.IsEmpty
                ? GetNodeOrNull<Game.Sheet.Sheet>(SheetPath)
                : null;

            Gather();

            _shelf = SaveShelf.Read(Saves());

            GD.Print("");
            GD.Print($"room    {_furniture.Count} objects, and no menu screens anywhere");

            foreach (Furniture one in _furniture)
                GD.Print($"        {one.Is.Word(),-12} {one.Called,-20} - replaces {one.Is.Replaces()}");

            GD.Print($"saves   {_shelf} in {Saves()}");

            foreach (Box box in _shelf.Boxes) GD.Print($"        {box}");

            foreach (Content.Schema.ContentProblem problem in _shelf.Problems)
                GD.PushWarning("saves: " + problem);

            OwnTheLight();

            Stand();

            if (_table == null)
                GD.PushWarning($"room: no table at '{TablePath}' - the room is furniture with " +
                               "nothing to play on");
        }

        // ONE LIGHT SOURCE, AND THE ROOM IS THE ONE THAT HAS IT (THE_TABLE.md section 6).
        //
        // table.tscn carries a sun and a world environment of its own, and it has to: it opens and
        // plays on its own, and every check script since B0 has run against it that way. Inside a
        // room those are a SECOND sun and a SECOND sky, both additive, and the eye check saw
        // exactly what that looks like - "the lighting is way too bright on the grid map", because
        // the board sits directly under the lamp and was getting a full directional light on top
        // of it.
        //
        // So the room turns them off as it takes the table in. The table is not changed and does
        // not know: open table.tscn by itself and its own sun is still there.
        void OwnTheLight()
        {
            var lamp = LampPath != null && !LampPath.IsEmpty
                ? GetNodeOrNull<OmniLight3D>(LampPath)
                : null;

            if (lamp != null)
            {
                lamp.LightColor = Lamp;
                lamp.LightEnergy = LampEnergy;
            }
            else
            {
                GD.PushWarning($"room: no lamp at '{LampPath}' - the room is lit by its ambient " +
                               "alone, which is not a room at night, it is a fog");
            }

            if (_table == null) return;

            int doused = 0;

            foreach (Node child in _table.GetChildren())
            {
                if (child is DirectionalLight3D sun && sun.Visible)
                {
                    sun.Visible = false;
                    doused++;
                }

                if (child is WorldEnvironment sky && sky.Environment != null)
                {
                    sky.Environment = null;
                    doused++;
                }
            }

            GD.Print($"room    one lamp at {LampEnergy:0.0}, and {doused} of the table's own light " +
                     "source(s) put out - a table indoors is lit by the room it is in");
        }

        // every Furniture anywhere under the room, so the scene decides where things stand and this
        // file never holds a list that could disagree with it
        void Gather()
        {
            _furniture.Clear();

            Collect(this);

            foreach (Furniture one in _furniture) one.Touched += OnTouched;

            // the room is meant to be the whole interface, and an object that is not there is a
            // thing the player cannot reach any other way - there is no menu to fall back on
            foreach (Prop prop in Enum.GetValues<Prop>())
                if (_furniture.All(f => f.Is != prop))
                    GD.PushWarning($"room: there is no {prop.Word()} in this room, so " +
                                   $"{prop.Replaces()} is unreachable - the room IS the interface");
        }

        void Collect(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is Furniture one) _furniture.Add(one);

                Collect(child);
            }
        }

        public override void _ExitTree()
        {
            foreach (Furniture one in _furniture) one.Touched -= OnTouched;
        }

        public string Saves()
        {
            if (SavesFolder.Length > 0) return SavesFolder;

            return OS.HasFeature("editor")
                ? Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..",
                                                FolderName))
                : Path.Combine(Path.GetDirectoryName(OS.GetExecutablePath()) ?? ".", FolderName);
        }

        public void Reread()
        {
            _shelf = SaveShelf.Read(Saves());

            Stand();
        }

        // THE SHELF ACCUMULATES (R4). Every save on disk is a box, and a finished one carries its
        // trophy. Nothing here is a separate cosmetic store: the boxes ARE the files, re-read from
        // the folder each time, so the room cannot claim a campaign the save folder does not have.
        void Stand()
        {
            Furniture shelf = _furniture.FirstOrDefault(f => f.Is == Prop.Shelf);

            if (shelf == null) return;

            foreach (Node standing in shelf.GetChildren())
                if (standing.Name.ToString().StartsWith("Box", StringComparison.Ordinal))
                    standing.QueueFree();

            float along = -shelf.Size.X * 0.5f + 0.06f;

            foreach (Box box in _shelf.Boxes)
            {
                if (along > shelf.Size.X * 0.5f - 0.04f) break;

                var spine = new MeshInstance3D
                {
                    Name = "Box" + box.File,
                    Mesh = new BoxMesh { Size = new Vector3(0.035f, 0.22f, 0.17f) },
                    MaterialOverride = new StandardMaterial3D
                    {
                        AlbedoColor = Colour(box),
                        Roughness = 0.95f,
                    },
                    Position = new Vector3(along, 0.14f, 0f),
                };

                shelf.AddChild(spine);

                // the trophy lands on the box, not beside it - it belongs to that campaign
                if (box.Finished)
                    spine.AddChild(new MeshInstance3D
                    {
                        Name = "Trophy",
                        Mesh = new SphereMesh { Radius = 0.016f, Height = 0.032f },
                        MaterialOverride = new StandardMaterial3D
                        {
                            AlbedoColor = new Color("#c9a227"),
                            Metallic = 0.8f,
                            Roughness = 0.35f,
                        },
                        Position = new Vector3(0f, 0.13f, 0f),
                    });

                along += 0.042f;
            }
        }

        // each campaign's boxes look alike on the shelf, so a row of them reads as a row of one
        // campaign's chapters rather than as a colour chart
        static Color Colour(Box box)
        {
            int hash = 17;

            foreach (char c in box.Campaign) hash = hash * 31 + c;

            var rng = new RandomNumberGenerator { Seed = (ulong)Math.Abs(hash) };

            return Color.FromHsv(rng.Randf(), 0.35f, rng.RandfRange(0.35f, 0.6f));
        }

        // a finished campaign adds a box. The versioned JSON P6 writes IS the box; there is no
        // second record of what you have played that could drift from the folder.
        public Box Keep(SaveGame save, string named = null)
        {
            if (save == null) return null;

            string folder = Saves();

            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception could)
            {
                GD.PushError($"room: the shelf has nowhere to stand - {could.Message}");
                return null;
            }

            string file = named ?? $"{Stamp()}_{(save.Campaign.Length > 0 ? save.Campaign : "game")}" +
                                   SaveShelf.Extension;

            Content.Schema.ContentProblem wrote =
                SaveWriter.To(Path.Combine(folder, file), save);

            if (wrote != null)
            {
                GD.PushError("room: " + wrote);
                return null;
            }

            GD.Print($"room    a box goes on the shelf - {file}");

            Reread();

            return _shelf.Of(file);
        }

        // sortable and human-readable, so the folder reads in the order the shelf stands in
        static string Stamp() => DateTime.Now.ToString("yyyyMMdd_HHmmss");


        // ---- touching things ---------------------------------------------------------------

        // Each object IS the thing; none of them opens a menu. What a touch does is written here in
        // one place, so the claim "you can play the whole game touching only objects" is a list you
        // can read rather than a promise.
        void OnTouched(int prop)
        {
            var which = (Prop)prop;

            GD.Print($"room    {which.Word()} - {which.Replaces()}");

            switch (which)
            {
                case Prop.Shelf:
                    // a save is a box you pick up, not a slot you select (R4)
                    Held = _shelf.Boxes.FirstOrDefault();

                    GD.Print(Held == null
                        ? "        the shelf is empty - nothing has been finished yet"
                        : $"        took down {Held}");
                    break;

                case Prop.Sheet:
                    // the sheet is on the table and is touched there; this brings the eye to it
                    GD.Print($"        {_sheet?.Character.ToString() ?? "no sheet on this table"}");
                    break;

                case Prop.Door:
                    // the one irreversible object in the room, so it asks
                    EmitSignal(SignalName.Leaving);
                    break;

                default:
                    break;
            }

            EmitSignal(SignalName.Touched, prop);
        }

        // take a box down and put what is inside it on the table. The versioned JSON P6 writes is
        // what is in the box; there is no second record of "campaigns you finished" to drift from it
        public Box TakeDown(string file)
        {
            Box box = _shelf.Of(file) ?? _shelf.Boxes.FirstOrDefault();

            if (box == null)
            {
                GD.Print("room    nothing on the shelf to take down");
                return null;
            }

            Held = box;

            if (box.Save?.Sheet != null) _sheet?.Take(box.Save.Sheet);

            GD.Print($"room    {box.File} is open - {box.Save}");

            return box;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseMotion motion) { Hover(motion.Position); return; }

            if (!@event.IsActionPressed("place_piece") || @event is not InputEventMouse mouse) return;

            Furniture one = Under(mouse.Position);

            if (one == null) return;

            GetViewport().SetInputAsHandled();

            one.Touch();
        }

        void Hover(Vector2 at)
        {
            Furniture one = Under(at);

            if (ReferenceEquals(one, _under)) return;

            _under?.Light(false);
            _under = one;
            _under?.Light(true);
        }

        Furniture Under(Vector2 at)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return null;

            var query = PhysicsRayQueryParameters3D.Create(
                camera.ProjectRayOrigin(at),
                camera.ProjectRayOrigin(at) + camera.ProjectRayNormal(at) * Reach);

            query.CollideWithAreas = false;

            Godot.Collections.Dictionary hit = GetWorld3D()?.DirectSpaceState?.IntersectRay(query);

            if (hit == null || hit.Count == 0) return null;

            var what = hit["collider"].As<GodotObject>();

            foreach (Furniture one in _furniture)
                if (one.Owns(what)) return one;

            return null;
        }

        const float Reach = 20f;

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"room: {_furniture.Count} objects, {_shelf?.Count ?? 0} boxes on the shelf";
    }
}
