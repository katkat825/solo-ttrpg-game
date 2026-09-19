using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Content.Saves;
using Core.Localization;
using Game.Book;
using Game.Localization;
using Game.Saves;

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

        // THE FIGHT ON THE TABLE, FOR THE HAND THAT REACHES WITH NO MOUSE (V2). The room's
        // reachables are the one list a keyboard, a cursor and a screen reader all walk, so an
        // object that ends a turn and is not in it is an object only a mouse can use. Optional:
        // a room with no fight on the table simply has one fewer thing in reach.
        [Export] public NodePath FightPath { get; set; }

        Game.Fight.Fight _fight;

        // where saves live. Empty looks beside the executable, which is where the game writes them
        [Export] public string SavesFolder { get; set; } = "";

        public const string FolderName = "saves";

        // THE ONE LIGHT IN THE ROOM. Exported and actually applied - until the eye check these
        // two were declared here, documented as the room owning the light, and read by nothing.
        [Export] public NodePath LampPath { get; set; } = "Lamp";

        [Export] public Color Lamp { get; set; } = new Color("#ffd9a8");

        [Export] public float LampEnergy { get; set; } = 3.2f;

        // the room's ambient, which is the other half of what makes it look like an hour of the day
        [Export] public NodePath SkyPath { get; set; } = "WorldEnvironment";

        // AN HOUR TO PRETEND IT IS, so the eye check can look at midnight in December at four in the
        // afternoon in September. Empty means the machine's own clock, which is the whole input in
        // play: no network, ever, and it tells nobody where you are.
        [Export] public string Pretend { get; set; } = "";

        // ---- the book and the bookcase (BK1-BK6) --------------------------------------------

        [Export] public NodePath BookcasePath { get; set; }

        // the book, laid open on the shelf under the corkboard. It is the room's rather than the
        // table's because the shelf it lives on is, and because it is opened at the bookcase too
        [Export] public NodePath OpenedPath { get; set; }

        [Export] public NodePath HelpPath { get; set; }

        // whose hands hand the rules book over (BK3). The same export the sheet has, for the same
        // reason: the room knows where the DM sits, and the DM has never known where the room is
        [Export] public NodePath DmPath { get; set; }

        // THE WAY IN (Phase AX). The hand that reaches without a mouse, the voice that reads the
        // room out, the letters' size and the keys - all of it beside the room rather than in it,
        // because this file is already the longest in the game and for a better reason than that.
        [Export] public NodePath ReachingPath { get; set; }

        // RELAUNCH CONTINUES IN PLACE (BK2). Booting drops you at the table with the book already
        // open to the character you last played - the diegetic "continue", with no prompt to answer.
        // Exported so an eye check can boot into a cold room on purpose.
        [Export] public bool ContinueOnBoot { get; set; } = true;

        [Signal] public delegate void TouchedEventHandler(int prop);

        // a campaign book was opened at a character: you have sat down. Empty who means a blank
        // ribbon, which is a new character and a blank sheet (BK5).
        // 'Reading' would collide with the event member Godot's generator emits for a signal of
        // that name, and Reading is the bookmark you have open
        [Signal] public delegate void SatDownEventHandler(string campaign, string who);

        // a blank object on the bookcase was picked up - the Workshop door for that content type
        [Signal] public delegate void WorkshopEventHandler(int starter);

        // the door is the quit, and quitting asks - it is the one irreversible object in the room.
        // It carries whether anything was left unwritten, so whatever asks can say so rather than
        // asking a room-shaped question the player has no way to answer
        [Signal] public delegate void LeavingEventHandler(bool unsaved);

        readonly List<Furniture> _furniture = new List<Furniture>();

        readonly ILocalizer _text = new GodotLocalizer();

        Node3D _table;

        Game.Sheet.Sheet _sheet;

        Furniture _under;

        SaveShelf _shelf;

        Bookcase _bookcase;

        Opened _open;

        Help _help;

        Game.Dm.Dm _dm;

        Game.Access.Reaching _reach;

        // the books standing on the shelf under the corkboard: the campaign you are playing, and
        // the rules book beside it as a peer
        readonly List<Game.Book.Book> _standing = new List<Game.Book.Book>();

        public IReadOnlyList<Furniture> Furnishings => _furniture;

        public SaveShelf Shelf => _shelf;

        public IReadOnlyList<Game.Book.Book> OnTheShelf => _standing;

        public Bookcase Case => _bookcase;

        public Game.Access.Reaching Reach => _reach;

        // WHICH BOOK IS IN YOUR HANDS AND WHICH PAGE IT IS OPEN AT. Pure, and the part of this
        // worth getting right: a book opens at its contents, the rules book lays over whatever you
        // were reading without losing your place, and going back goes back one step
        public Opening Opening { get; } = new Opening();

        // the re-readable log (BK4). The quest half of it is derived from the fact store, so it
        // survives a reload; what was said and done belongs to this sitting
        public StorySoFar Story { get; } = new StorySoFar();

        // which errand you are pointed at, read by both the corkboard and the book (BK4)
        public Pinning Pinning { get; } = new Pinning();

        // what you own and what is on the table tonight (BK6)
        public Loadout Loadout { get; private set; } = new Loadout();

        // every book you have (BK2), rebuilt whenever the save folder is re-read
        public Collection Collection { get; private set; }

        // the last five saves as tabs, with a sixth for the next five (BK4)
        public Tabs Turning { get; private set; }

        // which campaign's book you have open, and whose game inside it
        public string Playing { get; private set; } = "";

        public Bookmark Reading { get; private set; }

        // WHAT TO WRITE DOWN WHEN SOMETHING ASKS.
        //
        // The room owns the folder and the shelf; it does not own the game. Whatever is being
        // played - the fight today, a place being walked tomorrow - hands over a way to take a
        // snapshot, and everything that saves goes through this one seam. With nothing set the
        // room still opens and the door still works; there is simply nothing to write.
        public Func<SaveGame> Snapshot { get; set; }

        public Autosave Saving { get; } = new Autosave();

        // WHAT TIME IT IS WHERE THE PLAYER IS SITTING (AX6), and a seam for whatever wants to say
        // otherwise - the export above for an eye check, a delegate for a headless one
        public Func<DateTime> Now { get; set; }

        public Daylight Hour { get; private set; }

        // something happened that a save would want to have caught
        public void Happened() => Saving.Happened();

        // whether closing now would lose something. The door asks this, and so will the book
        public bool Unsaved => Snapshot != null && Saving.Dirty;

        // which box the player last took down, so a resumed game knows what it resumed
        public Box Held { get; private set; }

        public override void _Ready()
        {
            _table = TablePath != null && !TablePath.IsEmpty ? GetNodeOrNull<Node3D>(TablePath) : null;

            _fight = FightPath != null && !FightPath.IsEmpty
                ? GetNodeOrNull<Game.Fight.Fight>(FightPath)
                : null;

            _eye = GetNodeOrNull<Leaning>("Table/Leaning");

            _sheet = SheetPath != null && !SheetPath.IsEmpty
                ? GetNodeOrNull<Game.Sheet.Sheet>(SheetPath)
                : null;

            // found before anything is laid out, because laying anything out asks it to gather again
            _reach = ReachingPath != null && !ReachingPath.IsEmpty
                ? GetNodeOrNull<Game.Access.Reaching>(ReachingPath)
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

            TheBooks();

            if (_table == null)
                GD.PushWarning($"room: no table at '{TablePath}' - the room is furniture with " +
                               "nothing to play on");

            // last, because everything above it changed what there is to reach
            Gathered();

            if (_reach == null)
                GD.PushWarning("room: there is no Reaching in this room, so nothing here can be " +
                               "played by keyboard and nothing can be read out - the room IS the " +
                               "interface, and half the people it is for cannot use a mouse");
        }

        // WHAT THERE IS TO REACH CHANGED. Called wherever the room lays words out or stands a book
        // somewhere, which is the only honest trigger: the hand has to be able to find a line on a
        // page that did not exist a frame ago.
        public void Gathered() => _reach?.Gathered();

        // ---- the book and the bookcase ------------------------------------------------------

        // Found, wired, filled, and then opened where you left off. Everything here is additive to
        // the room that was already standing: a room with no bookcase in the scene still opens, and
        // still plays, and says what is missing rather than failing.
        void TheBooks()
        {
            _bookcase = Found<Bookcase>(BookcasePath);
            _open = Found<Opened>(OpenedPath);
            _help = Found<Help>(HelpPath);
            _dm = Found<Game.Dm.Dm>(DmPath);

            if (_bookcase != null)
            {
                _bookcase.Lifted += OnBookOpened;
                _bookcase.Workshop += OnWorkshop;
            }

            if (_open != null) _open.Turned += OnTurned;

            if (_help != null) _help.Asked += OnAsked;

            // the two trays that ship are the dice you start with; anything else is earned, and
            // earning is what a campaign writes down rather than what this file lists
            foreach (string skin in Game.Tray.TraySkin.All())
                Loadout.Earn(TableProp.Tray, skin);

            _installed = Installed();

            Shelve();

            if (ContinueOnBoot) Continue();
        }

        T Found<T>(NodePath path) where T : Node =>
            path != null && !path.IsEmpty ? GetNodeOrNull<T>(path) : null;

        // EVERY CAMPAIGN IS A BOOK, on the bookcase and on the table's shelf (BK1, BK2).
        //
        // Built out of two lists and nothing else: what the loader found, and what is in the save
        // folder. Re-read rather than remembered, so the room cannot claim a campaign the folder
        // does not have - the same identity the shelf has had since R4.
        public void Shelve()
        {
            Collection = Game.Book.Collection.Of(_installed, _shelf);

            Turning = Tabs.Of(_shelf, Playing.Length > 0 ? Playing : null);

            GD.Print($"books   {Collection}");

            foreach (Volume volume in Collection.Volumes) GD.Print($"        {volume}");

            StandTheShelf();

            if (_bookcase == null)
            {
                GD.PushWarning("room: there is no bookcase in this room, so the campaigns you are " +
                               "not playing have nowhere to stand and the Workshop has no door");
                return;
            }

            _bookcase.Stand(Collection);
            _bookcase.StandTheRules();
            _bookcase.StandTheBlanks();
            _bookcase.StandTheCollection(Game.Tray.TraySkin.All(),
                                        Content.Minis.SharedMinis.All.Select(m => m.Id));

            GD.Print($"        {_bookcase}");

            Gathered();
        }

        // ASKED ONCE A SITTING. What is subscribed can change between two evenings and the bookcase
        // has to say so, which is why it is asked at all rather than written down - but it cannot
        // change while the room is open, and asking the loader again on every save write would mean
        // re-reading every campaign on disk to stand one book.
        IReadOnlyList<string> _installed = Array.Empty<string>();

        IReadOnlyList<string> Installed()
        {
            try
            {
                return Game.Campaigns.Library.Load(quiet: true)
                                     .InPlay
                                     .Where(c => c.Manifest is { IsPlayable: true })
                                     .Select(c => c.Id)
                                     .ToArray();
            }
            catch (Exception could)
            {
                GD.PushWarning("room: the loader could not be asked what is installed - " +
                               could.Message);

                return Array.Empty<string>();
            }
        }

        // THE SHELF UNDER THE CORKBOARD holds the book you are playing and the rules book beside it
        // as a peer (BK3). One book per campaign rather than one per save: a playthrough with forty
        // autosaves in it is one book, and the tabs inside it are how you get back to an earlier one.
        void StandTheShelf()
        {
            Furniture shelf = _furniture.FirstOrDefault(f => f.Is == Prop.Shelf);

            if (shelf == null) return;

            foreach (Game.Book.Book was in _standing) was.QueueFree();

            _standing.Clear();

            float along = -shelf.Size.X * 0.5f + 0.06f;

            foreach (Volume volume in Collection.Readable)
            {
                if (along > shelf.Size.X * 0.5f - 0.04f) break;

                var book = new Game.Book.Book
                {
                    Name = Standing + volume.Campaign,
                    Campaign = volume.Campaign,
                    Finished = volume.Finished,
                    Position = new Vector3(along, Tall * 0.5f, 0f),
                };

                shelf.AddChild(book);

                _standing.Add(book);

                book.Touched += OnBookOpened;

                along += book.Thickness + 0.007f;
            }

            if (along > shelf.Size.X * 0.5f - 0.04f) return;

            var rules = new Game.Book.Book
            {
                Name = Standing + "rules",
                Is = Tome.Rules,
                Position = new Vector3(along + 0.02f, Tall * 0.5f, 0f),
            };

            shelf.AddChild(rules);

            _standing.Add(rules);

            rules.Touched += OnBookOpened;
        }

        // ---- opening a book, and what each page does ----------------------------------------

        // RELAUNCH CONTINUES IN PLACE (BK2). No prompt, no "continue?" - the newest place in any
        // book you can read is where the book is already open when the room appears. A cold room
        // with nothing saved simply has no book open, which is what a first evening looks like.
        public Bookmark Continue()
        {
            Bookmark most = Collection?.Most;

            if (most == null)
            {
                GD.Print("books   nothing to continue - every book on the case is unopened");
                return null;
            }

            SitDownAt(Collection.Newest.Campaign, most);

            return most;
        }

        // WHICH CAMPAIGN IS ON THE TABLE, told rather than chosen - the room owns the folder and the
        // books, it does not own the game. Opening a book is one way it gets set and a world being
        // walked is the other, and the log and the corkboard need it either way.
        public void Plays(string campaign)
        {
            if (string.Equals(Playing, campaign ?? "", StringComparison.Ordinal)) return;

            Playing = campaign ?? "";

            Turning = Tabs.Of(_shelf, Playing.Length > 0 ? Playing : null);

            Reconsider();
        }

        // sat down: the book opens, the sheet inside it goes on the table, and the tabs down its
        // edge are this campaign's saves
        public void SitDownAt(string campaign, Bookmark ribbon)
        {
            Plays(campaign);

            Reading = ribbon;

            if (ribbon?.Latest != null)
            {
                Held = ribbon.Latest;

                if (ribbon.Latest.Save?.Sheet != null) _sheet?.Take(ribbon.Latest.Save.Sheet);
            }

            OpenTheBook(Tome.Campaign);

            GD.Print($"books   {Playing} is open at {(ribbon == null ? "the front" : ribbon.ToString())}");

            EmitSignal(SignalName.SatDown, Playing, ribbon?.Who ?? "");
        }

        public bool OpenTheBook(Tome tome)
        {
            if (!Opening.Open(tome)) return false;

            LayTheContents();

            return true;
        }

        // THE CONTENTS PAGE, WHICH IS THE PAUSE MENU AND IS NOT ONE. Every line on it is a line
        // printed in a book you are holding, and the words come out of the locale like every other
        // word in the game.
        void LayTheContents()
        {
            if (_open == null || Opening.Which is not { } tome) return;

            var lines = new List<string>();

            foreach (string key in Contents.Of(tome)) lines.Add(_text.Get(key));

            _open.Lay(_text.Get(tome.NameKey()), lines);

            Gathered();

            GD.Print($"book    {_text.Get(tome.NameKey())} - {string.Join(" / ", lines)}");
        }

        // a row on whichever page is showing. The row index means a page of the campaign book or a
        // chapter of the rules, depending on what is in your hands - which is exactly what Opening
        // already knows and why the node does not have to
        void OnTurned(int row)
        {
            if (Opening.Which is not { } tome) return;

            // THE TWO META PAGES ANSWER THEIR OWN ROWS. Every other page is read rather than pressed,
            // so a touch on one of those turns back to the contents; these two are the only places in
            // the game where touching a line changes something rather than going somewhere
            if (Opening.At is Page.Settings) { TurnADial(row); return; }

            if (Opening.At is Page.Keys) { ArmAKey(row); return; }

            if (!Opening.AtTheContents) { Opening.Back(); LayTheContents(); return; }

            if (tome == Tome.Rules)
            {
                var chapters = (Reference[])Enum.GetValues<Reference>();

                if (row >= 0 && row < chapters.Length) TurnTo(chapters[row]);

                return;
            }

            var pages = (Page[])Enum.GetValues<Page>();

            if (row >= 0 && row < pages.Length) TurnTo(pages[row]);
        }

        void TurnADial(int row)
        {
            var dials = (Setting[])Enum.GetValues<Setting>();

            if (row < 0 || row >= dials.Length) return;

            // Turned applies it, writes it down and lays this page again, so a dial cannot be changed
            // in one of two ways depending on where it was changed from
            _reach?.Turned(dials[row]);
        }

        void ArmAKey(int row)
        {
            var acts = (Game.Access.Act[])Enum.GetValues<Game.Access.Act>();

            if (row < 0 || row >= acts.Length || _reach == null) return;

            _reach.How.Keys.Arm(acts[row]);

            LayTheKeys();
        }

        // LAY WHATEVER IS SHOWING AGAIN, without turning to it. Only the pages that are READ are
        // here: turning to the bookcase closes the book and turning to Save writes one, and neither
        // is a thing to do again because a value on the page changed.
        public void Again()
        {
            if (!Opening.IsOpen) return;

            switch (Opening.At)
            {
                case Page.Settings:
                    LayTheSettings();
                    break;

                case Page.Keys:
                    LayTheKeys();
                    break;

                case Page.StorySoFar:
                    LayTheStory();
                    break;

                case Page.Loadout:
                    LayTheLoadout();
                    break;

                case null when Opening.AtTheContents:
                    LayTheContents();
                    break;
            }
        }

        // EVERY PAGE OF THE CAMPAIGN BOOK, IN ONE PLACE YOU CAN READ - the same discipline OnTouched
        // holds for the objects in the room, and for the same reason: "you can play the whole game
        // touching only objects" has to be a list somebody can check rather than a promise.
        public bool TurnTo(Page page)
        {
            if (!Opening.TurnTo(page)) return false;

            GD.Print($"book    turned to {page.Word()}");

            switch (page)
            {
                case Page.StorySoFar:
                    LayTheStory();
                    break;

                case Page.Bookcase:
                    // you close the book and stand up. It writes on the way, because standing up
                    // mid-dungeon is the evening this phase exists to stop losing
                    Wrote(Autosave.When.Scene);
                    ShutTheBook();
                    break;

                case Page.Save:
                    Box wrote = SaveNow();

                    // and the tabs, so the save you just asked for is visibly there: a save button
                    // that appears to do nothing is indistinguishable from a broken one
                    Shelve();
                    LayTheTabs();

                    GD.Print(wrote == null
                        ? "        nothing to write - there is no game in progress"
                        : $"        wrote {wrote.File}");
                    break;

                case Page.Settings:
                    LayTheSettings();
                    break;

                case Page.Keys:
                    LayTheKeys();
                    break;

                case Page.Loadout:
                    LayTheLoadout();
                    break;
            }

            return true;
        }

        public bool TurnTo(Reference chapter)
        {
            if (!Opening.TurnTo(chapter)) return false;

            _open?.Lay(_text.Get(chapter.TitleKey()),
                       new[] { _text.Get(chapter.BodyKey()) },
                       new[] { false });

            Gathered();

            GD.Print($"rules   {_text.Get(chapter.TitleKey())}");

            return true;
        }

        // THE RE-READABLE LOG (BK4). The quest half is derived from the fact store every time it is
        // asked, so it survives a reload and cannot disagree with the world; what was said and done
        // is this sitting's. Read rather than pressed - a log line is not a control.
        void LayTheStory()
        {
            var lines = new List<string>();

            foreach (Entry entry in Story.Read(Ledger(), Playing))
                lines.Add(_text.Format(entry.Reads, _text.Get(entry.Key)));

            if (lines.Count == 0) lines.Add(_text.Get(Recordings.Empty));

            _open?.Lay(_text.Get(Page.StorySoFar.NameKey()), lines,
                       lines.Select(_ => false).ToArray());

            Gathered();
        }

        // THE RELOAD TABS (BK4): the last five saves, and a sixth for the next five.
        void LayTheTabs()
        {
            Turning ??= Tabs.Of(_shelf, Playing.Length > 0 ? Playing : null);

            var lines = new List<string>();

            foreach (Box box in Turning.Showing) lines.Add(box.File);

            if (Turning.More) lines.Add(_text.Format(Tabs.MoreKey, Turning.Hidden));

            if (lines.Count == 0) lines.Add(_text.Get(Recordings.Empty));

            _open?.Lay(_text.Get(Page.StorySoFar.NameKey()), lines);
        }

        // THE DIALS, AND THEY TURN NOW (Phase AX). Each line is one whole sentence with its value
        // counted into it - "Text size - Large" - rather than a label and a value in two columns,
        // because two columns cannot be translated into a language that puts them the other way round.
        void LayTheSettings()
        {
            var lines = new List<string>();
            var turnable = new List<bool>();

            Game.Access.Adjustments how = _reach?.How;

            foreach (Setting setting in Enum.GetValues<Setting>())
            {
                if (setting.Built())
                {
                    string value = how == null
                        ? "-"
                        : _text.Get(setting.ValueKey(setting.Value(how)));

                    lines.Add(_text.Format(setting.NameKey(), value));
                }
                else
                {
                    // printed and plainly not turnable, which is the same call the DM's note makes:
                    // knowing what you cannot do yet is information
                    lines.Add(_text.Get(setting.NameKey()));
                }

                turnable.Add(setting.Built() && how != null);
            }

            _open?.Lay(_text.Get(Page.Settings.NameKey()), lines, turnable);

            Gathered();
        }

        // WHICH KEY DOES WHAT (AX1). A page of its own, because seven acts and eight dials do not fit
        // on one leaf of paper and a book that needs scrolling is not a book. Touch a line and the next
        // key you press is the one it takes - so there is no dialog to accept and nothing to cancel.
        void LayTheKeys()
        {
            Game.Access.Bindings keys = _reach?.How?.Keys;

            var lines = new List<string>();

            foreach (Game.Access.Act act in Enum.GetValues<Game.Access.Act>())
                lines.Add(_text.Format(Game.Access.Acts.NameKey(act),
                                       keys == null ? "-"
                                       : keys.Armed == act ? _text.Get(Game.Access.Acts.Waiting)
                                       : keys.Of(act).ToString()));

            _open?.Lay(_text.Get(Page.Keys.NameKey()), lines,
                       lines.Select(_ => keys != null).ToArray());

            Gathered();
        }

        // WHAT YOU OWN AND WHAT IS ON THE TABLE TONIGHT (BK6). The skins name themselves - a tray
        // carries its own NameKey - so this page has no strings of its own beyond its heading.
        void LayTheLoadout()
        {
            var lines = new List<string>();

            foreach (string skin in Loadout.Owned(TableProp.Tray))
            {
                Game.Tray.TraySkin loaded = skin == Dressing.Plain
                    ? null
                    : Game.Tray.TraySkin.Load(skin);

                string called = loaded != null && loaded.NameKey.Length > 0
                    ? _text.Get(loaded.NameKey)
                    : skin;

                lines.Add(Loadout.Wearing(TableProp.Tray) == skin ? called + " *" : called);
            }

            _open?.Lay(_text.Get(Page.Loadout.NameKey()), lines);

            Gathered();
        }

        public void ShutTheBook()
        {
            Opening.Close();

            _open?.Shut();

            Gathered();

            GD.Print("book    shut, and you are back at the bookcase");
        }

        // A BOOK PICKED UP, wherever it was standing. The bookcase and the table's shelf both send
        // their books here, because picking one up is one gesture in both places.
        void OnBookOpened(string campaign, int tome)
        {
            var which = (Tome)tome;

            if (which == Tome.Rules) { OpenTheBook(Tome.Rules); return; }

            Volume volume = Collection?.Of(campaign);

            if (volume == null) { OpenTheBook(Tome.Campaign); return; }

            if (!volume.Installed)
            {
                // named rather than silently refused: a book you cannot read is still a book, and
                // the player is owed the reason
                GD.PushWarning($"room: '{campaign}' has saves on the shelf and is not installed - " +
                               "the book is here and there is nothing inside it to read");
                return;
            }

            SitDownAt(campaign, volume.Latest ?? Bookmark.Blank());
        }

        // THE "?" ON THE TABLE (BK3). The DM hands the rules book over, at its contents - so there
        // is no help overlay anywhere, there is a book arriving the way everything at this table
        // arrives, in a pair of hands.
        void OnAsked() => Asks();

        // PUBLIC, because the "?" is not the only way to ask: a key reaches it from anywhere, which
        // is the point of it being the one control a lost player should not have to find
        public void Asks()
        {
            // gestures, not cues: a cue carries a line, and handing a book over has none
            _dm?.Does(Handing.ToArray());

            OpenTheBook(Tome.Rules);
        }

        // reached behind the screen for it, pushed it across, and taken the hand back. Three
        // gestures out of D1's closed vocabulary; BK3 adds none.
        public static readonly IReadOnlyList<Content.Places.Gesture> Handing =
            new[]
            {
                Content.Places.Gesture.ReachBehind,
                Content.Places.Gesture.Push,
                Content.Places.Gesture.Withdraw,
            };

        // the quests of whatever campaign is open. Null until a book is open, which is what an
        // empty story-so-far page is for
        Content.Quests.QuestBook Quests() =>
            Playing.Length == 0
                ? null
                : Game.Campaigns.Library.Load(quiet: true).Campaign(Playing)?.Quests;

        // WHAT HAS HAPPENED TO EVERY QUEST, derived. The book and the corkboard read the same list,
        // so they cannot disagree about which errands you are carrying.
        public IEnumerable<(Content.Quests.Quest Quest, Content.Quests.QuestState State)> Ledger()
        {
            Content.Quests.QuestBook quests = Quests();

            return quests == null
                ? Array.Empty<(Content.Quests.Quest, Content.Quests.QuestState)>()
                : quests.Log(FactsNow());
        }

        // WHERE THE FACTS COME FROM. The world being walked owns them; the room does not, and must
        // not keep a copy - a second fact store is a second answer to "is Bob dead". Whatever is
        // playing sets this, exactly as it sets Snapshot.
        public Func<Content.World.Facts> Remembering { get; set; }

        Content.World.Facts FactsNow() => Remembering?.Invoke();

        // WHERE YOU ARE STANDING, told rather than watched - the same seam Remembering is, and set
        // by the same caller. The room does not keep a copy, because a second answer to "where are we"
        // is exactly the thing this is for answering.
        public Func<string> Where { get; set; }

        // "WHAT WAS I DOING? WHERE ARE WE?" (AX5). One set of sentences, whether they are read out
        // loud or pushed across on the DM's note: an orientation that could say two different things
        // depending on who asked would be worse than none.
        public Game.Access.Spoken Whereabouts()
        {
            Content.Quests.Quest errand = Pinning.Target;

            return Game.Access.Whereabouts.Of(
                _text,
                Where?.Invoke(),
                errand == null || Playing.Length == 0 ? null : errand.TitleKey(Playing));
        }

        // the folder on the corkboard, re-read. Called by whatever moved the world on, for the same
        // reason Happened is: the room is told rather than watching
        public void Reconsider() => Pinning.Read(Ledger());


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
            // AND THE LAMP FOLLOWS THE PLAYER'S OWN CLOCK AND CALENDAR (AX6). Where the lamp is and
            // how bright it was set stay the scene's, tuned by eye; this says how much of it is on and
            // how much daylight is mixed into it, so opening the game after dinner in November gives
            // you a dark quiet room and a June afternoon does not.
            Hour = Daylight.At(Now?.Invoke() ?? Clock());

            var lamp = LampPath != null && !LampPath.IsEmpty
                ? GetNodeOrNull<OmniLight3D>(LampPath)
                : null;

            if (lamp != null)
            {
                lamp.LightColor = Lamp.Lerp(Daylight.Sky, Hour.Cool);
                lamp.LightEnergy = LampEnergy * Hour.Lamp;

                Daytime();

                GD.Print($"hour    {Hour}");
            }
            else
            {
                GD.PushWarning($"room: no lamp at '{LampPath}' - the room is lit by its ambient " +
                               "alone, which is not a room at night, it is a fog");
            }

            if (_table == null) return;

            int doused = 0;

            // THE WHOLE TABLE, AND EVERY KIND OF LIGHT. It used to be the table's direct children
            // and DirectionalLight3D alone, which is the one light that had gone wrong rather than
            // the rule - a lamp of the table's own, or a sun one node deeper inside a prop, was a
            // second light source this walked straight past while reporting that it had swept
            foreach (Node node in _table.FindChildren("*", recursive: true, owned: false))
            {
                if (node is Light3D light && light.Visible)
                {
                    light.Visible = false;
                    doused++;
                }

                if (node is WorldEnvironment sky && sky.Environment != null)
                {
                    sky.Environment = null;
                    doused++;
                }
            }

            GD.Print($"room    one lamp at {LampEnergy * Hour.Lamp:0.0}, and {doused} of the " +
                     "table's own light source(s) put out - a table indoors is lit by the room it " +
                     "is in");
        }

        // read once, because the room re-lights itself and must not compound what it already did
        float _ambient = -1f;

        void Daytime()
        {
            var sky = SkyPath != null && !SkyPath.IsEmpty
                ? GetNodeOrNull<WorldEnvironment>(SkyPath)
                : null;

            if (sky?.Environment == null) return;

            if (_ambient < 0f) _ambient = sky.Environment.AmbientLightEnergy;

            sky.Environment.AmbientLightEnergy = _ambient * Hour.Ambient;
        }

        // the machine's clock, or the hour an eye check asked to see instead
        DateTime Clock() =>
            Pretend.Length > 0 && DateTime.TryParse(Pretend, out DateTime pretend)
                ? pretend
                : DateTime.Now;

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

            foreach (Game.Book.Book one in _standing) one.Touched -= OnBookOpened;

            if (_bookcase != null)
            {
                _bookcase.Lifted -= OnBookOpened;
                _bookcase.Workshop -= OnWorkshop;
            }

            if (_open != null) _open.Turned -= OnTurned;

            if (_help != null) _help.Asked -= OnAsked;
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

            Shelve();
        }

        // the node name every book on the shelf carries, so a re-read clears exactly what it put
        // there and nothing else standing on the shelf
        public const string Standing = "Book";

        public const float Tall = 0.22f;

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

            GD.Print($"room    a book goes on the shelf - {file}");

            Reread();

            return _shelf.Of(file);
        }

        // sortable and human-readable, so the folder reads in the order the shelf stands in
        static string Stamp() => DateTime.Now.ToString("yyyyMMdd_HHmmss");


        // ---- saving as you play --------------------------------------------------------------

        // AUTOSAVE IS EVENT-BASED AND FIRST-CLASS. Until this, the only production write was a
        // finished campaign, which meant a player who closed the game mid-dungeon had played for
        // nothing. The events are the seams the game already has - a place entered, a fight ended,
        // a scene closed - plus the door, which never skips.
        //
        // Each write is its own file, so snapshots accumulate and an earlier one is still there to
        // go back to. Nothing is pruned: how many to keep is a question for when somebody has
        // measured what keeping them costs.
        public Box Wrote(Autosave.When moment)
        {
            if (Snapshot == null || !Saving.Worth(moment)) return null;

            SaveGame save = Snapshot();

            if (save == null) return null;

            Box box = Keep(save, Autosave.FileName(save.Campaign, moment, DateTime.Now) +
                                 SaveShelf.Extension);

            if (box != null) Saving.Wrote(moment);

            return box;
        }

        // the player asked. It is the same call - if a manual save were a different write from an
        // automatic one, one of the two would be the one that had the bug
        public Box SaveNow() => Wrote(Autosave.When.Manual);


        // ---- what there is to reach ----------------------------------------------------------

        // EVERYTHING IN THE ROOM YOU CAN REACH, IN THE ORDER IT IS ASKED IN (AX1).
        //
        // ONE LIST, and that is the whole point of it. A mouse finds the thing whose body is under
        // the cursor; a hand with no mouse walks this in order; a screen reader reads whichever one
        // the hand is on; and the hitbox sweep measures every span in it. Four ways in, one
        // description - because the alternative is a keyboard walk built beside the raycast, and the
        // half that falls behind is the half somebody depends on.
        //
        // The order is the raycast's own: the open book first, because it is the thing nearest your
        // hands and is the reason it is open; then the "?"; then the furniture; then the books on the
        // shelf; then the case you are not sitting at.
        public IReadOnlyList<Game.Access.Reachable> Reachables()
        {
            var all = new List<Game.Access.Reachable>();

            if (_open is { Showing: true }) all.AddRange(_open.Rows());

            if (_help != null) all.Add(_help.Reach());

            // THE PAPER ON THE TABLE, which is the character screen and was in nobody's list. It
            // goes ahead of the furniture because the blanks are the thing you are actually
            // reaching for, and behind the open book for the same reason the book is first.
            if (_sheet != null) all.AddRange(_sheet.Reachables());

            foreach (Furniture one in _furniture) all.Add(one.Reach());

            foreach (Game.Book.Book one in _standing) all.Add(one.Reach());

            if (_bookcase != null) all.AddRange(_bookcase.Reachables());

            // the initiative marker, and only while there is a turn standing beside it: a hand
            // that walked onto a control belonging to nobody's turn would have nothing to press
            if (_fight?.EndTurnMarker is { Live: true } marker) all.Add(marker);

            return all;
        }


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
                    // a save is a book you pick up, not a slot you select (R4)
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
                    // the one irreversible object in the room, so it asks - and it writes on the
                    // way out first. Closing the door on an unwritten game is the one way this
                    // room could lose somebody's evening, and asking them to remember to save is
                    // a menu habit, not a table one
                    bool unsaved = Unsaved;

                    if (Wrote(Autosave.When.Quit) is { } onTheWayOut)
                        GD.Print($"        wrote {onTheWayOut.File} on the way out");
                    else if (unsaved)
                        GD.PushWarning("room: the door was opened with something unsaved and " +
                                       "nothing could be written - say so before it closes");

                    EmitSignal(SignalName.Leaving, unsaved);
                    break;

                default:
                    break;
            }

            EmitSignal(SignalName.Touched, prop);
        }

        // take a campaign down and put what is inside it on the table. The versioned JSON P6
        // writes is what is in it; there is no second record of "campaigns you finished" to drift
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

            GodotObject what = Hit(mouse.Position);

            if (what == null) return;

            // ONE RAYCAST, AND EVERYTHING TOUCHABLE IN THE ROOM ASKED IN ONE ORDER. The open book
            // first, because it is the thing nearest your hands and is the reason it is open; then
            // the furniture; then the books and the blanks standing where you are not sitting.
            if (Touch(what)) GetViewport().SetInputAsHandled();
        }

        // A CLICK LANDED ON SOMETHING. It finds the thing in the same list a hand walks, rather
        // than asking each kind of thing in its own order again - so a mouse and a keyboard can
        // never reach different sets of objects, which they did while there were two lists.
        bool Touch(GodotObject what)
        {
            foreach (Game.Access.Reachable one in Reachables())
            {
                if (!one.Owns(what) || !one.Live) continue;

                _reach?.Onto(what);

                one.Touch();

                // AND REACHING FOR THE PAPER BRINGS IT TO YOU. What is written on the sheet is 10
                // pixels tall from where you are sitting and 47 with it held up, so touching a
                // blank you cannot read and having it silently change is not an interaction - it
                // is the reason the sheet looked broken. Escape sets it back down.
                if (_sheet != null && _sheet.Mine(what)) LeanOverTheSheet();

                return true;
            }

            return false;
        }

        // the eye, which lives on the table with the camera it moves. Optional: a room opened
        // without one simply does not lean, exactly as it did before there was one
        Leaning _eye;

        void LeanOverTheSheet()
        {
            if (_eye == null || _sheet == null || _eye.Moving) return;

            _eye.LeanOver(_sheet.GlobalPosition);
        }

        // THE WORKSHOP DOOR (BK6). What is behind it is Steam's - an app id, the SDK and two
        // machines - so the room names the kind and the skeleton it would copy, and the wiring is
        // P8. A mock here would be a test that lied.
        //
        // Reached through the bookcase's own signal rather than off the raycast, because a blank
        // picked up by keyboard has to do exactly what a blank picked up by mouse does.
        void OnWorkshop(int blank)
        {
            var starter = (Starter)blank;

            GD.Print($"case    {_bookcase?.NameOf(starter)} - the Workshop for " +
                     $"{starter.Kind().ToString().ToLowerInvariant()} content" +
                     (starter.Waiting()
                         ? ", which has no skeleton to copy yet"
                         : $", from templates/{starter.Skeleton()}/{starter.Fills()}"));

            EmitSignal(SignalName.Workshop, blank);
        }

        void Hover(Vector2 at)
        {
            GodotObject what = Hit(at);

            // the hand goes where the cursor is, so a reach after a click carries on from the click
            // rather than from wherever the keyboard last left it
            _reach?.Onto(what);

            _open?.Light(_open.RowUnder(what));

            _help?.Light(_help.Owns(what));

            foreach (Game.Book.Book one in _standing) one.Light(one.Owns(what));

            if (_bookcase != null)
                foreach (Game.Book.Book one in _bookcase.Books)
                    one.Light(one.Owns(what));

            Furniture under = null;

            foreach (Furniture one in _furniture)
                if (one.Owns(what)) { under = one; break; }

            if (ReferenceEquals(under, _under)) return;

            _under?.Light(false);
            _under = under;
            _under?.Light(true);
        }

        // what the cursor is over, whatever it is. Split out of Under so that one raycast serves
        // every touchable thing in the room rather than one per kind of thing
        GodotObject Hit(Vector2 at)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return null;

            var query = PhysicsRayQueryParameters3D.Create(
                camera.ProjectRayOrigin(at),
                camera.ProjectRayOrigin(at) + camera.ProjectRayNormal(at) * Far);

            query.CollideWithAreas = false;

            Godot.Collections.Dictionary hit = GetWorld3D()?.DirectSpaceState?.IntersectRay(query);

            if (hit == null || hit.Count == 0) return null;

            return hit["collider"].As<GodotObject>();
        }

        Furniture Under(Vector2 at)
        {
            GodotObject what = Hit(at);

            if (what == null) return null;

            foreach (Furniture one in _furniture)
                if (one.Owns(what)) return one;

            return null;
        }

        // how far a raycast into the room goes, in metres
        const float Far = 20f;

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"room: {_furniture.Count} objects, {_shelf?.Count ?? 0} on the shelf, {Saving}";
    }
}
