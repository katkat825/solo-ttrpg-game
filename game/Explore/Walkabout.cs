using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Campaigns;
using Content.Entities;
using Content.Places;
using Content.Quests;
using Content.Sheet;
using Content.World;
using Core.Dice;
using Core.Localization;
using Core.Space;
using Game.Board;
using Game.Localization;

namespace Game.Explore
{
    // WALKING A PLACE, AT THE TABLE.
    //
    // The world layer has walked places headless since it was built: a place is a map, who is
    // standing in it comes off the facts, the five verbs are the whole vocabulary, and a fight is
    // one of the things that can happen in a place rather than the only thing a map is for. What
    // was missing was a person watching it happen. This is that, and it adds no rules.
    //
    // It is the second thing that can claim a square on the board, the fight being the first, and
    // it claims one the same way for the same reason: the board answers WHICH square and stops.
    // Reach says what a click means, this does it, and the world decides what follows.
    //
    // The fight it starts is a real fight on the same board with the same hero, mustered from the
    // episode the world handed over - which is why the wounds you carry out of one room are the
    // wounds you walk into the next with, and why nothing about the fight knows it came from here.
    [GlobalClass]
    public partial class Walkabout : Node3D
    {
        // the campaign to walk. Empty leaves this node asleep, which is what the table looks like
        // when it boots straight into a fight
        [Export] public string CampaignId { get; set; } = "";

        [Export] public string StartAt { get; set; } = "";

        [Export] public NodePath BoardPath { get; set; } = "../Board";

        [Export] public NodePath DmPath { get; set; } = "../Dm";

        [Export] public NodePath CompanionPath { get; set; } = "../Companion";

        [Export] public NodePath TalkPath { get; set; } = "../Talk";

        [Export] public NodePath ResponsePath { get; set; } = "Response";

        [Export] public NodePath LootPath { get; set; }

        [Export] public NodePath RoomPath { get; set; }

        // the paper, where the three checks you reach for yourself are printed, and the felt they
        // are thrown on. Either may be unset; then nothing is offered and nothing is thrown
        [Export] public NodePath SheetPath { get; set; }

        [Export] public NodePath TrayPath { get; set; }

        // the fight standing in this scene. It is switched off and read for its figures, so the
        // minis a fight uses are named in one place and it is the table, as it always has been
        [Export] public NodePath FightPath { get; set; } = "../Fight";

        [Export] public int Seed { get; set; }

        [Signal] public delegate void ArrivedEventHandler(string place);

        [Signal] public delegate void FoughtEventHandler(bool won);

        Board.Board _board;

        Game.Dm.Dm _dm;

        Game.Companion.Companion _friend;

        Game.Dialogue.Talk _talk;

        Response _response;

        Game.Loot.Stack _loot;

        Game.Room.Room _room;

        Game.Sheet.Sheet _sheet;

        Game.Tray.DiceTray _tray;

        // the check in the air, and what the scene made of it; null when nothing is being tried
        Content.Kits.Ability _trying;

        Game.Fight.Fight _sleeping;

        Game.Fight.Fight _fighting;

        Standees _standees;

        Exploring _world;

        Game.Campaigns.Loaded _campaign;

        Episode _episode;

        // what the offer on the table is about, kept because the answer arrives as an index
        Present _facing;

        readonly ILocalizer _text = new GodotLocalizer();

        public Exploring World => _world;

        public bool Walking => _world?.Where != null && _fighting == null;

        public Standees OnTheMat => _standees;

        public Response Answers => _response;

        public Game.Loot.Stack Carrying => _loot;

        public Game.Sheet.Sheet Paper => _sheet;

        // the fight this walk started, while there is one
        public Game.Fight.Fight Fighting => _fighting;

        // everywhere this walk has been, for a headless check; developer diagnostics
        readonly List<string> _been = new List<string>();

        public IReadOnlyList<string> Been => _been;

        public override void _EnterTree()
        {
            if (!Wanted()) return;

            // BEFORE THE FIGHT READIES, which is the only moment it can still be told. A sibling
            // enters the tree in scene order and readies after; listed above the fight, this is
            // the last instant the boot fight can be asked to stand down
            _sleeping = FightPath == null || FightPath.IsEmpty
                ? null
                : GetNodeOrNull<Game.Fight.Fight>(FightPath);

            if (_sleeping != null) _sleeping.Waits = true;
        }

        bool Wanted()
        {
            if (Game.Campaigns.Requested.Campaign.Length > 0) CampaignId = Game.Campaigns.Requested.Campaign;

            if (Game.Campaigns.Requested.Place.Length > 0) StartAt = Game.Campaigns.Requested.Place;

            return CampaignId.Length > 0;
        }

        public override void _Ready()
        {
            if (!Wanted())
            {
                GD.Print("walk    no campaign asked for, so nothing is being walked");
                return;
            }

            _board = GetNodeOrNull<Board.Board>(BoardPath);

            if (_board == null)
            {
                GD.PushError($"walk: no board at '{BoardPath}' - there is nowhere to lay a place out");
                return;
            }

            _dm = Find<Game.Dm.Dm>(DmPath);
            _friend = Find<Game.Companion.Companion>(CompanionPath);
            _talk = Find<Game.Dialogue.Talk>(TalkPath);
            _loot = Find<Game.Loot.Stack>(LootPath);
            _room = Find<Game.Room.Room>(RoomPath);
            _sheet = Find<Game.Sheet.Sheet>(SheetPath);
            _tray = Find<Game.Tray.DiceTray>(TrayPath);

            if (_sheet != null) _sheet.Checked += OnChecked;

            // THE BOOK'S LOG IS FED FROM HERE, because this is what actually happens at the table:
            // a line spoken and a verb done to somebody. The bubbles fade on purpose and the book is
            // where they went, so a log nothing writes to would be a re-readable record of nothing.
            if (_talk != null) _talk.Said += OnSaid;

            _response = GetNodeOrNull<Response>(ResponsePath);

            if (_response == null)
            {
                _response = new Response { Name = "Response", DmPath = DmPath };
                AddChild(_response);
            }

            _response.Answered += OnAnswered;

            _campaign = Game.Campaigns.Library.Load(quiet: true).Campaign(CampaignId);

            if (_campaign == null || _campaign.Failed)
            {
                GD.PushError($"walk: '{CampaignId}' " +
                             (_campaign == null ? "is not installed" : "did not load") +
                             " - there is no world to walk");
                return;
            }

            Game.Campaigns.CampaignLocale.Register(_campaign.Folder);

            _world = new Exploring(_campaign.Package,
                                   Content.World.Facts.For(_campaign.Id),
                                   new SeededRng(Seed != 0 ? Seed : (int)Time.GetTicksMsec()));

            _standees = new Standees(_board, Figures(), Game.Campaigns.Library.Load(quiet: true))
            {
                Campaign = _campaign.Id,
            };

            // WHERE THE ROOM READS THE FACTS FROM. The world being walked owns them; the room keeps
            // no copy, because a second fact store is a second answer to "is Bob dead" - so the
            // corkboard and the book read this one through a delegate, the same way saving does.
            if (_room != null)
            {
                _room.Remembering = () => _world?.Facts;

                // AND WHERE YOU ARE STANDING (AX5), the same seam and for the same reason: the room
                // keeps no copy, so "where are we" cannot be answered with a place you have left
                _room.Where = () => _world?.Where?.NameKey(_campaign.Id);

                _room.Plays(_campaign.Id);
            }

            _board.Campaign = _campaign.Id;
            _board.Claims = Touch;

            GD.Print("");
            GD.Print($"walk    {_campaign.Id}: {_campaign.Places.Count} place(s), " +
                     $"{_campaign.Entities.Count} entity(s), {_campaign.Roads.Count} road(s)");

            Enter(StartAt.Length > 0 ? StartAt : _campaign.Places.Ids.FirstOrDefault(), 0, swap: false);
        }

        T Find<T>(NodePath path) where T : Node =>
            path == null || path.IsEmpty ? null : GetNodeOrNull<T>(path);

        // the figures a fight already knows how to make, off the fight standing in this scene, so
        // the minis are named once and it is the table that names them
        MiniMaker Figures() => new MiniMaker(
            new MiniScenes(_sleeping?.HeroPiece, _sleeping?.RabblePiece,
                           _sleeping?.RivalPiece, _sleeping?.DreadPiece),
            Game.Campaigns.Library.Load(quiet: true).Shelf)
        {
            Painted = _board.Paint is ShaderMaterial shader ? shader.Shader : null,
            BasePaint = _board.Paint,
            CellSize = _board.Metrics.CellSize,
        };

        public override void _ExitTree()
        {
            if (_response != null) _response.Answered -= OnAnswered;

            if (_sheet != null) _sheet.Checked -= OnChecked;

            if (_talk != null) _talk.Said -= OnSaid;

            if (_tray != null) _tray.Resolved -= OnTried;

            if (_board != null && _board.Claims == Touch) _board.Claims = null;
        }


        // ---- arriving -------------------------------------------------------------------------

        // A PLACE CHANGE IS THE DM SWAPPING THE MAT, and the first one is not: at boot there is no
        // old mat to take away, so it is simply laid down
        public bool Enter(string place, int arriving = 0, bool swap = true)
        {
            if (_world == null || string.IsNullOrEmpty(place)) return false;

            _response?.Sweep();
            _standees?.Clear();

            Arrival arrival = _world.Enter(place, arriving);

            if (arrival.Refused)
            {
                GD.PushError($"walk: {arrival.Why}");
                return false;
            }

            _board.Plan = arrival.Place;

            if (swap)
            {
                // hung on BEFORE the swap is asked for: a board with no mat lays the place down
                // and says so in the same call, and a handler added afterwards would miss it
                _board.LaidOut += Once(arrival);

                _board.Relay(_world.Map, arrival.Hero, Hands, arrival.Place.Id);
            }
            else
            {
                _board.LayOut(_world.Map, arrival.Hero, arrival.Place.Id);

                Settled(arrival);
            }

            _been.Add(arrival.Place.Id);

            GD.Print("");
            GD.Print($"walk    \"{_text.Get(arrival.Place.NameKey(_campaign.Id))}\" - {arrival}");

            // ARRIVING SOMEWHERE IS SAID (AX2). A sighted player sees the mat change; there is
            // nothing else that would tell anybody else the place is a different place
            Aloud(arrival.Place.NameKey(_campaign.Id));

            return true;
        }

        // the mat is carried by the hands, and the hands belong to the DM; a table with nobody
        // across it swaps the mat with nobody's hands and that is all that changes
        void Hands(Gesture gesture) => _dm?.Does(gesture);

        // the board fires LaidOut every swap, so what is hung on it has to come off again
        System.Action Once(Arrival arrival)
        {
            System.Action handler = null;

            handler = () =>
            {
                _board.LaidOut -= handler;
                Settled(arrival);
            };

            return handler;
        }

        // the mat is down: stand everybody on it, let the DM do what the place asks, and write the
        // evening down, because a place entered is one of the events a save is worth
        void Settled(Arrival arrival)
        {
            _standees.Lay(_world);

            foreach (Cue cue in arrival.Cues) _dm?.Perform(cue, _campaign.Id);

            foreach (string fact in arrival.Facts) GD.Print($"        wrote {fact}");

            // and the sheet learns what this place lets you try. A check the author did not list
            // is printed and plainly not available rather than vanishing off the paper
            _sheet?.Allows(arrival.Place.Checks);

            _room?.Happened();
            _room?.Wrote(Game.Saves.Autosave.When.Arrived);

            // the corkboard's folder, re-read: walking in may have finished an errand, and the pin
            // comes out of a finished one by itself
            _room?.Reconsider();

            Nudge(arrival);

            EmitSignal(SignalName.Arrived, arrival.Place.Id);
        }

        // AN ERRAND THIS PLACE COULD MOVE ON, mentioned once on the way in and never again. The
        // world derives it; all this does is say so, and offer the one that has not been accepted
        void Nudge(Arrival arrival)
        {
            // the companion says it once, on the way in. It is the world that worked out there was
            // anything to say, and the companion that has a mouth
            if (arrival.Nearby.Count > 0) _friend?.SeesAQuestNearby();

            foreach (string quest in arrival.Nearby)
                GD.Print($"        '{quest}' turns in here");

            Quest offered = _campaign.Quests.All
                                     .FirstOrDefault(q => q.StateIn(_world.Facts) == QuestState.Offered);

            if (offered == null) return;

            _facing = null;

            _response.Ask(Offer.Offering(offered.Id));
        }


        // ---- a click on the mat -----------------------------------------------------------------

        // TOUCHED, from outside: the board's click ends up here, and so does a keyboard walk and a
        // headless check. One way in, so a mouse and a key cannot mean different things.
        //
        // true means this took the square and the board should not walk its own piece
        public bool Touch(Cell cell)
        {
            if (_world == null || _fighting != null) return false;

            // a mat in the air has nothing to click on, and a question on the table is answered
            // before anything else happens
            if (_board.Laid is { Ready: false }) return true;

            if (_response != null && _response.Asking) return true;

            Reach reach = Reach.Of(_world, cell);

            switch (reach.Is)
            {
                case Means.Walk:
                    Walk(reach.Stand);
                    return true;

                case Means.Reach:
                    Face(reach);
                    return true;

                case Means.Leave:
                    Walk(reach.Stand);
                    Leave(reach.Way);
                    return true;

                case Means.Shut:
                    Walk(reach.Stand);
                    GD.Print($"walk    the way to '{reach.Way.To}' is not open yet");
                    return true;

                default:
                    GD.Print($"walk    {reach}");
                    return true;
            }
        }

        void Walk(Cell to)
        {
            if (to == _world.Hero) return;

            IReadOnlyList<Cell> route = _world.Walk(to);

            if (route == null)
            {
                _board.Refuse(to, _world.Hero);
                return;
            }

            _board.Walk(_board.Piece, to);
        }

        // up to somebody, and then what they offer goes on the table
        void Face(Reach reach)
        {
            Walk(reach.Stand);

            _facing = reach.It;

            if (reach.It.IsAFoe)
            {
                GD.Print($"walk    '{reach.It.Id}' is a monster standing on a map - this is a fight");

                Begin(_world.Begin(reach.It));
                return;
            }

            Offer offer = Offer.From(reach.It);

            if (offer.Any) { _response.Ask(offer); return; }

            GD.Print($"walk    '{reach.It.Id}' has nothing left to offer");
        }

        void OnAnswered(int index)
        {
            Answer answer = _response.Taken;

            if (answer == null) return;

            switch (answer.Is)
            {
                case Answering.Accept:
                    if (_world.Accept(answer.About))
                        GD.Print($"walk    '{answer.About}' accepted");
                    break;

                case Answering.Decline:
                    GD.Print($"walk    '{answer.About}' declined, for now");
                    break;

                case Answering.Verb:
                    Do(answer.Verb);
                    break;

                default:
                    GD.Print($"walk    {answer}");
                    break;
            }
        }

        // A LINE WENT PAST. It is kept by key rather than by words, so re-reading the log in another
        // language reads in that language rather than in the one it happened in - and it is said out
        // loud on the way past, because a conversation a blind player cannot hear is a conversation
        // they are not in (AX2).
        void OnSaid(string speaker, string key)
        {
            _room?.Story.Said(speaker, key);

            Aloud(key);
        }

        // whatever just appeared in words, said as well. Urgent, because something you did outranks
        // whatever was still being read out
        void Aloud(string key)
        {
            if (_room?.Reach == null || string.IsNullOrEmpty(key)) return;

            _room.Reach.Announce(Game.Access.Spoken.Urgently(_text.Get(key)));
        }

        void Do(Interaction verb)
        {
            if (_facing == null) return;

            Doing did = _world.Do(_facing, verb);

            GD.Print($"walk    {did}");

            if (did.Refused) return;

            if (did.Line.Length > 0) { _dm?.Says(did.Line); Aloud(did.Line); }

            if (did.Node.Length > 0) _talk?.Begin(_campaign.Dialogue, did.Node);

            foreach (string item in did.Took) _loot?.Deal(_campaign.Id, item);

            foreach (Cue cue in did.Cues) _dm?.Perform(cue, _campaign.Id);

            _room?.Happened();

            // written down in the book before anything else happens to the place, because what you
            // did to somebody is worth re-reading whether or not it started a fight
            _room?.Story.Did(verb, _facing.Id, _facing.What?.NameKey);
            _room?.Reconsider();

            if (did.Fight != null) { Begin(did.Fight); return; }

            // whoever you dealt with may have stopped offering what you just did
            _standees.Lay(_world);

            _facing = _world.Standing(_facing.Slot);

            Offer again = Offer.From(_facing);

            if (again.Any) _response.Ask(again);
        }

        void Leave(Exit way)
        {
            Going going = _world.Take(way);

            if (going.Refused) { GD.Print($"walk    {going}"); return; }

            GD.Print($"walk    {going}");

            if (going.LineKey(_campaign.Id) is { Length: > 0 } line) _dm?.Says(line);

            Enter(going.To, going.Arriving);
        }


        // ---- the three you reach for yourself ---------------------------------------------------

        // A CHECK OFF THE SHEET IS THE KIT'S OWN CHECK PRIMITIVE, thrown on the real felt against
        // the number this place allows. There is no second resolver and no second pool: the dice
        // that answer whether you talked the man round are the dice that answer everything else.
        void OnChecked(int which)
        {
            if (_world?.Where == null || _tray == null || _trying != null) return;

            var check = (Content.Sheet.Check)which;

            if (!_world.Where.Allows(check))
            {
                GD.Print($"walk    there is nobody here to {check.Word()}");
                return;
            }

            int against = _world.Where.Against(check);

            _trying = check.For(against);

            Core.Resolution.Pool pool = _trying.PoolFor(_board.Hero);

            if (pool.Count == 0)
            {
                GD.Print($"walk    nothing to throw for {check.Word()}");
                _trying = null;
                return;
            }

            _tray.Resolved += OnTried;

            GD.Print("");
            GD.Print($"walk    {check.Word()} - {_trying.Attribute} + {_trying.Skill}, " +
                     $"{pool.Count} dice vs {against}");

            _tray.Throw(pool);
        }

        void OnTried(Game.Tray.TrayThrow thrown)
        {
            if (_trying == null) return;

            _tray.Resolved -= OnTried;

            Content.Kits.AbilityOutcome outcome = _trying.Read(thrown.Result, thrown.ImpactValue);

            GD.Print($"walk    {_trying.Id}: {outcome.Total} vs {_trying.Against} - " +
                     (outcome.Landed ? "it lands" : "it does not"));

            _dm?.Reacts();

            _trying = null;

            _room?.Happened();
        }


        // ---- a fight, begun and left on the same mat --------------------------------------------

        void Begin(Episode episode)
        {
            if (episode == null || !episode.IsReal)
            {
                GD.Print("walk    nothing here to fight");
                return;
            }

            _response?.Sweep();

            // the figures the walk put down are lifted: the fight musters its own from the same
            // slots, and two figures on one square is the board refusing the second of them
            _standees.Clear();

            _episode = episode;

            _fighting = new Game.Fight.Fight
            {
                Name = "Episode",
                Episode = episode,
                BoardPath = BoardFor(),
                TrayPath = _sleeping?.TrayPath ?? default,
                DmPath = _sleeping?.DmPath ?? default,
                CompanionPath = _sleeping?.CompanionPath ?? default,
                RoomPath = _sleeping?.RoomPath ?? default,
                HeroPiece = _sleeping?.HeroPiece,
                RabblePiece = _sleeping?.RabblePiece,
                RivalPiece = _sleeping?.RivalPiece,
                DreadPiece = _sleeping?.DreadPiece,
                Seed = Seed,
            };

            _fighting.Ended += OnFightEnded;

            // added to the fight's own scene, which is where the paths above are resolved from
            (_sleeping?.GetParent() ?? (Node)this).AddChild(_fighting);

            GD.Print("");
            GD.Print($"walk    {episode}");
        }

        NodePath BoardFor() => _sleeping?.BoardPath ?? BoardPath;

        void OnFightEnded(bool won)
        {
            if (_fighting == null) return;

            int[] fell = _fighting.Pieces
                                  .Where(p => p.Slot > 0 && p.IsDown)
                                  .Select(p => p.Slot)
                                  .ToArray();

            Game.Fight.Fight over = _fighting;

            _fighting = null;

            over.Ended -= OnFightEnded;
            over.QueueFree();

            Ending ending = _world.Fought(_episode, won, fell);

            _episode = null;

            GD.Print("");
            GD.Print($"walk    {ending}");

            foreach (Cue cue in ending.Cues) _dm?.Perform(cue, _campaign.Id);

            // the board is the walk's again, and whoever is left standing goes back on the mat
            _board.Claims = Touch;

            _standees.Lay(_world);

            _room?.Happened();
            _room?.Wrote(Game.Saves.Autosave.When.Fought);

            EmitSignal(SignalName.Fought, won);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            _world == null ? "walk: asleep" : $"walk: {_world}";
    }
}
