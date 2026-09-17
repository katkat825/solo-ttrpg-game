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
using Game.Localization;
using Game.Room;
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

            Node sheet = room.GetNodeOrNull("Table/Sheet");

            FillItIn(sheet as Game.Sheet.Sheet);

            TheDiceFollowTheSheet();

            TheShelf(theRoom);

            GD.Print("");
            Finish();
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

        void FillItIn(Game.Sheet.Sheet sheet)
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

            // touched, not confirmed: each blank cycles, and there is no button anywhere
            foreach (Line line in Enum.GetValues<Line>())
            {
                if (!line.IsADropdown()) continue;

                if (!offers.CanFill(line)) { GD.Print($"        {line.Word()}: nothing installed"); continue; }

                string wrote = sheet.Touch(line);

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
