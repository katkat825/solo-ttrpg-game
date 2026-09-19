using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Classes;
using Content.Saves;
using Content.Sheet;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Game.Access;
using Game.Localization;
using Game.Room;
using Game.Saves;
using Game.Sheet;

// Game.Room is both a namespace and the type; alias so Room binds to the node
using RoomNode = Game.Room.Room;

namespace Game.Diagnostics
{
    // BOOTS THE REAL ROOM AND PLAYS FROM COLD TO A DUNGEON WITHOUT TOUCHING A MENU (R5).
    //
    // R5's verify list is "play from cold boot to an active dungeon touching only objects; grep the
    // build for menu/panel scenes and find none". The first half needs a person for the feel of it,
    // but every mechanical claim underneath is checkable and this checks them:
    //
    //   - THE DELETIONS HOLD. Nothing in the build is a menu, a panel, a modal or an HUD - not a
    //     scene, not a script. This is the one that will catch a future milestone reaching for one,
    //     and it is the whole reason R5 is a check and not a paragraph
    //   - every object THE_TABLE.md section 6 promises is actually in the room, and has a name in
    //     this locale. An object that is missing is a thing the player cannot reach at all, because
    //     there is no menu to fall back on
    //   - the sheet fills in by touching blanks, with no Confirm button anywhere in its surface
    //   - THE POOL THROWN ON THE TRAY IS BUILT FROM WHAT IS WRITTEN ON THE SHEET (R0's own verify):
    //     change a line and the dice change
    //   - a box on the shelf is a real save file, and taking it down puts its sheet on the table
    //
    // It instances the real room.tscn, which instances the real table.tscn, for the reason every
    // check in this project does: the thing measured has to be the thing played.
    //
    // Everything printed is developer diagnostic, exempt from localization.
    public partial class RoomCheck : HeadlessCheck
    {
        protected override string Subject => "room";

        [Export] public PackedScene TheRoom { get; set; }

        // where to look for a menu that should not exist
        [Export] public string Project { get; set; } = "res://";

        // A SHELF OF ITS OWN, not the player's. This check puts a box on the shelf to prove one
        // can go there, and it must never do that in somebody's real save folder.
        [Export] public bool UseAScratchShelf { get; set; } = true;

        readonly ILocalizer _text = new GodotLocalizer();

        public override void _Ready()
        {
            NoMenusAnywhere();

            Node3D room = Stand();

            if (room == null) { Finish(); return; }

            var theRoom = room as RoomNode;

            if (theRoom == null)
            {
                Problem("the room scene's root is not a Room");
                Finish();
                return;
            }

            EveryObjectIsThere(theRoom);

            OneLightInTheRoom(room);

            EveryBindingConnects(room);

            EverySignalIsHeard();

            EverythingOnTheTableIsInThePicture(room);

            EveryWordIsBigEnoughToRead(room);

            YouCanLeanInAndRead(room);

            Node sheet = room.GetNodeOrNull("Table/Sheet");

            FillItIn(theRoom, sheet as Game.Sheet.Sheet);

            TheDiceFollowTheSheet();

            TheShelf(theRoom);

            SavingAsYouPlay(theRoom);

            GD.Print("");
            Finish();
        }

        // AUTOSAVE IS EVENT-BASED, AND CLOSING THE DOOR IS ONE OF THE EVENTS.
        //
        // Until this, the only production write was a finished campaign - so a player who closed
        // the game mid-dungeon lost the evening, and the machinery to stop that was all built and
        // unreachable. Three things are worth a machine here: that an event writes, that an event
        // with nothing new behind it does NOT (or the shelf fills with identical files), and that
        // the door writes whatever is in front of it on the way past.
        void SavingAsYouPlay(RoomNode room)
        {
            GD.Print("");

            int stood = room.Shelf.Count;

            // nothing is being played yet, so there is nothing any event could write down
            if (room.Wrote(Autosave.When.Fought) != null)
                Problem("a fight ended with nothing on the table and the room wrote a save anyway");

            if (room.Unsaved)
                Problem("the room says there is unsaved work and nothing has been played");

            // now something is
            var played = Finished();

            room.Snapshot = () => played;
            room.Happened();

            if (!room.Unsaved)
                Problem("something happened and the room does not think there is anything to write");

            Box first = room.Wrote(Autosave.When.Fought);

            if (first == null)
            {
                Problem("a fight ended on a game in progress and nothing was written - this is the " +
                        "evening a player loses");
                return;
            }

            GD.Print($"saving  a fight ended and {first.File} went up");

            if (room.Unsaved)
                Problem("a save was written and the room still says there is unsaved work");

            // AND THE SAME EVENT AGAIN WRITES NOTHING. A save per frame is how a folder of
            // snapshots becomes unusable, and the reload tabs read that folder
            if (room.Wrote(Autosave.When.Fought) != null)
                Problem("nothing happened and a second identical save was written anyway");

            // the door never skips, because it is the one that cannot be gone back for
            Box onTheWayOut = room.SaveNow();

            if (onTheWayOut == null)
                Problem("the player asked to save with nothing new and got nothing - a manual save " +
                        "that is sometimes a no-op is indistinguishable from a broken one");

            if (room.Shelf.Count <= stood)
                Problem($"{room.Shelf.Count - stood} campaign(s) were added by saving and the shelf " +
                        "does not stand any more than it did - the shelf IS the folder");

            GD.Print($"saving  {room.Saving}");
            GD.Print($"shelf   {room.Shelf.Count} standing on it now");
        }

        Node3D Stand()
        {
            if (TheRoom == null)
            {
                Problem("no room scene was given to stand in");
                return null;
            }

            var room = TheRoom.Instantiate<Node3D>();

            if (room == null)
            {
                Problem("the room scene did not instance");
                return null;
            }

            // set BEFORE the tree readies it, which is the only moment an export can still be told
            // something; after AddChild the room has already read its shelf
            if (UseAScratchShelf && room is RoomNode theRoom)
                theRoom.SavesFolder = Scratch();

            AddChild(room);

            return room;
        }


        // a fresh empty folder every run, so the check never reads a box it left behind last time
        // and never reads one somebody was actually playing
        string Scratch()
        {
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "room_check_" + System.IO.Path.GetRandomFileName());

            try
            {
                System.IO.Directory.CreateDirectory(folder);
            }
            catch (Exception could)
            {
                Problem("nowhere to stand a scratch shelf - " + could.Message);
                return "";
            }

            return folder;
        }


        // ---- the deletions ------------------------------------------------------------------

        // "No menu screens, anywhere - this is a hard rule, not a preference" (THE_TABLE.md
        // section 6). A rule nobody checks is a rule that lasts until the first awkward milestone.
        public static readonly IReadOnlyList<string> Forbidden =
            new[] { "menu", "panel", "modal", "hud", "screen_", "_screen" };

        // the diagnostics ARE developer tooling and were never the player's interface; the one
        // exception, named rather than pattern-matched, so a new exception has to be argued for
        public static readonly IReadOnlyList<string> Excused = new[] { "res://Diagnostics/" };

        // THE ROOM HAS ONE LIGHT, AND IT IS THE LAMP (fixes_needed.md #1, and it came BACK).
        //
        // table.tscn ships its own Sun (a DirectionalLight3D) and its own WorldEnvironment so it can
        // be opened and checked on its own. Room.OwnTheLight() puts both out when the table is taken
        // into the room, because a table indoors is lit by the room it is in, not by a sun of its
        // own. Leave that sun lit and it washes the dark map walls to the brightness of the felt -
        // exactly the report that opened the third eye check, and exactly the bug the FIRST eye check
        // already fixed once. It came back the moment the scenes were rewritten, which is why it now
        // has a machine on it: any rewrite that reintroduces a sun, or nests it where OwnTheLight
        // cannot reach, fails HERE instead of in an eye check three passes later.
        //
        // The scan is the WHOLE room subtree (owned:false reaches into the instanced table), not the
        // table's direct children the way OwnTheLight walks them.
        //
        // AND IT COUNTS EVERY KIND OF LIGHT, not only suns. "One light source" is the rule; asking
        // only about DirectionalLight3D asked about the one bug that had already happened, and a
        // second lamp or a spot over the screen would have walked straight past a check named after
        // the rule it was not enforcing. One lit light in the room, whatever class it is.
        void OneLightInTheRoom(Node3D room)
        {
            GD.Print("");

            if (room == null) { Problem("no room to check the light of"); return; }

            var lamps = new List<Light3D>();

            foreach (Node node in room.FindChildren("*", nameof(Light3D),
                                                    recursive: true, owned: false))
                if (node is Light3D light && light.Visible) lamps.Add(light);

            int lit = lamps.Count;

            foreach (Light3D light in lamps.Skip(1))
                Problem($"a second light is lit in the room ('{light.Name}', a " +
                        $"{light.GetType().Name}) - the room has ONE light source, and lighting " +
                        "it twice washes the dark map walls to the brightness of the felt, which " +
                        "has been found by eye and fixed twice already");

            if (lit == 0)
                Problem("nothing in the room is lit at all - the room is its ambient alone, " +
                        "which is not a room at night, it is a fog");

            foreach (Light3D light in lamps.Take(1))
                GD.Print($"light   the one light is '{light.Name}', a {light.GetType().Name} " +
                         $"at energy {light.LightEnergy:0.00}");

            int envs = 0;

            foreach (Node node in room.FindChildren("*", nameof(WorldEnvironment),
                                                    recursive: true, owned: false))
                if (node is WorldEnvironment sky && sky.Environment != null) envs++;

            // the room keeps its own; the table's must have been cleared, so more than one is the
            // table's brought indoors and not put away
            if (envs > 1)
                Problem($"{envs} world environments are live at once - the table brought its own " +
                        "indoors and it was not cleared");

            GD.Print($"room    {lit} light(s) lit, {envs} world environment(s) live - " +
                     "want 1 light and 1 environment");
        }

        // ---- is it in the picture ---------------------------------------------------------------

        // EVERYTHING STANDING ON THE TABLE IS INSIDE THE CAMERA'S FRAME.
        //
        // The camera does not move, so this is arithmetic - and it was being done by hand, once per
        // eye check, and forgotten in between. Four passes found the same bug four times: the A3
        // character sheet off the right of the picture, the hint cord off the left, a speech card
        // growing off its own corner, and the whole stack of cards you are carrying sitting 12 cm
        // below the bottom edge for as long as it had existed. None of them was reachable by
        // anything headless, and each one cost an eye check that could have been spent on taste.
        //
        // WHAT FAILS AND WHAT ONLY REPORTS is the one judgement here. The table's own objects are
        // composition that three eye-check passes were spent settling, so one of them leaving the
        // frame is a regression and fails. The room's Furniture is different by construction: the
        // room is BIGGER than the picture on purpose - you walk to the bookcase - so a prop out of
        // frame is the open framing question and is reported rather than failed.
        void EverythingOnTheTableIsInThePicture(Node3D room)
        {
            GD.Print("");

            if (room == null) { Problem("no room to frame"); return; }

            Camera3D eye = room.FindChildren("*", nameof(Camera3D), recursive: true, owned: false)
                               .OfType<Camera3D>()
                               .FirstOrDefault();

            if (eye == null)
            {
                Problem("there is no camera in the room, so nothing in it can be seen at all");
                return;
            }

            Framing frame = Framing.Of(eye);

            GD.Print($"frame   {frame}");

            var table = room.GetNodeOrNull<Node3D>("Table");

            if (table == null) { Problem("there is no table in the room to stand things on"); return; }

            Aabb felt = Tabletop(table);

            if (felt.Size == Vector3.Zero)
            {
                Problem("the table has no tabletop, so there is no surface to stand anything on");
                return;
            }

            float surface = felt.Position.Y + felt.Size.Y;
            float edge = frame.NearEdgeOn(surface);

            GD.Print($"felt    {felt.Size.X:0.00} x {felt.Size.Z:0.00} m, surface at y {surface:0.000} - " +
                     $"the bottom of the picture crosses it at z {edge:0.000}, " +
                     "and anything nearer than that is below the edge");

            // the table's own objects: everything it stands on itself, minus the things that are
            // not objects at all. A camera, a light and an environment are not on the table
            foreach (Node child in table.GetChildren())
            {
                if (child is Camera3D or Light3D or WorldEnvironment) continue;

                if (child is not Node3D thing) continue;

                float margin = frame.Margin(thing.GlobalPosition);

                if (margin < 0f)
                    Problem($"'{thing.Name}' stands {-margin * 1000f:0} mm outside the picture at " +
                            $"{thing.GlobalPosition} - a thing on this table that the fixed camera " +
                            "cannot see is a thing the player has no other way to reach");

                GD.Print($"        {thing.Name,-12} {margin * 1000f,7:0} mm inside the frame");
            }

            // and the room's own props, which are allowed to be outside it
            var away = new List<string>();

            foreach (Node node in room.FindChildren("*", nameof(Game.Room.Furniture),
                                                    recursive: true, owned: false))
            {
                if (node is not Game.Room.Furniture prop) continue;

                if (!frame.Holds(prop.GlobalPosition)) away.Add(prop.Is.Word());
            }

            if (away.Count > 0)
                Caution($"{away.Count} of the room's own objects are outside the picture " +
                        $"({string.Join(", ", away)}) - the room is bigger than the frame on " +
                        "purpose, but a prop a mouse can never put a cursor on is one only the " +
                        "keyboard can reach. How much room is in frame is an eye check");
        }

        // the biggest flat thing the table stands on, found rather than named, so the surface this
        // measures against is the one the game actually draws
        static Aabb Tabletop(Node3D table)
        {
            var biggest = new Aabb();

            float most = 0f;

            foreach (Node child in table.GetChildren())
            {
                if (child is not MeshInstance3D mesh) continue;

                Aabb box = mesh.GlobalTransform * mesh.GetAabb();

                float area = box.Size.X * box.Size.Z;

                if (area <= most) continue;

                most = area;
                biggest = box;
            }

            return biggest;
        }


        // CAN ANY OF IT ACTUALLY BE READ. Not "is it in the picture" - the sweep above already
        // holds that, and every word in this game passed it while being too small to read.
        void EveryWordIsBigEnoughToRead(Node3D room)
        {
            GD.Print("");

            Framing frame = InThePicture.SeenThrough(room);

            var table = room?.GetNodeOrNull<Node3D>("Table");

            IReadOnlyList<string> sizes =
                InThePicture.TooSmallToRead(table, frame, out int read, out float worst);

            GD.Print($"words   {read} line(s) on the table at {Framing.Screen:0}p; the floor is " +
                     $"{Legible.Least:0} px of em, and a line under it sitting back has to be one " +
                     "you can pick up");

            foreach (string one in sizes) GD.Print("        " + one);

            int onlyInHand = sizes.Count(one => one.EndsWith("only in hand"));

            if (onlyInHand > 0)
                Caution($"{onlyInHand} kind(s) of writing on this table can only be read with the " +
                        "thing in your hands - which is correct for a sheet of A4 and wrong for " +
                        "anything that arrives and is swept, so every one of them has to be a " +
                        "thing you can actually lean over or pick up");

            if (read > 0 && worst < Legible.Least)
                Problem($"the smallest words on this table are {worst:0.0} px tall on a " +
                        $"{Framing.Screen:0}p screen even held up in front of you, and " +
                        $"{Legible.Least:0} is the floor - the text dial multiplies up from here " +
                        "rather than rescuing it");
        }


        // LEANING IN IS THE OTHER HALF OF THE SENTENCE ABOVE, AND NOBODY HAD EVER CHECKED IT.
        //
        // Half the writing on this table is only legible with the thing held up in front of you.
        // That is a defensible design - it is how you read a sheet of paper - but only while
        // leaning in actually works, and while a player can find out that it does. Otherwise
        // "pick it up to read it" is a sentence in a comment and the game is simply unreadable,
        // which is what it looked like from outside.
        //
        // So: does the camera move when it is asked to, does it end up near enough to read at, and
        // does it come back. Three assertions and they are all about the same claim.
        void YouCanLeanInAndRead(Node3D room)
        {
            GD.Print("");

            var eye = room?.GetNodeOrNull<Leaning>("Table/Leaning");

            if (eye == null)
            {
                Problem("there is nothing in this room that leans, so every word on the table " +
                        "that is only readable in hand is unreadable");
                return;
            }

            Camera3D camera = InThePicture.CameraIn(room);

            if (camera == null) { Problem("no camera in the room to lean"); return; }

            Vector3 back = camera.GlobalPosition;

            var sheet = room.GetNodeOrNull<Node3D>("Table/Sheet");

            if (sheet == null) { Problem("no sheet on the table to lean over"); return; }

            eye.LeanOver(sheet.GlobalPosition);

            // a lean is a move over time; run it out rather than waiting a frame
            Settle(eye);

            float near = (camera.GlobalPosition - sheet.GlobalPosition).Length();

            GD.Print($"lean    sitting back is {(back - sheet.GlobalPosition).Length():0.00} m " +
                     $"from the sheet, leaned in is {near:0.00} m");

            if (near >= (back - sheet.GlobalPosition).Length())
                Problem("leaning over the sheet did not bring the camera any closer to it - the " +
                        "one way to read what is written on this table does nothing");

            if (near > Lean.Held * 1.5f)
                Problem($"leaned all the way in the camera is still {near:0.00} m from the sheet, " +
                        $"and a thing is read at about {Lean.Held:0.00} m");

            // and the mat's own zoom, which is the other move and has its own state behind it
            if (eye.Window == null)
            {
                Problem("the camera has no window on the mat, so zooming does nothing at all");
            }
            else
            {
                eye.Back();
                Settle(eye);

                float was = eye.Window.Zoom;

                eye.Zoom(0.5f);

                if (Mathf.IsEqualApprox(eye.Window.Zoom, was))
                    Problem($"zooming in did not change the window ({eye.Window}) - a player who " +
                            "presses the zoom key gets nothing");

                GD.Print($"zoom    {eye.Window}");
            }

            eye.Back();
            Settle(eye);
        }

        // a lean takes about half a second of real time and a check has no real time, so it is
        // wound forward by hand
        static void Settle(Leaning eye)
        {
            for (int step = 0; step < 120 && eye.Moving; step++) eye._Process(0.02);
        }


        // ---- does every binding connect ---------------------------------------------------------

        // A NODEPATH THAT POINTS AT NOTHING IS A FEATURE THAT SILENTLY DOES NOTHING.
        //
        // Half of what this room is wired out of is exported NodePaths set in the scene - the room
        // finds the table through one, the fight finds the board and the tray through two more, the
        // companion finds the screen it looks at through a fourth. Every one of them is a string,
        // none of them is checked by the compiler, and a rename anywhere leaves a node that boots
        // fine, warns at most once, and quietly stops doing its job. That is the "case exists but
        // does nothing" class, and it is the cheapest of the four to make impossible.
        //
        // An EMPTY path is not a failure: half of these are optional and say so at their own line -
        // a companion with no tray simply stares ahead. A path that was SET and resolves to nothing
        // is always a mistake.
        void EveryBindingConnects(Node3D room)
        {
            GD.Print("");

            if (room == null) return;

            int bound = 0;

            var broken = new List<string>();

            Bindings(room, ref bound, broken);

            foreach (string one in broken)
                Problem($"{one} - a binding that resolves to nothing is a feature that boots " +
                        "fine and does nothing");

            GD.Print($"wiring  {bound} binding(s) set in the shipped scenes, " +
                     $"{broken.Count} pointing at nothing");
        }

        void Bindings(Node node, ref int bound, List<string> broken)
        {
            foreach (Godot.Collections.Dictionary property in node.GetPropertyList())
            {
                if ((Variant.Type)(int)property["type"] != Variant.Type.NodePath) continue;

                var named = (string)property["name"];

                var path = node.Get(named).AsNodePath();

                if (path == null || path.IsEmpty) continue;

                bound++;

                if (node.GetNodeOrNull(path) == null)
                    broken.Add($"{node.GetPath()}.{named} points at '{path}'");
            }

            foreach (Node child in node.GetChildren()) Bindings(child, ref bound, broken);
        }


        // A SIGNAL NOBODY LISTENS TO IS A FEATURE THAT FIRES INTO THE AIR.
        //
        // The other half of the binding sweep above, and the half that found more. A NodePath that
        // points at nothing at least warns when it is read; a signal with no subscriber does
        // exactly what a working one does, from the emitting side, forever. The room emits SatDown
        // when you open a campaign book and nothing has ever heard it - which is why the shipped
        // room boots into a hard-coded fight and a campaign cannot be started by picking up its
        // book.
        //
        // Read off the source rather than the live tree, because a signal connected in a scene is
        // still connected and a signal connected on a node that is not standing tonight is still
        // wired. A caution and not a failure: an unheard signal is sometimes an honest seam left
        // open early, and the point is that nobody can leave one by accident again.
        void EverySignalIsHeard()
        {
            GD.Print("");

            var declared = new Dictionary<string, string>();
            var heard = new HashSet<string>();

            Source(Project, declared, heard);

            var deaf = new List<string>();

            foreach (KeyValuePair<string, string> one in declared)
                if (!heard.Contains(one.Key)) deaf.Add($"{one.Value}.{one.Key}");

            deaf.Sort();

            GD.Print($"signal  {declared.Count} declared in the build, {deaf.Count} with nobody " +
                     "listening");

            foreach (string one in deaf) GD.Print($"        {one} is emitted and never heard");

            if (deaf.Count > 0)
                Caution($"{deaf.Count} signal(s) fire into the air: {string.Join(", ", deaf)} - " +
                        "each one is a thing the game does that nothing downstream reacts to");
        }

        static readonly System.Text.RegularExpressions.Regex Declares =
            new System.Text.RegularExpressions.Regex(
                @"\[Signal\][^;]*?delegate\s+void\s+(\w+)EventHandler");

        void Source(string folder, Dictionary<string, string> declared, HashSet<string> heard)
        {
            using DirAccess dir = DirAccess.Open(folder);

            if (dir == null) return;

            foreach (string file in dir.GetFiles())
            {
                if (!file.EndsWith(".cs") && !file.EndsWith(".tscn")) continue;

                string path = folder.TrimSuffix("/") + "/" + file;

                if (Excused.Any(e => path.StartsWith(e, StringComparison.Ordinal))) continue;

                string text = Godot.FileAccess.GetFileAsString(path);

                if (text.Length == 0) continue;

                foreach (System.Text.RegularExpressions.Match found in Declares.Matches(text))
                    declared[found.Groups[1].Value] = file.TrimSuffix(".cs");

                Listening(text, heard);
            }

            foreach (string inside in dir.GetDirectories())
            {
                if (inside.StartsWith(".") || inside == "bin" || inside == "obj") continue;

                Source(folder.TrimSuffix("/") + "/" + inside, declared, heard);
            }
        }

        // a C# event subscription, a Connect by name, or a scene-level connection
        static void Listening(string text, HashSet<string> heard)
        {
            foreach (System.Text.RegularExpressions.Match found in
                     System.Text.RegularExpressions.Regex.Matches(
                         text, @"\.(\w+)\s*\+=|Connect\s*\(\s*\w*\.?SignalName\.(\w+)|" +
                               @"signal\s*=\s*""(\w+)"""))
            {
                for (int group = 1; group <= 3; group++)
                    if (found.Groups[group].Success) heard.Add(found.Groups[group].Value);
            }
        }


        void NoMenusAnywhere()
        {
            var found = new List<string>();

            Walk(Project, found);

            GD.Print($"menus   {(found.Count == 0 ? "none anywhere in the build" : string.Join(", ", found))}");

            foreach (string file in found)
                Problem($"{file} is a menu, a panel or a modal, and this game has none - " +
                        "THE_TABLE.md section 6 is a hard rule. Find the object that panel " +
                        "should have been and put it in the room");
        }

        void Walk(string folder, List<string> found)
        {
            using DirAccess dir = DirAccess.Open(folder);

            if (dir == null) return;

            foreach (string file in dir.GetFiles())
            {
                if (!file.EndsWith(".tscn") && !file.EndsWith(".cs")) continue;

                string path = folder.TrimSuffix("/") + "/" + file;

                if (Excused.Any(e => path.StartsWith(e, StringComparison.Ordinal))) continue;

                string name = file.ToLowerInvariant();

                if (Forbidden.Any(bad => name.Contains(bad))) found.Add(path);
            }

            foreach (string inside in dir.GetDirectories())
            {
                // Godot's own cache and the build output are not the game
                if (inside.StartsWith(".") || inside == "bin" || inside == "obj") continue;

                Walk(folder.TrimSuffix("/") + "/" + inside, found);
            }
        }


        // ---- the objects --------------------------------------------------------------------

        void EveryObjectIsThere(RoomNode room)
        {
            GD.Print("");
            GD.Print($"room    {room}");

            foreach (Prop prop in Enum.GetValues<Prop>())
            {
                Furniture one = room.Furnishings.FirstOrDefault(f => f.Is == prop);

                if (one == null)
                {
                    Problem($"there is no {prop.Word()} in the room, so {prop.Replaces()} cannot " +
                            "be reached at all - the room IS the interface, and there is no menu " +
                            "to fall back on");
                    continue;
                }

                string key = Props.NameKey(prop);

                if (!_text.Has(key))
                {
                    Problem($"the {prop.Word()} has no name in this locale ({key}), so the one " +
                            "affordance the room has reads as a key");
                    continue;
                }

                GD.Print($"        {prop.Word(),-12} \"{one.Called}\" - replaces {prop.Replaces()}");
            }
        }


        // ---- the sheet ----------------------------------------------------------------------

        void FillItIn(RoomNode room, Game.Sheet.Sheet sheet)
        {
            GD.Print("");

            if (sheet == null)
            {
                Problem("there is no character sheet on the table, so there is nowhere to make a " +
                        "character and no character screen to make one in either");
                return;
            }

            Filling offers = sheet.Offers;

            if (offers == null || !offers.CanFill(Line.Class))
            {
                Problem("no class is installed, so the sheet's one load-bearing blank cannot be " +
                        "filled - install a class pack");
                return;
            }

            GD.Print($"sheet   offers {offers}");

            // TOUCHED THE WAY A PLAYER TOUCHES IT, which is the whole of what this check got
            // wrong for as long as it has existed: it called sheet.Touch(line) - the method - and
            // reported that the sheet fills in by touching its blanks. It does not. The blanks
            // were words with no body behind them and were in nobody's reachable list, so the
            // only caller of that method in the entire build was this line, and a player clicking
            // the sheet, or walking to it with a keyboard, got nothing at all.
            //
            // So it goes through the room's own list now. A check that reaches past the thing the
            // player uses is a check that will pass on a game nobody can play.
            var reachable = new Dictionary<string, Game.Access.Reachable>();

            foreach (Game.Access.Reachable one in room.Reachables())
                reachable[one.Called] = one;

            foreach (Line line in Enum.GetValues<Line>())
            {
                if (!line.IsADropdown()) continue;

                if (!offers.CanFill(line)) { GD.Print($"        {line.Word()}: nothing installed"); continue; }

                string called = _text.Get(SheetKeys.Label(line));

                if (!reachable.TryGetValue(called, out Game.Access.Reachable blank))
                {
                    Problem($"the {line.Word()} blank is not reachable from the room, so it can " +
                            "be filled in by calling a method and by nothing a player can do");
                    continue;
                }

                if (blank.Body == null)
                    Problem($"the {line.Word()} blank has no body, so a click can never land on it");

                if (!Hitbox.Generous(blank.Span))
                    Problem($"the {line.Word()} blank is {Hitbox.Short(blank.Span) * 1000f:0} mm " +
                            "short of a fingertip");

                blank.Touch();

                string wrote = sheet.Written(line);

                GD.Print($"        {line.Word()}: {wrote}");

                if (wrote.Length == 0)
                    Problem($"touching the {line.Word()} blank wrote nothing, and it has " +
                            $"{offers.Offers(line).Count} thing(s) to write");
            }

            if (!sheet.Finished)
            {
                Problem("the sheet is not finished after every blank was touched once - " +
                        $"{sheet.Character}");
                return;
            }

            GD.Print($"        finished: {sheet.Character}");

            // R1: the DM picks it up, turns it round, looks it over, pauses, sets it down
            sheet.HandItOver();

            GD.Print($"        the hands did {Game.Sheet.Sheet.Reading.Count} things with it: " +
                     string.Join(", ", Game.Sheet.Sheet.Reading.Select(
                         Content.Places.Gestures.Word)));
        }

        // R0's verify list, as arithmetic: change a starting attribute on the sheet and the dice
        // change. This is the claim that the sheet IS the character rather than a picture of one.
        void TheDiceFollowTheSheet()
        {
            GD.Print("");

            Game.Campaigns.Library shelf = Game.Campaigns.Library.Load(quiet: true);

            ClassCard card = shelf.InPlay.SelectMany(c => c.Classes.All).FirstOrDefault();

            if (card == null) return;

            TraitCard nudge = shelf.InPlay
                                   .SelectMany(c => c.Sheet.For(Blank.Race))
                                   .FirstOrDefault(r => r.Nudges.Count > 0);

            if (nudge == null)
            {
                GD.Print("dice    no race on this shelf nudges anything, so there is nothing to prove");
                return;
            }

            var classes = new ClassRoster[0];

            var sheet = new CharacterSheet { ClassId = card.Id };

            Actor plain = Assemble(shelf, sheet);

            sheet.RaceId = nudge.Id;

            Actor nudged = Assemble(shelf, sheet);

            KeyValuePair<Attr, int> moved = nudge.Nudges.First();

            Die was = plain.Attribute(moved.Key);
            Die now = nudged.Attribute(moved.Key);

            GD.Print($"dice    {card.Id} alone throws {moved.Key} {was.Label()}; " +
                     $"writing '{nudge.Id}' on the race line makes it {now.Label()}");

            if (was == now)
                Problem($"writing '{nudge.Id}' on the sheet changed nothing about the dice - the " +
                        "sheet is a picture of the character rather than the character");

            Pool pool = nudged.BuildPool(Attr.Might, Skill.Blades);

            GD.Print($"        and the pool thrown on the tray is {pool.Count} dice: " +
                     string.Join(" + ", pool.Dice.Select(d => d.Die.Label())));
        }

        static Actor Assemble(Game.Campaigns.Library shelf, CharacterSheet sheet)
        {
            foreach (Game.Campaigns.Loaded campaign in shelf.InPlay)
            {
                if (!campaign.Classes.Has(sheet.ClassId)) continue;

                return sheet.Assemble(campaign.Classes, shelf.Items,
                                      Merged(shelf));
            }

            return null;
        }

        // the shelf's sheet options are per pack; the blanks offer all of them at once
        static SheetOptions Merged(Game.Campaigns.Library shelf) =>
            shelf.InPlay.Select(c => c.Sheet).FirstOrDefault(s => s.Count > 0);


        // ---- the shelf ----------------------------------------------------------------------

        // a campaign played to the end: no encounter left and no fight in the air, which is what
        // Box.Finished reads and what puts the trophy on the box
        static SaveGame Finished()
        {
            var save = new SaveGame
            {
                Campaign = "greyhollow",
                CampaignFormat = 1,
                Chapter = "the_hollow",
                Sheet = new CharacterSheet
                {
                    Name = "Aeth",
                    ClassId = "hearthguard.warden",
                    RaceId = "hearthguard.hillfolk",
                    BackgroundId = "hearthguard.gate_watch",
                    Vigor = 17,
                    Nerve = 2,
                    Erasures = 6,
                },
            };

            save.Sheet.Conditions.Add(Condition.Winded);
            save.Sheet.Grew("shield_wall");

            return save;
        }

        void TheShelf(RoomNode room)
        {
            GD.Print("");
            GD.Print($"shelf   {room.Shelf} in {room.Saves()}");

            foreach (Box box in room.Shelf.Boxes) GD.Print($"        {box}");

            if (room.Shelf.Count == 0)
            {
                GD.Print("        nothing finished yet, which is what a new room looks like");

                // R4: a finished campaign adds a box, and the box IS the versioned JSON P6 writes.
                // Written here rather than assumed, because the claim is that they are one thing.
                Box added = room.Keep(Finished());

                if (added == null)
                {
                    Problem("a finished campaign could not put a box on the shelf, so nothing the " +
                            "player does would ever accumulate in the room");
                    return;
                }

                GD.Print($"        a campaign finished and a box went up: {added}");

                if (!added.Finished)
                    Problem($"{added.File} went on the shelf without its trophy - a finished " +
                            "campaign is meant to be visibly finished");
            }

            Box took = room.TakeDown(room.Shelf.Boxes[0].File);

            if (took == null)
            {
                Problem("the shelf has boxes on it and none of them could be taken down");
                return;
            }

            GD.Print($"        took down {took.File} - {took.Save}");

            if (took.Save?.Sheet == null)
                GD.Print("        (an older save, with no sheet in it - it still opens)");
        }
    }
}
