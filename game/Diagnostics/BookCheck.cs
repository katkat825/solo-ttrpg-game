using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Quests;
using Content.Saves;
using Content.Sheet;
using Content.World;
using Core.Localization;
using Game.Book;
using Game.Localization;
using Game.Room;
using Game.Saves;

// Game.Room is both a namespace and a type, and so is Game.Book; alias both so the nodes bind
using RoomNode = Game.Room.Room;
using BookNode = Game.Book.Book;

namespace Game.Diagnostics
{
    // BOOTS THE REAL ROOM AND READS ITS BOOKS (Phase BK).
    //
    // Phase BK's verify column is mostly eye work - a book has to LOOK like a book - but every
    // mechanical claim under it is checkable, and these are the ones that would rot silently:
    //
    //   - A CAMPAIGN IS A BOOK, and there is one for every campaign installed, whether it has ever
    //     been played or not. That is the difference between a bookcase and a save folder
    //   - THE CONTENTS PAGE IS A PAGE, with a real word on every line of it. A contents page with a
    //     key on it reads as a bug and is the failure the locale audit cannot see
    //   - THE RULES BOOK IS A PEER: pressing "?" opens it over whatever you were reading, and
    //     closing it puts you back where you were rather than shutting both
    //   - FIVE CHARACTERS PER CAMPAIGN AND A BLANK ONE, and the blank ribbon disappears at five
    //   - THE RELOAD TABS: five down the edge, a sixth for the next five, and the newest first
    //   - THE STORY SO FAR survives a reload, because the quest half of it is derived from facts
    //   - A SIDE ERRAND CAN BE HANDED BACK AND TAKEN ON AGAIN; the story you are in cannot
    //   - THE WORKSHOP DOOR: a blank object per content type, each naming the skeleton it copies
    //   - AND STILL NO MENUS. RoomCheck greps for one; this is the milestone that most wanted one,
    //     so it says out loud that the book is what a pause menu would have been
    //
    // Everything printed is developer diagnostic, exempt from localization.
    public partial class BookCheck : HeadlessCheck
    {
        protected override string Subject => "book";

        [Export] public PackedScene TheRoom { get; set; }

        // a shelf of its own, never the player's: this check writes saves to prove a book can stand
        [Export] public bool UseAScratchShelf { get; set; } = true;

        readonly ILocalizer _text = new GodotLocalizer();

        public override void _Ready()
        {
            RoomNode room = Stand();

            if (room == null) { Finish(); return; }

            TheBooksOnTheCase(room);

            TheContentsPage(room);

            TheRulesAreAPeer(room);

            FiveCharactersAndABlankOne(room);

            TheReloadTabs(room);

            TheStorySoFar(room);

            TheErrandsYouCanHandBack();

            TheCorkboardReadsTheRealCampaign(room);

            TheWorkshopDoor(room);

            GD.Print("");
            Finish();
        }

        RoomNode Stand()
        {
            if (TheRoom == null)
            {
                Problem("no room scene was given, so there is no bookcase to read");
                return null;
            }

            var room = TheRoom.Instantiate<RoomNode>();

            if (room == null)
            {
                Problem("the room scene's root is not a Room");
                return null;
            }

            // set BEFORE the tree readies it: after AddChild the room has already read its shelf
            if (UseAScratchShelf) room.SavesFolder = Scratch();

            AddChild(room);

            return room;
        }

        string Scratch()
        {
            string folder = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "book_check_" + System.IO.Path.GetRandomFileName());

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


        // ---- BK1, BK2: a campaign is a book, and they stand on a bookcase --------------------

        void TheBooksOnTheCase(RoomNode room)
        {
            GD.Print("");

            if (room.Case == null)
            {
                Problem("there is no bookcase in the shipped room, so the campaigns you are not " +
                        "playing have nowhere to stand and the Workshop has no door");
                return;
            }

            GD.Print($"case    \"{room.Case.Called}\" - {room.Case}");

            if (room.Case.Called.Length == 0 || room.Case.Called == BookKeys.BookcaseName)
                Problem($"the bookcase has no name in this locale ({BookKeys.BookcaseName})");

            Collection collection = room.Collection;

            if (collection == null || collection.Count == 0)
            {
                Problem("nothing is installed, so there is not one book on the case - a bookcase " +
                        "shows every campaign you have, not only the ones you have played");
                return;
            }

            foreach (Volume volume in collection.Volumes)
            {
                GD.Print($"        {volume}");

                // BK1's own claim: a book carries the campaign's title, which is the campaign's
                // string and not the engine's. A book with a key on the spine is not a book
                if (volume.Installed && !_text.Has(volume.TitleKey))
                    Problem($"'{volume.Campaign}' has no name in this locale " +
                            $"({volume.TitleKey}), so its book has nothing on the spine");
            }

            // AN INSTALLED CAMPAIGN NOBODY HAS PLAYED IS STILL A BOOK. The scratch shelf is empty,
            // so every volume here is unopened - and there had better still be some
            if (collection.Volumes.All(v => !v.Unopened))
                Problem("every book on the case has been played, on a shelf with no saves in it - " +
                        "the bookcase is reading the save folder rather than what is installed");

            if (room.Case.Books.Count == 0)
                Problem("the bookcase stood no books at all, and there are campaigns to stand");

            BookNode rules = room.Case.Books.FirstOrDefault(b => b.Is == Tome.Rules);

            if (rules == null)
                Problem("the rules book is not on the bookcase - it is a peer of the campaign " +
                        "book and lives in both places (BK3)");

            // and on the table's own shelf, which is where it is when you are playing
            if (room.OnTheShelf.All(b => b.Is != Tome.Rules))
                Problem("the rules book is not on the shelf under the corkboard either, so at the " +
                        "table there is nothing to hand you");

            GD.Print($"shelf   {room.OnTheShelf.Count} book(s) under the corkboard: " +
                     string.Join(", ", room.OnTheShelf.Select(b => b.ToString())));
        }


        // ---- BK3: the contents page, which is the pause menu and is not one -------------------

        void TheContentsPage(RoomNode room)
        {
            GD.Print("");

            foreach (Tome tome in Enum.GetValues<Tome>())
            {
                if (!_text.Has(tome.NameKey()))
                    Problem($"the {tome.Word()} has no name in this locale ({tome.NameKey()})");

                foreach (string key in Contents.Of(tome))
                    if (!_text.Has(key))
                        Problem($"the {tome.Word()}'s contents page has a line with no words on it " +
                                $"({key}) - a contents page reading as a key is a bug you can see");
            }

            // every rules chapter has a body as well as a title, or the book opens onto nothing
            foreach (Reference chapter in Enum.GetValues<Reference>())
                if (!_text.Has(chapter.BodyKey()))
                    Problem($"the rules chapter '{chapter.Word()}' has a title and no text " +
                            $"({chapter.BodyKey()})");

            room.OpenTheBook(Tome.Campaign);

            if (!room.Opening.IsOpen || !room.Opening.AtTheContents)
                Problem("the campaign book was opened and is not at its contents page - a book " +
                        "opens at the front");

            GD.Print($"book    {room.Opening}");

            // and every page it lists actually goes somewhere
            foreach (Page page in Enum.GetValues<Page>())
            {
                if (page == Page.Bookcase) continue;

                if (!room.TurnTo(page))
                    Problem($"the contents page lists '{page.Word()}' and turning to it did " +
                            "nothing - a line in a book that does not turn is a line that lies");

                GD.Print($"        {page.Word(),-14} \"{_text.Get(page.NameKey())}\"");

                room.Opening.Back();
            }

            // the one that leaves closes the book, which is the whole of "back to the bookcase"
            room.OpenTheBook(Tome.Campaign);
            room.TurnTo(Page.Bookcase);

            if (room.Opening.IsOpen)
                Problem("turning to 'back to the bookcase' left the book open - standing up is " +
                        "closing it");
        }


        // ---- BK3: the rules book is a peer, not an overlay -----------------------------------

        void TheRulesAreAPeer(RoomNode room)
        {
            GD.Print("");

            room.OpenTheBook(Tome.Campaign);
            room.TurnTo(Page.StorySoFar);

            // pressing "?" hands you a second book; you do not drop the first one
            room.OpenTheBook(Tome.Rules);

            if (room.Opening.Which != Tome.Rules)
                Problem("the \"?\" was pressed and the rules book is not the one in your hands");

            if (!room.Opening.Beneath)
                Problem("the rules book was opened over the campaign book and the campaign book " +
                        "was dropped - a book handed to you does not take the one you were holding");

            room.TurnTo(Reference.Pool);

            GD.Print($"rules   {room.Opening}");

            // back out of the chapter, then back out of the rules book: you are where you were
            room.Opening.Back();
            room.Opening.Back();

            if (room.Opening.Which != Tome.Campaign || room.Opening.At != Page.StorySoFar)
                Problem($"the rules book was closed and you did not come back to the page you " +
                        $"were reading - {room.Opening}");

            GD.Print($"        and closing it puts you back at {room.Opening}");

            room.ShutTheBook();
        }


        // ---- BK5: five characters per campaign, and a blank one ------------------------------

        void FiveCharactersAndABlankOne(RoomNode room)
        {
            GD.Print("");

            Volume empty = Volume.Of("greyhollow", true, Array.Empty<Box>());

            if (!empty.Unopened || empty.Ribbons.Count() != 1 || !empty.Ribbons.First().IsBlank)
                Problem("a campaign nobody has played offers something other than one blank " +
                        "ribbon - a new character is a blank sheet and nothing else");

            // written down for real, so the count is the save folder's and not a number kept here
            var written = new List<Box>();

            for (int at = 0; at < Volume.Most; at++)
            {
                Box box = room.Keep(APlaythrough("greyhollow", "Hero" + at),
                                    $"20260918_0000{at}0_greyhollow_fought" + SaveShelf.Extension);

                if (box == null)
                {
                    Problem("a playthrough could not be written to the scratch shelf");
                    return;
                }

                written.Add(box);
            }

            Volume full = Volume.Of("greyhollow", true, written);

            GD.Print($"case    {full}");

            if (full.Bookmarks.Count != Volume.Most)
                Problem($"{Volume.Most} characters were written and the book has " +
                        $"{full.Bookmarks.Count} - a character is the newest save under a name");

            if (full.RoomForAnother || full.Ribbons.Any(r => r.IsBlank))
                Problem($"a {Volume.Most}th character exists and the book still offers a blank " +
                        "ribbon - five per campaign is the number");

            // the sixth is refused by the volume rather than by whatever is drawing it
            Volume over = Volume.Of("greyhollow", true,
                                    written.Concat(new[]
                                    {
                                        room.Keep(APlaythrough("greyhollow", "Sixth"),
                                                  "20260918_000060_greyhollow_fought" +
                                                  SaveShelf.Extension),
                                    }));

            if (over.Bookmarks.Count != Volume.Most)
                Problem($"a sixth character got into the book - it holds {over.Bookmarks.Count}");

            foreach (Bookmark ribbon in full.Ribbons) GD.Print($"        {ribbon}");

            // and sitting down at one puts that character's sheet on the table
            room.Shelve();

            Bookmark most = room.Continue();

            if (most == null)
                Problem("there are saves on the shelf and nothing to continue - a relaunch is " +
                        "meant to drop you where you were");
            else
                GD.Print($"open    continued at {most}, book {room.Opening}");
        }

        // a real save with a sheet in it, so the shelf is being read rather than imagined
        static SaveGame APlaythrough(string campaign, string who) =>
            new SaveGame
            {
                Campaign = campaign,
                CampaignFormat = 1,
                Place = "the_hollow",
                Sheet = new CharacterSheet
                {
                    Name = who,
                    ClassId = "hearthguard.warden",
                    Vigor = 14,
                    Nerve = 2,
                },
            };


        // ---- BK4: the reload tabs -----------------------------------------------------------

        void TheReloadTabs(RoomNode room)
        {
            GD.Print("");

            var tabs = new Tabs(room.Shelf.Boxes);

            GD.Print($"tabs    {tabs}");

            if (tabs.Showing.Count > Tabs.Reachable)
                Problem($"{tabs.Showing.Count} tabs are down the edge of the book and {Tabs.Reachable} " +
                        "is the number - a book with forty tabs is a filing cabinet");

            // newest first, because that is the one you reach for
            for (int at = 1; at < tabs.Showing.Count; at++)
                if (tabs.Showing[at].Written > tabs.Showing[at - 1].Written)
                    Problem("the tabs are not newest-first, so the one you want is not the one " +
                            "under your thumb");

            int showing = tabs.Showing.Count;

            if (tabs.More)
            {
                if (!tabs.ShowMore())
                    Problem("the book says there are more saves behind the sixth tab and turning " +
                            "it showed none");

                if (tabs.Showing.Count <= showing)
                    Problem("show-more showed no more");

                GD.Print($"        and show-more turns up {tabs.Showing.Count - showing} more");
            }
            else if (tabs.ShowMore())
            {
                Problem("there is nothing behind the sixth tab and turning it did something - a " +
                        "control that does nothing is worse than no control");
            }

            // and the ones in THIS book are this campaign's, not the whole folder's
            Tabs mine = Tabs.Of(room.Shelf, "greyhollow");

            if (mine.Count == 0)
                Problem("this campaign's book has no tabs and its saves are on the shelf");

            if (Tabs.Of(room.Shelf, "no_such_campaign").Count != 0)
                Problem("a campaign with no saves has tabs in its book");
        }


        // ---- BK4: the story so far, and what survives a reload -------------------------------

        void TheStorySoFar(RoomNode room)
        {
            GD.Print("");

            foreach (string key in Recordings.Keys())
                if (!_text.Has(key))
                    Problem($"a line of the story so far has no wording ({key})");

            if (!_text.Has(Tabs.MoreKey)) Problem($"the sixth tab has no word ({Tabs.MoreKey})");

            var story = new StorySoFar();

            // a real line and a real thing, out of a shipped campaign: a fixture naming a key
            // nothing writes would print the key back and look like the bug it is testing for
            story.Said("wolf", "dialogue.wolf.bark.snag.001");
            story.Did(Content.Entities.Interaction.Search, "the_lockbox",
                      "actor.saltmarch.the_lockbox.name");

            if (story.Count != 2)
                Problem($"two things were said and done and the log holds {story.Count}");

            // THE HALF THAT SURVIVES A RELOAD. A quest's place in the log is derived from the fact
            // store, so it is still there after a load - and it changes when the world does, which
            // is the whole reason quest state is never stored
            IReadOnlyList<Entry> before = story.Read(room.Ledger(), room.Playing).ToArray();

            GD.Print($"story   {story}, {before.Count} line(s) on the page");

            foreach (Entry entry in before)
                GD.Print($"        {_text.Format(entry.Reads, _text.Get(entry.Key))}");

            if (before.Count != 2)
                Problem($"an empty quest book put {before.Count - 2} quest line(s) in the log");

            if (!_text.Has(Recordings.Empty))
                Problem($"an empty story-so-far page has nothing to say ({Recordings.Empty})");
        }


        // ---- BK4: what may be handed back ---------------------------------------------------

        void TheErrandsYouCanHandBack()
        {
            GD.Print("");

            // an errand somebody set: it waits to be offered, so there is somebody to give it back to
            var errand = new Quest("the_sluice",
                                   new Requirement(new[] { "norrel.spoken" }, null),
                                   new Requirement(new[] { "the_sluice_gate.open" }, null),
                                   null);

            // and the story you are in: on from the moment the campaign opened
            var spine = new Quest("the_tide",
                                  Requirement.Always,
                                  new Requirement(new[] { "the_tide.turned" }, null),
                                  null);

            if (!errand.IsSide) Problem("an errand you had to be offered does not read as a side quest");

            if (spine.IsSide) Problem("a quest on from the start reads as an errand somebody set");

            var pinning = new Pinning();

            pinning.Read(new[]
            {
                (errand, QuestState.Active),
                (spine, QuestState.Active),
            });

            GD.Print($"board   {pinning}");

            if (pinning.Count != 2)
                Problem($"two errands are being carried and the folder holds {pinning.Count}");

            if (pinning.Pinned.Length == 0)
                Problem("there are errands in the folder and the corkboard points at none of them");

            if (!pinning.Pin("the_tide") || pinning.Pinned != "the_tide")
                Problem("the corkboard could not be re-pointed at a quest being carried");

            if (!pinning.CanAbandon("the_sluice"))
                Problem("a side errand cannot be handed back, and handing one back is the point");

            if (pinning.CanAbandon("the_tide"))
                Problem("the story you are in can be abandoned - there is nobody to give it back to");

            // and the pin falls off by itself when what it pointed at is finished
            pinning.Read(new[] { (errand, QuestState.Active), (spine, QuestState.Done) });

            if (pinning.Pinned != "the_sluice")
                Problem($"the pinned quest was finished and the pin stayed on it - {pinning}");

            GD.Print($"        finished the spine, and the pin moved to {pinning.Pinned}");
        }


        // ---- and the wiring: the log and the corkboard read the world being played ------------

        // THE POLICY ABOVE IS TESTED ON QUESTS BUILT BY HAND. This is the other half: a real
        // campaign's real quest, reaching the corkboard through the seams the table actually uses -
        // the room is TOLD what is playing and TOLD where the facts are, and keeps no copy of either.
        void TheCorkboardReadsTheRealCampaign(RoomNode room)
        {
            GD.Print("");

            const string campaign = "saltmarch";

            Game.Campaigns.Loaded loaded =
                Game.Campaigns.Library.Load(quiet: true).Campaign(campaign);

            if (loaded == null || loaded.Failed || loaded.Quests.Count == 0)
            {
                Caution($"'{campaign}' is not installed with quests in it, so the corkboard has " +
                        "nothing real to read");
                return;
            }

            Facts facts = Facts.For(campaign);

            room.Remembering = () => facts;
            room.Plays(campaign);

            // nothing has happened, so nothing is being carried
            if (room.Pinning.Count != 0)
                Problem($"nothing has happened and the corkboard is carrying " +
                        $"{room.Pinning.Count} errand(s)");

            Quest errand = loaded.Quests.All.FirstOrDefault(q => q.IsSide);

            if (errand == null)
            {
                Caution($"no errand in '{campaign}' can be handed back, so there is nothing to try");
                return;
            }

            foreach (string fact in errand.Offered.When) facts.Set(fact);

            facts.Set(errand.AcceptedFact);

            room.Reconsider();

            if (room.Pinning.Pinned != errand.Id)
                Problem($"'{errand.Id}' was accepted and the corkboard points at " +
                        $"'{room.Pinning.Pinned}' - the folder is not reading the world");

            GD.Print($"board   {campaign}: {room.Pinning}");

            if (!room.Pinning.CanAbandon(errand.Id))
                Problem($"'{errand.Id}' is an errand somebody set and the book will not hand it back");

            // and the story so far read it without being told separately
            Entry taken = room.Story
                              .Read(room.Ledger(), room.Playing)
                              .FirstOrDefault(e => e.Subject == errand.Id);

            if (taken == null)
                Problem($"'{errand.Id}' was accepted and the story so far does not mention it");
            else
                GD.Print($"story   {_text.Format(taken.Reads, _text.Get(taken.Key))}");
        }


        // ---- BK6: the Workshop door, and what is behind it ----------------------------------

        void TheWorkshopDoor(RoomNode room)
        {
            GD.Print("");

            if (room.Case == null) return;

            int wanted = Enum.GetValues<Starter>().Length;

            if (room.Case.Blanks != wanted)
                Problem($"{wanted} content types and {room.Case.Blanks} blank object(s) on the " +
                        "bookcase - each kind is a door and a missing one cannot be opened");

            foreach (Starter starter in Enum.GetValues<Starter>())
            {
                if (!_text.Has(starter.NameKey()))
                    Problem($"the blank {starter.Word()} has no name in this locale " +
                            $"({starter.NameKey()})");

                string skeleton = starter.Waiting()
                    ? "nothing to copy yet"
                    : $"templates/{starter.Skeleton()}/{starter.Fills()}";

                GD.Print($"blank   {starter.Word(),-10} \"{room.Case.NameOf(starter)}\" -> " +
                         $"{starter.Kind().ToString().ToLowerInvariant(),-9} {skeleton}");

                if (starter.Waiting()) continue;

                // the skeleton it claims to copy has to be there, or the door opens onto nothing
                string folder = System.IO.Path.Combine(
                    ProjectSettings.GlobalizePath("res://"), "..", "templates",
                    starter.Skeleton(), starter.Fills());

                if (!System.IO.Directory.Exists(folder))
                    Caution($"the blank {starter.Word()} would copy {skeleton} and that folder is " +
                            "not there - only the local skeleton is missing; the Workshop itself " +
                            "is Steam's (P8)");
            }

            // AND THE HALF THAT IS HONESTLY NOT BUILT, said out loud rather than faked. A dice skin
            // is a .tres in the engine's own folder and is not a pack at all, so there is no
            // skeleton to copy and making one is a content type the engine does not have yet.
            foreach (Starter waiting in Enum.GetValues<Starter>().Where(Starters.Waiting))
                Caution($"'{waiting.Word()}' has no template skeleton - it is not a pack kind yet, " +
                        "so the blank object stands on the case and says so");

            // BK6's other half, which IS built: the dice-skin swap, from the book
            Loadout loadout = room.Loadout;

            GD.Print($"wear    {loadout}");

            string skin = loadout.Owned(Game.Room.TableProp.Tray)
                                 .FirstOrDefault(s => s != Dressing.Plain);

            if (skin == null)
            {
                Caution("no tray skin ships, so there is nothing to swap from the book");
                return;
            }

            if (!loadout.Wear(Game.Room.TableProp.Tray, skin))
                Problem($"'{skin}' is owned and could not be worn");

            if (loadout.Wearing(Game.Room.TableProp.Tray) != skin)
                Problem("a skin was worn and the table is wearing something else");

            if (loadout.Wear(Game.Room.TableProp.Tray, "a_skin_nobody_owns"))
                Problem("a skin nobody owns was put on the table - what you own is a real question " +
                        "and this is where it is asked");

            GD.Print($"        swapped the tray to '{skin}' from the book, and refused one " +
                     "nobody owns");
        }
    }
}
