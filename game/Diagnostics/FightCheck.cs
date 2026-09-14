using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Space;
using Content.Encounters;
using Game.Board;
using Game.Fight;
using Game.Localization;
using Game.Tray;

// Game.Board and Game.Fight are namespaces that each hold a type of the same name, so both need
// an alias to be referred to from anywhere else under Game - see Fight.cs
using BoardNode = Game.Board.Board;
using FightNode = Game.Fight.Fight;

namespace Game.Diagnostics
{
    // THE FIGHT, PLAYED HEADLESS AND HELD TO WHAT THE FELT SAYS (COMBAT_LOOP.md, the verify lists).
    //
    // Every milestone in Phase C ends in something to watch, and watching is the developer's job -
    // no test can tell you whether two actions a round feels like heroism. What a test CAN do is
    // hold the invisible half to the visible one:
    //
    //   C0  the damage dealt is the number on the die with the ring round it
    //   C1  a Condition steps the die down, the smaller die reaches the felt, and the mark beside
    //       the piece says what the localizer says rather than a hardcoded word
    //   C2  a Rabble goes down to any hit and to nothing else, a Rival soaks, a foe takes its one
    //       action once, and a downed piece leaves its square
    //   C3  the order is thrown once and written down the side of the map in words rather than
    //       keys, everybody in the fight is in it, and a readied strike costs a reaction and
    //       nobody's turn
    //   C4  all four Nerve spends are reachable and each one does what it says - a fourth die
    //       genuinely reaches the felt, a re-throw moves exactly one die, a shrug cancels a
    //       Trouble, a push buys an action - and Nerve never changes by itself
    //   C5  a caster throws Heart + Channeling + focus, the Heart die steps down every cast, and
    //       a breather puts it back
    //   C6  a Dread takes two actions to everybody else's one and changes at exactly half Vigor,
    //       once, with bigger dice on the other side of it
    //   P0  a monster that came out of a campaign's JSON stands on the board and is fought like
    //       any other, with nothing about it in the code
    //   P1  a weapon authored in data is a bigger die on the felt and armour is a higher Defense,
    //       and what a fallen monster was carrying is the same on the same seed
    //
    // Those are the things that go wrong quietly, look fine in motion, and are miserable to notice
    // by eye.
    //
    // IT PLAYS THE REAL TABLE. It instances table.tscn - the shipped scene, the real board, the
    // real tray with real physics dice landing on real felt - and drives it through the same entry
    // point a mouse click uses, `Board.Claims`. A mock table would be a different game. This is
    // DiceFairness' argument applied to the fight: the thing measured is the thing played.
    //
    // Run it with check-fight.ps1.
    // IT WATCHES THROUGH THE SEAM. `ICombatObserver` is how the rules narrate, and a check is
    // exactly the sort of thing it exists for - so whose turn it is and what a blow did arrive as
    // events rather than being sampled. The first version polled `Encounter.Acting` once a frame
    // and raced `Fight._Process`: a foe whose whole turn happened between two samples looked like
    // a foe that never got one, and the check reported it. What is still polled is only what the
    // rules do not narrate - the felt, the pieces and the mat
    public partial class FightCheck : HeadlessCheck, ICombatObserver
    {
        protected override string Subject => "fight";

        // the shipped scene, named in the check's own scene rather than loaded by path here, so
        // this file has no opinion about which table is the table
        [Export] public PackedScene Table { get; set; }

        // how many complete fights to play. every hero swing is a real handful of physics dice
        // settling, so this is what decides how long the check takes - a fight is a dozen or so
        // throws at about a second and a half each
        [Export] public int Fights { get; set; } = 3;

        // THE LAST FIGHT IS PLAYED BY THE CASTER (C5). The Barbarian is untrained in Channeling -
        // two dice and no leftover, which is the rules working correctly and nothing to look at -
        // so the placeholder roster has a Mage in it and this is what picks him up. Set to an
        // empty string to play every fight as whoever the table's board names
        [Export] public string CastingHero { get; set; } = EngineIds.Mage;

        // AND THE LAST FIGHT IS THE BOSS (C6). One Dread, alone - no Rabble and no Rival - because
        // the phase change fires at half its Vigor and a hero cut down in round three never gets
        // it there. A duel is not what a boss fight should be at the table (the sim's boss report
        // gives it two Rabble for exactly that reason) and it is what a CHECK of one wants.
        //
        // Off the board means no boss, and then this check never sees one
        [Export] public Vector2I BossAt { get; set; } = new Vector2I(5, 2);

        // P0's VERIFY, AUTOMATED: "hand-write a monster JSON, load it, and fight it on the board
        // with no code change". One fight is given a foe out of a campaign folder instead of the
        // engine's Rival - by id, through the scene's own export, which is the same thing an
        // author would type. Skipped when no campaign on disk has it, because a game with no
        // campaigns installed has to run this check too
        [Export] public string CampaignFoe { get; set; } = "ashfall.ghoul";

        // P1's VERIFY, AUTOMATED: the campaign fight's hero carries gear authored in a file, so the
        // check can watch a bigger die reach the felt and a higher Defense reach the fight. Empty
        // for either leaves the statblock's own
        [Export] public string CampaignWeapon { get; set; } = "cold_iron_axe";

        [Export] public string CampaignArmour { get; set; } = "scale_coat";

        // P2's VERIFY, AUTOMATED: "author a second, differently-shaped map in data and load it -
        // the board, the spawns and the tagged cells all come from the file, no scene edited and
        // no code touched". The campaign fight is played in the campaign's own room, which is a
        // different shape from the shipped cellar and has its spawn slots drawn into it.
        //
        // Both empty plays every fight in the shipped room, which is what a game with no campaigns
        // installed does
        [Export] public string CampaignId { get; set; } = "ashfall";

        // P4's VERIFY, AUTOMATED: "load a campaign from a folder and play an encounter defined
        // entirely by its data - monsters, map, placements, triggers, loot - with no code specific
        // to that campaign anywhere". ONE STRING is what this check knows about ashfall, and it is
        // the name of an encounter. Everything else - which room, who is in it, where each of them
        // stands, what they drop - is read out of the folder and checked against the folder
        [Export] public string CampaignEncounter { get; set; } = "ash_yard";

        // P6's VERIFY, AUTOMATED: "save mid-fight, quit, reload - the fight resumes with the same
        // dice on the felt, the same Vigor and conditions, the same board".
        //
        // QUITTING IS PLAYED BY A SECOND TABLE. The check writes a save out of the fight it is
        // playing, then instances `table.tscn` again from nothing, hands the save to it, and holds
        // the two side by side. That is a stronger test than reloading in place: the second table
        // has never seen the first, so anything the restore forgot to say is simply not there
        [Export] public bool SaveAndReload { get; set; } = true;

        // which round to take it in. 2 rather than 1 because a save taken before anybody has done
        // anything proves very little - by round 2 somebody has been hit, somebody has moved, and
        // the turn order has come round once
        [Export] public int SaveInRound { get; set; } = 2;

        // PLAY IT STRAIGHT AND MEASURE IT. Everything this check does to reach a milestone is a
        // handicap: it winds the hero on purpose to watch a die shrink, gives up a turn to arm a
        // readied strike, spends Nerve on whatever is reachable rather than on whatever would
        // help, and plays the last fight as a caster whose melee is two dice. Those are the right
        // things for a check to do and they make the win rate meaningless.
        //
        // With this on it does none of them: the hero swings at the nearest thing until somebody
        // falls over. That is the number to hold against SIMULATION.md section 5 - roughly 85%
        // survival at about 6.6 rounds, plus whatever the geometry costs, which the sim has no
        // model for. Run it as `.\check-fight.ps1 -Plain -Fights 10`
        [Export] public bool Plain { get; set; }

        // a fight that stops moving means the felt never settled, or the loop is waiting on
        // something that is never going to happen. the fairness sweep has the same fail-safe, and
        // this one is measured since the last thing that HAPPENED rather than since the start
        [Export] public double StuckSeconds { get; set; } = 45.0;

        // the same one the fight writes its marks with, so this compares what is on the mat against
        // what the translation system would say rather than against a second opinion
        readonly ILocalizer _text = new GodotLocalizer();

        BoardNode _board;

        DiceTray _tray;

        FightNode _fight;

        Actor _hero;

        // ---- what the whole run adds up to ----

        int _fought;

        int _won;

        int _rounds;

        int _swings;

        int _hits;

        int _rabbleFelled;

        int _readied;

        int _nerveSpent;

        int _nerveBanked;

        // C4: each of the four spends, ticked off once. The point is not how many times - it is
        // that every one of them was reachable through the gestures the game actually has
        bool _spentOnHeart;

        bool _spentOnReroll;

        bool _spentOnPush;

        bool _shrugged;

        // the biggest pool that reached the felt, so a fourth die is provable rather than hoped for
        int _widestPool;

        // ---- C5: the caster's fight ----

        bool _casting;

        bool _fighting;

        bool _campaignFight;

        int _campaignFoesFought;

        bool _campaignRoom;

        int _campaignRoomsFought;

        bool _saved;

        int _saves;

        // how many turns ended with nowhere to go. A few is a corridor; a great many is a room
        // nobody can cross, which is worth failing over
        int _blocked;

        public const int BlockedTurnsWorthReporting = 12;

        int _casts;

        Die _heartBefore;

        bool _breathed;

        int _castsSeen;

        int _castingFights;

        int _phases;

        // WHICH bosses have turned, by reference, because "twice" is a question about one Dread
        // and not about a run (see PhaseChanged)
        readonly HashSet<Actor> _turned = new HashSet<Actor>(ReferenceEqualityComparer.Instance);

        int _bossFights;

        int _bossActions;

        int _reactions;

        // the order cannot be checked at muster: the hero's initiative is a real throw on the real
        // tray, so the fight has not begun yet when the room is set up
        bool _orderChecked;

        // a Trouble is on the felt and the window is open on it
        bool _sawTrouble;

        // ---- the swing in the air ----

        bool _inFlight;

        Actor _target;

        int _targetVigorBefore;

        // the throw that opened the swing. An exploding Impact die puts more throws on the felt
        // for the SAME swing, so the opening one is the one whose Impact die the damage is
        // measured against, and the rest are the chain
        TrayThrow _opening;

        int _explosions;

        int _explosionsSeen;

        // ---- C1: the first swing thrown from beside a foe is the condition swing ----

        Die _mightBefore;

        bool _conditionSwing;

        bool _conditionChecked;

        // ---- who has been seen acting, so a foe that never gets a turn is caught ----

        int _watchingRound;

        readonly HashSet<Actor> _acted = new HashSet<Actor>();

        int _turnsWatched;

        int _blowsWatched;

        bool _finished;

        double _waited;

        public override void _Ready()
        {
            if (Table == null)
            {
                Problem("no table scene set on the check - there is nothing to play on");
                Finish();
                return;
            }

            ReadCommandLine(OS.GetCmdlineUserArgs());

            Restart();
        }

        // A FRESH TABLE FOR EVERY FIGHT. The alternative is reviving the dead and putting the
        // pieces back, which is a second, private idea of how a room is set up - and the one that
        // would quietly stop matching the real one
        void Restart()
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            // THE HERO IS CHOSEN BEFORE THE BOARD LOADS, which means before AddChild - Godot
            // readies a subtree inside that call, and the board recruits in its _Ready. Walking
            // the un-added instance is the only moment an export can still be set
            Node table = Table.Instantiate();

            // the last fight is the boss's and the one before it is the caster's, so a default
            // run of three covers a plain fight, a caster and a Dread
            // ONE THING PER FIGHT, and never two at once - a Mage fighting a campaign's ghoul
            // would be two milestones sharing one set of dice, and whichever failed would be the
            // harder to read for it. Last is the boss, the one before it is the caster, and the
            // first is the campaign's, when there are enough fights to go round
            _fighting = BossAt != FightNode.Nowhere && !Plain && _fought == Fights - 1;

            _casting = !Plain && !string.IsNullOrEmpty(CastingHero) && Fights >= 2
                       && _fought == Fights - 2;

            // THE CAMPAIGN'S OWN ROOM comes first, because it is the one a campaign always has:
            // P2's map and P4's encounter arrive together, since an encounter is what names a map
            _campaignRoom = !Plain && Fights >= 3 && _fought == 0 && Planned() != null;

            // AND THE NAMED-FOE CHECKS RIDE ON TOP OF IT (P0, P1). They ask after one particular
            // monster and one particular axe, which only the campaign that ships them has - so
            // they are gated separately, and a second campaign pointed at with `--campaign=` plays
            // its own encounter without being asked for ashfall's ghoul (P7)
            _campaignFight = _campaignRoom && !string.IsNullOrEmpty(CampaignFoe)
                             && Game.Campaigns.Library.Load(quiet: true).Has(CampaignFoe);

            if (_casting) _castingFights++;

            foreach (Node node in Descendants(table))
            {
                if (_casting && node is BoardNode board) board.HeroId = CastingHero;

                // P2 AND P4: THE ROOM AND EVERYBODY IN IT COME OUT OF THE CAMPAIGN. Two strings,
                // set before the board's _Ready has run - the board reads the encounter, the
                // encounter names the map, and the fight musters from the same plan. Nothing else
                // here knows which room it is standing in or who is in it
                if (node is BoardNode room3)
                {
                    if (_campaignRoom)
                    {
                        room3.Campaign = CampaignId;
                        room3.Encounter = CampaignEncounter;
                    }
                    else
                    {
                        // THE CASTER'S FIGHT AND THE BOSS'S ARE IN THE ROOM THAT SHIPS, and stay
                        // there even when the run was launched with `--campaign=` (P7). A check
                        // that plays three different fights is the one caller that has to say so
                        room3.Fixed = true;
                    }
                }

                if (node is not FightNode room) continue;

                if (_campaignFight)
                {
                    room.HeroWeapon = CampaignWeapon;
                    room.HeroArmour = CampaignArmour;
                }

                // and the scene's own squares are cleared, so a failure to read the encounter is
                // an empty room and a loud one rather than the shipped cellar's four Rabble
                // quietly standing in for the campaign's
                if (_campaignRoom)
                {
                    room.Spawns = new Godot.Collections.Array<string>();
                    room.RivalAt = FightNode.Nowhere;
                    room.DreadAt = FightNode.Nowhere;
                    room.RabbleAt = new Godot.Collections.Array<Vector2I>();
                }

                if (_fighting)
                {
                    _bossFights++;
                    room.DreadAt = BossAt;
                    room.RivalAt = FightNode.Nowhere;
                    room.RabbleAt = new Godot.Collections.Array<Vector2I>();
                }
            }

            AddChild(table);

            // after the table's own _Ready has run, which is what AddChild above guarantees: Godot
            // readies a subtree bottom-up before the call returns
            _board = Find<BoardNode>();
            _tray = Find<DiceTray>();
            _fight = Find<FightNode>();

            if (_board == null || _tray == null || _fight == null)
            {
                Problem($"the table is missing a board ({_board != null}), a tray ({_tray != null}) " +
                        $"or a fight ({_fight != null})");
                Finish();
                return;
            }

            _hero = _fight.Hero;

            if (_fight.Foes.Count == 0)
            {
                Problem("the fight mustered no foes - there is nobody to swing at");
                Finish();
                return;
            }

            // LAST, so the fight's own handler has already applied the outcome by the time this one
            // looks. Delegates fire in subscription order and the fight subscribed in its _Ready,
            // which ran inside the AddChild above
            _tray.Resolved += OnThrown;
            _fight.Watch(this);

            _inFlight = false;
            _target = null;
            _opening = null;
            _orderChecked = false;
            _sawTrouble = false;
            _casts = 0;
            _breathed = false;
            _watchingRound = 0;
            _acted.Clear();
            _explosions = 0;
            _waited = 0.0;
            _saved = false;

            CheckTheRoom();

            if (_fighting && _fight.Boss == null)
                Problem("the boss fight was set up and the fight mustered no Dread");

            // P0: THE MONSTER THE CAMPAIGN WROTE IS ON THE BOARD, and is an Actor like any other -
            // its id is scoped by its campaign, its name key falls out of that id, and the fight
            // has no idea it came from a file
            if (_campaignFight)
            {
                Actor foe = _fight.Foes.FirstOrDefault(f => f.Id == CampaignFoe);

                if (foe == null)
                {
                    Problem($"the campaign fight named '{CampaignFoe}' and no such foe was mustered");
                }
                else
                {
                    _campaignFoesFought++;

                    if (!Content.Campaigns.ContentId.IsScoped(foe.Id))
                        Problem($"'{foe.Id}' came out of a campaign and is not scoped by it");

                    if (_board.CellOf(_fight.PieceFor(foe)) == null)
                        Problem($"{foe.DebugName} was mustered onto no square");

                    GD.Print($"campaign    {foe.DebugName} from " +
                             $"{Content.Campaigns.ContentId.CampaignOf(foe.Id)} is on the board - " +
                             $"{foe.Tier}, vigor {foe.MaxVigor}, def {foe.Defense}, " +
                             $"names itself '{foe.NameKey}'");

                    CheckTheGear();
                }
            }

            if (_campaignRoom) CheckTheRoomCameFromTheFile();

            if (_casting && !TheEffect.Offered(_hero))
                Problem($"the casting fight was set up with {_hero.DebugName}, who is not trained " +
                        "in Channeling - there is nothing to look at");

            GD.Print("");
            GD.Print($"fight {_fought + 1} of {Fights} - {_hero.DebugName} against " +
                     $"{_fight.Foes.Count(f => f.Tier == Tier.Rabble)} Rabble, " +
                     $"{_fight.Foes.Count(f => f.Tier == Tier.Rival)} Rival and " +
                     $"{_fight.Foes.Count(f => f.Tier == Tier.Dread)} Dread");
        }

        // P2 AND P4: THE ROOM AND THE ENCOUNTER CAME OUT OF THE FOLDER. Four things have to be
        // true, and each of them is a separate way for the loader to be a no-op that looks like it
        // worked:
        //
        //   the board read the ENCOUNTER, and the encounter is the one that was asked for
        //   it opened the map that encounter names, out of the campaign, and not the shipped one
        //   the board is the SHAPE that file draws, and a different one from the cellar's
        //   every foe the encounter places is standing on the square the map drew for its slot
        //
        // The last is what makes the first three worth anything. A board can load a stranger's map
        // and still muster its foes onto squares out of a scene, and then the room is data and the
        // encounter is not - which is most of the way to nothing.
        //
        // AND NOTHING HERE NAMES A MONSTER, A SQUARE OR A ROOM. Every expectation is read back out
        // of the plan and the map, so this is a check that the game agrees with the folder rather
        // than a check that both agree with a list in a diagnostic - which would pass just as well
        // if the folder were ignored entirely
        void CheckTheRoomCameFromTheFile()
        {
            EncounterPlan plan = _board.Plan;

            if (plan == null || plan.Id != CampaignEncounter)
            {
                Problem($"the campaign fight asked for the encounter '{CampaignEncounter}' and " +
                        $"the board is playing '{plan?.Id ?? "nothing"}' - it fell back to a map");
                return;
            }

            if (_board.Loaded == null ||
                System.IO.Path.GetFullPath(_board.Loaded) != Planned())
            {
                Problem($"'{CampaignEncounter}' is fought on {plan.Map} and the board loaded " +
                        $"'{_board.Loaded}' - it fell back to the map that ships");
                return;
            }

            MapLayout map = _board.Map;

            // the shipped room, read the same way the board reads it, so "differently shaped" is
            // measured against the real thing rather than against two numbers written down here
            MapReader.TryRead(Godot.FileAccess.GetFileAsString(_board.MapPath), out MapLayout cellar,
                              out string _);

            if (cellar != null && map.Columns == cellar.Columns && map.Rows == cellar.Rows)
                Problem($"{plan.Map} is {map.Columns} x {map.Rows} and so is the shipped room - " +
                        "P2 wants a DIFFERENTLY shaped map, or this proves nothing");

            if (map.Spawns.Count == 0)
            {
                Problem($"{plan.Map} has no spawn slots drawn in it - the encounter has nowhere " +
                        "to stand and the fight fell back to the scene's squares");
                return;
            }

            // hero included: his square is slot 0 by the same grammar, and he is placed by the
            // board rather than by the fight, so this is a second loader checked in one line
            Cell? hero = _board.CellOf(_fight.PieceFor(_hero));

            if (hero != map.Start)
                Problem($"{_hero.DebugName} started on {hero} and {plan.Map} starts him " +
                        $"on {map.Start}");

            foreach (Placement placement in plan.Placements)
            {
                string id = Content.Campaigns.ContentId.Scoped(CampaignId, placement.Monster);
                Cell? want = map.SpawnAt(placement.Slot);

                if (want == null)
                {
                    Problem($"the encounter puts '{placement.Monster}' on spawn {placement.Slot} " +
                            $"and {plan.Map} has no such slot");
                    continue;
                }

                if (!_fight.Foes.Any(f => f.Id == id &&
                                          _board.CellOf(_fight.PieceFor(f)) == want))
                    Problem($"spawn {placement.Slot} of {plan.Map} is {want} and no " +
                            $"'{id}' is standing on it");
            }

            if (_fight.Foes.Count != plan.Placements.Count)
                Problem($"'{plan.Id}' places {plan.Placements.Count} and the fight mustered " +
                        $"{_fight.Foes.Count}");

            _campaignRoomsFought++;

            GD.Print($"        room: {plan} out of {CampaignId}/, {map} - no scene edited and no " +
                     "code touched");

            // AND THE HALF NOTHING RUNS YET, printed rather than asserted. `EncounterPlan` says
            // plainly that the runner is Phase R's; what P4 owns is that the slot exists, parses,
            // and survives the round trip from the file to here
            foreach (Trigger trigger in plan.Triggers)
                GD.Print($"        trigger: {trigger} - read and carried; nothing runs it until " +
                         "the campaign shell (Phase R)");

            foreach (Cue cue in plan.Cues)
                GD.Print($"        cue: {cue} - a slot for a gesture the DM has not learned yet " +
                         "(Phase D)");
        }

        // P6: SAVE IT, OPEN IT AGAIN ON A TABLE THAT HAS NEVER SEEN THIS FIGHT, AND COMPARE.
        //
        // Every assertion below is the same question asked of two tables: does the one that read
        // the file agree with the one that wrote it? Nothing is compared against a constant, so
        // this cannot pass by both sides being wrong in the same way, and nothing names ashfall -
        // whatever fight is being played is the fight that is saved.
        //
        // The second table is freed immediately. It exists for the length of this method, which is
        // the whole of what "quit and reload" needs to mean for a check
        void SaveAndReloadIt()
        {
            Content.Saves.SaveGame taken = _fight.Save();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                                                 "fightcheck-" + System.Guid.NewGuid().ToString("N") + ".json");

            Content.Schema.ContentProblem wrote = Content.Saves.SaveWriter.To(path, taken);

            if (wrote != null)
            {
                Problem($"the save could not be written - {wrote}");
                return;
            }

            try
            {
                Content.Schema.Read<Content.Saves.SaveGame> read =
                    Content.Saves.SaveReader.From(path);

                if (!read.Any)
                {
                    Problem($"the save was written and could not be read back - " +
                            string.Join(" | ", read.Problems.Select(p => p.ToString())));
                    return;
                }

                foreach (Content.Schema.ContentProblem problem in read.Problems)
                    Problem($"the save this build wrote has something in it this build cannot " +
                            $"read: {problem}");

                Reopened(taken, read.Value, new System.IO.FileInfo(path).Length);
            }
            finally
            {
                try { System.IO.File.Delete(path); } catch { /* a temp file */ }
            }
        }

        void Reopened(Content.Saves.SaveGame taken, Content.Saves.SaveGame read, long bytes)
        {
            Node table = Table.Instantiate();

            foreach (Node node in Descendants(table))
            {
                if (node is BoardNode board)
                {
                    board.Campaign = read.Campaign;
                    board.Encounter = read.Encounter;
                }

                if (node is FightNode room)
                {
                    room.Spawns = new Godot.Collections.Array<string>();
                    room.RivalAt = FightNode.Nowhere;
                    room.DreadAt = FightNode.Nowhere;
                    room.RabbleAt = new Godot.Collections.Array<Vector2I>();
                    room.Resuming = read;
                }
            }

            AddChild(table);

            try
            {
                Compare(table, taken, bytes);
            }
            finally
            {
                RemoveChild(table);
                table.QueueFree();
            }
        }

        void Compare(Node table, Content.Saves.SaveGame taken, long bytes)
        {
            FightNode again = In<FightNode>(table);
            BoardNode board = In<BoardNode>(table);

            if (again == null || board == null)
            {
                Problem("the reloaded table has no fight or no board on it");
                return;
            }

            Encounter resumed = again.Encounter;

            if (resumed == null || resumed.Round == 0)
            {
                Problem("the reloaded table is not in a fight - the save was taken mid-fight");
                return;
            }

            Encounter live = _fight.Encounter;

            Same("the round", live.Round, resumed.Round);
            Same("whose turn it is", live.Acting?.DebugName, resumed.Acting?.DebugName);
            Same("the actions left", live.ActionsLeft, resumed.ActionsLeft);
            // THE STANDING ONLY. A foe that had already fallen is not in the save at all (a save
            // is what is in the room), so the reloaded encounter musters it, kills it, and leaves
            // it at the back of the order where `Resume` appended it. Whose turn comes when is a
            // question about the living
            Same("the turn order", Names(live.Order.Where(a => !a.IsDown)),
                 Names(resumed.Order.Where(a => !a.IsDown)));

            SameActor("the hero", _hero, again.Hero, board, again);

            // MATCHED BY THE SPAWN SLOT, the same way the restore matched them, so this compares
            // the two tables rather than comparing the restore against itself
            foreach (Piece piece in _fight.Pieces.Where(p => !ReferenceEquals(p.Actor, _hero)))
            {
                if (piece.Actor.IsDown) continue;

                Piece twin = again.Pieces.FirstOrDefault(p => p.Slot == piece.Slot);

                if (twin == null)
                {
                    Problem($"{piece.Actor.DebugName} was on spawn {piece.Slot} and the reloaded " +
                            "table has nobody there");
                    continue;
                }

                SameActor($"spawn {piece.Slot}", piece.Actor, twin.Actor, board, again);
            }

            foreach (Piece piece in again.Pieces.Where(p => !ReferenceEquals(p.Actor, again.Hero)))
                if (!piece.Actor.IsDown &&
                    !_fight.Pieces.Any(p => p.Slot == piece.Slot && !p.Actor.IsDown))
                    Problem($"the reloaded table has {piece.Actor.DebugName} standing on spawn " +
                            $"{piece.Slot} and this one does not - the fallen came back");

            // AND THE FELT. The faces are compared rather than the verdict, and then the verdict
            // is compared too - because the verdict is DERIVED from the faces by the same
            // arithmetic on both sides, and two derivations that disagree would mean the reading
            // itself had moved (PoolResult.From)
            Same("the dice on the felt", Faces(taken), Faces(_fight.Save()));

            if (taken.Felt.Count > 0)
            {
                if (again.Restored == null)
                    Problem($"{taken.Felt.Count} dice were on the felt and the reloaded table " +
                            "read none of them back");
                else
                {
                    TrayThrow was = _tray.LastThrow;

                    if (was != null)
                    {
                        Same("the total on the felt", was.Result.Total, again.Restored.Result.Total);
                        Same("the impact die", was.Result.Impact, again.Restored.Result.Impact);
                        Same("the snagged die", was.SnaggedSlot, again.Restored.SnaggedSlot);
                    }
                }
            }

            _saves++;

            GD.Print($"        save: round {taken.Round}, {taken.Foes.Count} standing, " +
                     $"{taken.Felt.Count} dice on the felt, {bytes} bytes - written, read back on " +
                     "a table that had never seen this fight, and the two agree");
        }

        // one actor against the same actor on the other table. Everything a fight can do to
        // somebody, which is the list a save has to carry
        void SameActor(string who, Actor was, Actor now, BoardNode board, FightNode again)
        {
            if (now == null) { Problem($"{who} is not on the reloaded table"); return; }

            Same($"{who}: id", was.Id, now.Id);
            Same($"{who}: vigor", was.Vigor, now.Vigor);
            Same($"{who}: nerve", was.Nerve, now.Nerve);
            Same($"{who}: conditions", string.Join("+", was.Conditions),
                 string.Join("+", now.Conditions));
            Same($"{who}: notches", was.Notches, now.Notches);
            Same($"{who}: strain", was.Strain, now.Strain);
            Same($"{who}: weapon", was.Weapon, now.Weapon);
            Same($"{who}: defense", was.Defense, now.Defense);
            Same($"{who}: might", was.Attribute(Attr.Might), now.Attribute(Attr.Might));
            Same($"{who}: heart", was.Attribute(Attr.Heart), now.Attribute(Attr.Heart));
            Same($"{who}: satchel", string.Join(",", was.Satchel), string.Join(",", now.Satchel));
            Same($"{who}: square", _board.CellOf(_fight.PieceFor(was)),
                 board.CellOf(again.PieceFor(now)));
        }

        void Same<T>(string what, T was, T now)
        {
            if (!EqualityComparer<T>.Default.Equals(was, now))
                Problem($"{what} was '{was}' when the save was taken and is '{now}' after " +
                        "reloading it");
        }

        static string Names(IEnumerable<Actor> order) =>
            string.Join(", ", order.Select(a => a.DebugName));

        static string Faces(Content.Saves.SaveGame save) =>
            string.Join(" ", save.Felt.Select(d => $"{d.Trait}:{d.Die}:{d.Value}"));

        static T In<T>(Node root) where T : Node
        {
            foreach (Node node in Descendants(root))
                if (node is T found) return found;

            return null;
        }

        // WHERE THE ENCOUNTER'S MAP IS, or null when no campaign on disk has that encounter. Used
        // twice: once to decide whether there is a P4 fight to play at all, and once to check the
        // board opened THAT file rather than something that happened to parse.
        //
        // Through `Library` rather than by building a path, which is the point of the milestone -
        // the check finds the room the same way the game does, by asking a campaign what its
        // encounter is fought on
        string Planned()
        {
            if (string.IsNullOrWhiteSpace(CampaignId) || string.IsNullOrWhiteSpace(CampaignEncounter))
                return null;

            Game.Campaigns.Loaded campaign = Game.Campaigns.Library.Load(quiet: true).Campaign(CampaignId);

            if (campaign == null || campaign.Failed) return null;

            EncounterPlan plan = campaign.Encounters.Of(CampaignEncounter);

            if (plan == null) return null;

            string path = System.IO.Path.Combine(campaign.Folder, BoardNode.MapsFolder,
                                                 plan.Map + BoardNode.MapExtension);

            return System.IO.File.Exists(path) ? System.IO.Path.GetFullPath(path) : null;
        }

        // P1: A BIGGER DIE IN A FILE IS A BIGGER DIE IN THE POOL, and armour is a higher number
        // to beat. Both against the hero's own statblock rather than against a constant, because
        // what he started with is whatever the roster said
        void CheckTheGear()
        {
            var plain = new Content.Monsters.Rosters(new BuiltInArchetypes());

            if (!plain.Has(_hero.Id)) return;

            Actor bare = plain.Create(_hero.Id);

            if (!string.IsNullOrEmpty(CampaignWeapon))
            {
                if (_hero.WeaponId != CampaignWeapon)
                    Problem($"the hero was given '{CampaignWeapon}' and is holding '{_hero.WeaponId}'");
                else if (_hero.Weapon == bare.Weapon)
                    Problem($"the hero swapped {bare.Weapon.Label()} for '{CampaignWeapon}' and it " +
                            "is the same size - a better weapon is a bigger die (CORE_RULES pillar 3)");
                else
                    GD.Print($"        gear: {bare.WeaponId} {bare.Weapon.Label()} -> " +
                             $"{_hero.WeaponId} {_hero.Weapon.Label()}, and the pool with it");
            }

            if (!string.IsNullOrEmpty(CampaignArmour))
            {
                if (_hero.Defense <= bare.Defense)
                    Problem($"the hero is wearing '{CampaignArmour}' and his Defense is " +
                            $"{_hero.Defense} against a bare {bare.Defense}");
                else
                    GD.Print($"        armour: defense {bare.Defense} -> {_hero.Defense}");
            }
        }

        // P1: A RESEEDED RUN DROPS THE SAME THING.
        //
        // Asked of the table's own loaded campaign rather than hoped for in play: a monster with a
        // one-in-six axe on it will not drop one most evenings, and a check that only reported what
        // happened to fall would pass for years without ever testing the property. What matters is
        // that the draw goes through `IRng` - so the same seed gives the same answer and a
        // different seed does not - which is the rule every source of chance in this game keeps
        // (CONVENTIONS.md 6).
        //
        // What actually fell is reported beside it, because seeing a real drop is the other half
        void CheckTheLoot()
        {
            if (_hero.Satchel.Count > 0) _looted = string.Join(", ", _hero.Satchel);

            if (_lootChecked) return;

            _lootChecked = true;

            var library = Game.Campaigns.Library.Load(quiet: true);
            Content.Items.LootTable table = library.LootFor(CampaignFoe);

            if (table.IsEmpty)
            {
                GD.Print($"        '{CampaignFoe}' carries nothing, so there was no table to check");
                return;
            }

            string[] first = Draws(table, 4242);
            string[] again = Draws(table, 4242);
            string[] other = Draws(table, 99);

            if (!first.SequenceEqual(again))
                Problem($"'{CampaignFoe}' drew {string.Join("/", first)} on seed 4242 and " +
                        $"{string.Join("/", again)} on the same seed - loot is not going through IRng");

            if (first.SequenceEqual(other))
                Problem($"'{CampaignFoe}' drew the same twelve things on two different seeds, " +
                        "which is a table with one entry in it rather than a seeded draw");

            GD.Print($"        loot table: {table} - same seed, same draw");
        }

        static string[] Draws(Content.Items.LootTable table, int seed)
        {
            var rng = new Core.Dice.SeededRng(seed);

            return Enumerable.Range(0, 12).Select(_ => table.Draw(rng) ?? "-").ToArray();
        }

        bool _lootChecked;

        string _looted = "";

        // the room as it is set up, before a die is thrown
        void CheckTheRoom()
        {
            foreach (Piece piece in _fight.Pieces)
            {
                bool tracked = piece.Actor.Tier.HasHealthTrack();

                if (tracked && piece.Vigor == null)
                    Problem($"{piece.Actor.DebugName} has a health track and no tally beside it");

                if (!tracked && piece.Vigor != null)
                    Problem($"{piece.Actor.DebugName} has no health track and a tally beside it anyway");

                if (tracked && piece.Vigor != null && piece.Vigor.Max != piece.Actor.MaxVigor)
                    Problem($"{piece.Actor.DebugName}'s tally is {piece.Vigor.Max} long for " +
                            $"{piece.Actor.MaxVigor} vigor");

                if (_board.CellOf(piece.Mini) == null)
                    Problem($"{piece.Actor.DebugName} was mustered onto no square at all");
            }

            // ordinals, because "Rabble 3" has to come out of one key with a {0} in it rather than
            // an integer glued onto a translated name
            foreach (Actor mook in _fight.Foes.Where(f => f.Tier == Tier.Rabble))
                if (mook.Ordinal <= 0)
                    Problem($"a Rabble was mustered unnumbered - '{mook.NameKey}' takes no ordinal");
        }

        // C3: THE ORDER IS WRITTEN DOWN THE SIDE OF THE MAP, in words. The margin is where a
        // hardcoded name or a missing locale line would show up, and it is the first place in the
        // game that formats an ordinal into a key - "Rabble 3" out of one string with a {0} in it
        // rather than an integer glued onto a translated word (CONVENTIONS.md 7)
        void CheckTheOrder()
        {
            Encounter fight = _fight.Encounter;
            TurnOrder margin = _fight.Order;

            if (fight == null || margin == null)
            {
                Problem("the fight began with no turn order at all");
                return;
            }

            if (fight.Round == 0)
            {
                Problem("the fight was mustered and never began - the order was never thrown");
                return;
            }

            if (margin.Listed.Count != fight.Order.Count)
                Problem($"{fight.Order.Count} in the fight and {margin.Listed.Count} written in the margin");

            foreach (Actor actor in fight.Order)
            {
                Label3D line = margin.LineFor(actor);

                if (line == null)
                {
                    Problem($"{actor.DebugName} is in the order and not in the margin");
                    continue;
                }

                string words = actor.Ordinal > 0
                    ? _text.Format(actor.NameKey, actor.Ordinal)
                    : _text.Get(actor.NameKey);

                if (words == actor.NameKey)
                    Problem($"'{actor.NameKey}' has no words in the locale - the margin would read the key");

                if (line.Text != words)
                    Problem($"the margin reads '{line.Text}' for {actor.DebugName} and the localizer " +
                            $"says '{words}'");

                // a numbered foe's line must actually carry its number, or four Rabble are four
                // identical words in a list nobody can read
                if (actor.Ordinal > 0 && !line.Text.Contains(actor.Ordinal.ToString()))
                    Problem($"{actor.DebugName} is numbered {actor.Ordinal} and the margin reads " +
                            $"'{line.Text}' - the ordinal never reached the string");
            }

            GD.Print($"order   {margin}");
        }

        // the script passes on only what it was actually given, so every default is stated here and
        // the two cannot drift - F5's lesson from check-fairness.ps1
        void ReadCommandLine(string[] args)
        {
            const string fights = "--fights=";

            foreach (string arg in args)
            {
                if (arg.StartsWith(fights, System.StringComparison.Ordinal) &&
                    int.TryParse(arg.Substring(fights.Length), out int n) && n > 0)
                    Fights = n;

                if (arg == "--plain") Plain = true;
            }

            // P7: WHICH CAMPAIGN'S FIGHT, out of the command line. The second-campaign test needs
            // to play a campaign this file has never heard of, and the check naming one in its own
            // source is the boundary violation that test exists to find (Game.Campaigns.Requested)
            if (Game.Campaigns.Requested.Campaign.Length > 0)
            {
                CampaignId = Game.Campaigns.Requested.Campaign;

                // the campaign changed, so the things named inside the old one have not survived
                // it - whatever it ships is what this fight is, and the checks below read the
                // encounter rather than expecting an axe
                CampaignFoe = "";
                CampaignWeapon = "";
                CampaignArmour = "";
            }

            if (Game.Campaigns.Requested.Encounter.Length > 0)
                CampaignEncounter = Game.Campaigns.Requested.Encounter;

            if (Plain) CastingHero = "";
        }

        T Find<T>() where T : Node
        {
            foreach (Node node in Descendants(this))
                if (node is T found) return found;

            return null;
        }

        static IEnumerable<Node> Descendants(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                yield return child;

                foreach (Node deeper in Descendants(child)) yield return deeper;
            }
        }

        // ---- the loop: play the hero, and let the table play everybody else ----

        public override void _Process(double delta)
        {
            if (_finished || _fight == null) return;

            _waited += delta;

            if (_waited > StuckSeconds)
            {
                Problem($"fight {_fought + 1} stopped moving for {StuckSeconds}s at " +
                        $"{_fight.Encounter} - the felt never settled, or nobody's turn came round");
                Conclude();
                return;
            }

            // A SWING IS OVER WHEN THE FIGHT SAYS SO, not when a throw comes back. An exploding
            // Impact die puts a second, third and fourth throw on the felt for one swing, and the
            // check has no business guessing which of them was the last - `Fight.Settled` means
            // nothing is in the air, nothing is walking and the blow has landed
            if (_inFlight)
            {
                // C4: the felt is being read, and the player has a moment to spend a Nerve on it.
                // Both spends that live in that moment are taken here, once each
                if (_fight.Reading) { ReadTheFelt(); return; }

                if (!_fight.Settled) return;

                Read();
                return;
            }

            Encounter fight = _fight.Encounter;

            if (fight == null) return;

            if (fight.IsOver) { Finished(fight); return; }

            // P6: THE SAVE, taken out of a fight that is actually happening. Before the hero's
            // turn is driven, so the table is at rest and the save describes a moment rather than
            // a moment and a half
            if (_campaignRoom && SaveAndReload && !_saved && fight.Round >= SaveInRound &&
                !_tray.IsAnswering && _fight.Settled)
            {
                _saved = true;
                SaveAndReloadIt();
            }

            // the order is thrown for on the real tray, so it does not exist until the felt has
            // stopped moving - which is some frames after the room was set up
            if (!_orderChecked && fight.Round > 0)
            {
                _orderChecked = true;
                CheckTheOrder();
            }

            // the tray is answering somebody, or a piece is still walking, or a beat is being held
            if (_tray.IsAnswering || !_fight.Settled) return;

            // not the hero's turn: the fight drives the foes itself, and that is the thing being
            // watched rather than the thing being driven
            if (!fight.AwaitingHero) return;

            // C4: OUT OF ACTIONS AND STILL HOLDING THE TURN, which is what a Nerve is for. The
            // hero's turn does not end itself while he has one (Encounter.Done), so this is both
            // the push and the proof that the turn waited for it
            if (fight.ActionsLeft <= 0)
            {
                if (!Plain && !_spentOnPush && _hero.Nerve > 0)
                {
                    int nerve = _hero.Nerve;

                    _fight.NerveClicked();
                    _spentOnPush = true;

                    if (fight.ActionsLeft != 1)
                        Problem($"pushing bought {fight.ActionsLeft} actions rather than one");

                    if (_hero.Nerve != nerve - 1)
                        Problem($"pushing took {nerve - _hero.Nerve} nerve rather than one");

                    _waited = 0.0;
                    return;
                }

                _fight.Done();
                _waited = 0.0;
                return;
            }

            Swing(fight);
        }

        // ---- what the rules narrate (ICombatObserver) ----

        // EVERY FOE STILL STANDING GETS A TURN, EVERY ROUND. That is the half of the turn loop the
        // rules cannot enforce for the driver: `Encounter` hands out turns, and a fight that
        // silently skipped one - because a piece could not find a route, or a beat swallowed it -
        // would look exactly like a fight where that foe chose not to move. Four Rabble standing
        // still is four Rabble standing still either way.
        //
        // Settled at the ROUND BOUNDARY against who is still standing, so a foe cut down before
        // its turn came round is not owed one
        public void RoundBegan(int round)
        {
            if (_watchingRound > 0 && _fight?.Encounter != null)
                foreach (Actor owed in _fight.Encounter.Order.Where(a => !a.IsDown && !_acted.Contains(a)))
                    Problem($"round {_watchingRound} ended and {owed.DebugName} never had a turn");

            _watchingRound = round;
            _acted.Clear();
        }

        public void TurnBegan(Actor actor, int round)
        {
            if (actor == null) return;

            if (!_acted.Add(actor))
                Problem($"{actor.DebugName} was given a second turn in round {round}");

            // C6: TWO ACTIONS AND EVERYBODY ELSE HAS ONE. The fields were set and never read
            // anywhere in the repo until Phase C, so a Dread LOOKED like it acted twice and did
            // not (SEAMS.md section 3)
            if (actor.Tier == Tier.Dread)
            {
                _bossActions = _fight.Encounter?.ActionsLeft ?? 0;

                if (_bossActions != 2)
                    Problem($"{actor.DebugName} began its turn with {_bossActions} actions and a " +
                            "Dread has two");
            }

            _turnsWatched++;
        }

        // a blow is the rules speaking, so this is where the two halves of one are held together:
        // a hit takes the damage it reported, and a miss takes nothing
        public void AttackResolved(AttackOutcome outcome)
        {
            _blowsWatched++;

            // a blow struck by somebody whose turn it is not is a reaction, and the only one the
            // game has is the readied strike
            Encounter fight = _fight?.Encounter;

            if (fight != null && !fight.IsOver && fight.Acting != null &&
                !ReferenceEquals(outcome.Attacker, fight.Acting))
            {
                _reactions++;

                if (!ReferenceEquals(outcome.Attacker, _hero))
                    Problem($"{outcome.Attacker.DebugName} struck out of turn and is not the hero - " +
                            "nothing but the hero has a reaction");
            }

            if (!outcome.Hit && outcome.Damage != 0)
                Problem($"{outcome.Attacker.DebugName} missed {outcome.Target.DebugName} for " +
                        $"{outcome.Damage} damage");

            if (outcome.Hit && outcome.Damage <= 0)
                Problem($"{outcome.Attacker.DebugName} hit {outcome.Target.DebugName} for nothing");
        }

        public void ConditionApplied(Actor actor, Condition condition)
        {
        }

        // the piece has to leave the board the moment the rules take the actor out of the fight.
        // The table's own observer runs before this one, so by the time this is called it has
        // already had its chance to topple the model and free the square
        public void ActorDowned(Actor actor)
        {
            Mini piece = _fight.PieceFor(actor);

            if (piece == null)
            {
                Problem($"{actor.DebugName} went down and the fight has no piece for it");
                return;
            }

            if (_board.CellOf(piece) != null)
                Problem($"{actor.DebugName} went down and is still standing on {_board.CellOf(piece)} - " +
                        "the square never came free");

            if (!piece.IsToppled)
                Problem($"{actor.DebugName} went down and its piece is still on its feet");
        }

        // heroic effort, spent and banked. Counted rather than judged: what a run should show is
        // that all four spends were reachable and that nothing changed by itself
        public void NerveChanged(Actor actor, int was, int now)
        {
            if (now < was) _nerveSpent += was - now;
            else _nerveBanked += now - was;

            if (now < 0 || now > actor.NerveCap)
                Problem($"{actor.DebugName} has {now} nerve and a cap of {actor.NerveCap}");
        }

        // C6: EXACTLY AT THE THRESHOLD, EXACTLY ONCE, AND THE DICE ARE ACTUALLY BIGGER. All three
        // are things that look completely fine in motion if they are wrong - a phase that fired a
        // round late, or twice, or that re-rated nothing, is a boss behaving oddly rather than a
        // bug anybody would report
        public void PhaseChanged(Actor actor)
        {
            _phases++;

            // ONCE PER BOSS, NOT ONCE PER RUN - a P7 finding. This counted phase changes across
            // the whole run and failed at two, which was right for as long as the only Dread in a
            // run was the one the check placed itself. A campaign that ships a boss makes that
            // false without anything being wrong: `greyhollow`'s Mother Stalk turns in fight one
            // and this check's own Dread turns in fight three, and both are correct
            if (!_turned.Add(actor))
                Problem($"{actor.DebugName} changed phase more than once, and a phase change " +
                        "happens once");

            if (actor.Tier != Tier.Dread)
                Problem($"{actor.DebugName} changed phase and is a {actor.Tier}");

            if (actor.Vigor * 2 > actor.MaxVigor)
                Problem($"{actor.DebugName} changed phase at {actor.Vigor} of {actor.MaxVigor}, " +
                        "which is above half");

            if (_fight.Boss == null || !_fight.Boss.Turned)
                Problem($"{actor.DebugName} changed phase and the fight's boss says it has not");

            GD.Print($"        {actor.DebugName} CHANGES at {actor.Vigor}/{actor.MaxVigor}");
        }

        public void EncounterEnded(EncounterResult result)
        {
        }

        // the hero swings at whatever is beside him, or walks at the nearest thing that is not.
        // Deliberately a poor player - the point is that the loop holds together, not that it can
        // be won cleverly
        void Swing(Encounter fight)
        {
            Actor prey = Nearest(fight);

            if (prey == null)
            {
                Problem($"the hero has {fight.ActionsLeft} actions and nothing to spend them on, with " +
                        $"{fight.Foes.Count(f => !f.IsDown)} foes still standing");
                Conclude();
                return;
            }

            Cell? at = _board.CellOf(_fight.PieceFor(prey));

            if (at == null)
            {
                Problem($"{prey.DebugName} is standing and has no square");
                Conclude();
                return;
            }

            // C3: GIVE UP AN ACTION TO WATCH, once a fight, on a turn where something is still
            // walking toward us. Clicking the hero's own square is the gesture, so this goes
            // through Board.Claims like everything else
            // ROUND ONE, because that is the only round in which anything is still walking. By
            // round two the Rabble are already in reach and strike rather than approach, so a
            // readied strike armed then would sit there all fight with nothing to trigger it -
            // which is a finding about the encounter rather than about the reaction, and worth
            // knowing either way
            if (!Plain && _readied == 0 && fight.Round == 1)
            {
                Cell? here = _board.CellOf(_fight.PieceFor(_hero));

                if (here != null && _board.Claims(here.Value))
                {
                    _readied++;
                    _waited = 0.0;

                    if (!fight.IsReadied(_hero))
                        Problem("the hero clicked his own square to watch and is not readied");

                    if (fight.AwaitingHero)
                        Problem("the hero readied and it is still his turn");

                    return;
                }
            }

            // C4: THE FOUR SPENDS, THROUGH THE GESTURES THE GAME HAS. A Nerve token is clicked
            // by ray, so the check calls what a click on one calls - and the ordering inside
            // Fight.OnNerve is what decides which of the four it buys, exactly as it would for a
            // player. Armed here, before the swing, because that is when the fourth die is bought
            if (!Plain && !_spentOnHeart && _hero.Nerve > 0 && fight.ActionsLeft > 0)
            {
                int nerve = _hero.Nerve;

                _fight.NerveClicked();
                _spentOnHeart = true;
                _wantsAFourthDie = true;

                if (_hero.Nerve != nerve)
                    Problem("arming the Heart die spent a Nerve before the dice left the hand - " +
                            "a player who changes their mind would have paid for nothing");

                return;
            }

            _target = prey;
            _targetVigorBefore = prey.Vigor;
            _heartBefore = _hero.Attribute(Attr.Heart);
            _wasChannelling = _casting && at != null &&
                              !Adjacent(at.Value) && TheEffect.Offered(_hero);
            _opening = null;
            _explosions = 0;
            // NOT ON THE BOSS FIGHT. Winding the hero on purpose costs him a die and seven
            // vigor, and the phase change only fires if he can get a Dread to half - handicapping
            // the one fight whose whole point is the second half would be checking that he cannot
            _conditionSwing = !Plain && !_fighting && !_conditionChecked && Adjacent(at.Value);

            if (_conditionSwing) Wind();

            int actionsBefore = fight.ActionsLeft;

            // THE SAME ENTRY POINT A MOUSE CLICK USES. Board.Claims is what a click on a square is
            // offered to, so this drives the fight through the seam rather than round it
            if (_board.Claims == null || !_board.Claims(at.Value))
            {
                Problem($"a click on {at}, where {prey.DebugName} is standing, was not taken by the fight");
                Conclude();
                return;
            }

            _waited = 0.0;

            // a click that started a throw is a swing, to be read when the felt stops; one that
            // started a walk is finished already
            if (_tray.IsAnswering)
            {
                _inFlight = true;
                _swings++;
                return;
            }

            // NEITHER A THROW NOR A MOVE, which used to be reported as a bug and is a P7 finding.
            //
            // A click that costs nothing is `March` refusing: there is no way to that square this
            // turn, because the room is a corridor and the only way through it has somebody
            // standing in it. That is the rules working - "a refused move is a move that did not
            // happen" - and it never came up while every room was a yard with space in it.
            // `greyhollow`'s stair is 5 wide and 9 deep and it came up in the first fight.
            //
            // So the hero does what a player does when they cannot get there: stops, and watches.
            // Which is a real gesture with a real meaning (C3), costs the turn, and lets the round
            // move on - and the stuck timer is still underneath if the turn never ends at all
            if (fight.ActionsLeft == actionsBefore && !fight.IsOver && fight.AwaitingHero)
            {
                _blocked++;
                GD.Print($"        no way to {at} this turn - the hero stops and watches");
                _fight.Done();

                if (_blocked <= BlockedTurnsWorthReporting) return;

                Problem($"the hero has been unable to reach anything {_blocked} times - the room " +
                        "may have no way through it at all");
                Conclude();
            }
        }

        bool _wantsAFourthDie;

        bool _wasChannelling;

        // C5: THE POOL IS Heart + Channeling + focus AND THE HEART DIE WENT DOWN. Both halves,
        // because either one alone is a mechanic with no cost or a cost with no mechanic
        void CheckTheCast(TrayThrow thrown)
        {
            _wasChannelling = false;
            _casts++;
            _castsSeen++;

            string[] want = { Attr.Heart.Key(), Skill.Channeling.Key(), _hero.WeaponKey };
            string[] got = thrown.Slots.Select(t => t.LabelKey).ToArray();

            if (!want.SequenceEqual(got))
                Problem($"a cast threw [{string.Join(", ", got)}] and Channeling is " +
                        $"[{string.Join(", ", want)}]");

            if (thrown.ImpactIsFallback)
                Problem("a cast left no die over - the effect has no power, so the pool was too small");

            Die now = _hero.Attribute(Attr.Heart);

            if (now != _heartBefore.StepDown())
                Problem($"a cast took the Heart die from {_heartBefore.Label()} to {now.Label()} - " +
                        $"strain steps it down one (CORE_RULES section 10)");

            if (_hero.Strain != _casts)
                Problem($"{_casts} casts and {_hero.Strain} strain on the caster");

            GD.Print($"        cast {_casts}: Heart {_heartBefore.Label()} -> {now.Label()}, " +
                     $"power is the leftover {thrown.Result.Impact.Label()} showing {thrown.ImpactValue}");

            // and a breather puts it back, once the die has been watched shrinking a couple of times
            if (!_breathed && _casts >= 2 && now == Die.D4)
            {
                _breathed = true;

                Die floored = now;
                _fight.Breathe();

                if (_hero.Strain != 0)
                    Problem($"a breather left {_hero.Strain} strain on the caster");

                if (_hero.Attribute(Attr.Heart) == floored)
                    Problem($"a breather shed the strain and the Heart die is still {floored.Label()}");

                GD.Print($"        a breather: Heart {floored.Label()} back to " +
                         $"{_hero.Attribute(Attr.Heart).Label()}");

                _casts = 0;
            }
        }

        // one re-throw and one shrug, through the same gestures a player has: a click on a die,
        // and a click on a Nerve token
        void ReadTheFelt()
        {
            if (!Plain && _sawTrouble && !_shrugged && _hero.Nerve > 0)
            {
                int nerve = _hero.Nerve;
                Die axe = _hero.Weapon;

                _fight.NerveClicked();
                _shrugged = true;

                if (_hero.Nerve != nerve - 1)
                    Problem($"shrugging a Trouble took {nerve - _hero.Nerve} nerve rather than one");

                if (_hero.Weapon != axe)
                    Problem("a Trouble was shrugged off and the gear was notched anyway");

                _waited = 0.0;
                return;
            }

            if (!Plain && !_spentOnReroll && _hero.Nerve > 0 && _opening != null && _opening.Slots.Count > 1)
            {
                int nerve = _hero.Nerve;
                int[] before = _opening.Slots.Select(t => t.Value).ToArray();

                _rerollingSlot = 0;
                _rerolledFrom = before;
                _spentOnReroll = true;

                _fight.DiePicked(0);

                if (_hero.Nerve != nerve - 1)
                    Problem($"a re-throw took {nerve - _hero.Nerve} nerve rather than one");

                // the answer comes back through OnThrown as a fresh opening throw
                _opening = null;
                _waited = 0.0;
                return;
            }

            // nothing left to spend on this throw - take it as it lies, the way a click on the
            // board does
            _fight.TakeTheThrow();
            _waited = 0.0;
        }

        int _rerollingSlot = -1;

        int[] _rerolledFrom;

        // EXACTLY ONE DIE MOVED. A re-throw that kicked the whole handful would look completely
        // fine - three new numbers is what a throw looks like - and would be a different rule
        void CheckTheReroll(TrayThrow thrown)
        {
            if (_rerolledFrom == null || _rerollingSlot < 0) return;

            if (thrown.Slots.Count != _rerolledFrom.Length)
            {
                Problem($"a re-throw changed the pool from {_rerolledFrom.Length} dice to " +
                        $"{thrown.Slots.Count}");
            }
            else
            {
                for (int i = 0; i < thrown.Slots.Count; i++)
                    if (i != _rerollingSlot && thrown.Slots[i].Value != _rerolledFrom[i])
                        Problem($"a re-throw of die {_rerollingSlot} also moved die {i}, " +
                                $"{_rerolledFrom[i]} to {thrown.Slots[i].Value}");
            }

            GD.Print($"        re-throw: die {_rerollingSlot} went from {_rerolledFrom[_rerollingSlot]} " +
                     $"to {thrown.Slots[_rerollingSlot].Value}, the rest lay still");

            _rerolledFrom = null;
            _rerollingSlot = -1;
        }

        bool Adjacent(Cell cell)
        {
            Cell? hero = _board.CellOf(_fight.PieceFor(_hero));

            return hero != null
                && Mathf.Abs(hero.Value.X - cell.X) <= 1
                && Mathf.Abs(hero.Value.Y - cell.Y) <= 1;
        }

        // whatever is standing and closest, Rabble first when two are equally close - which is what
        // the engine's own targeting would do, and what a player does.
        //
        // A CASTER GOES FOR THE FAR ONE INSTEAD, because that is the click that channels: in reach
        // is a swing, out of reach and in sight is a bolt (Fight.Engage). Picking the nearest would
        // have this check play a wizard as a man with a stick
        Actor Nearest(Encounter fight)
        {
            Cell? hero = _board.CellOf(_fight.PieceFor(_hero));

            if (hero == null) return null;

            var standing = fight.Foes
                .Where(f => !f.IsDown && _board.CellOf(_fight.PieceFor(f)) != null)
                .ToList();

            if (_casting)
            {
                Actor far = standing
                    .Where(f => Distance(hero.Value, _board.CellOf(_fight.PieceFor(f)).Value) > 1)
                    .Where(f => Sight.Clear(_board.Map, hero.Value, _board.CellOf(_fight.PieceFor(f)).Value))
                    .OrderByDescending(f => Distance(hero.Value, _board.CellOf(_fight.PieceFor(f)).Value))
                    .FirstOrDefault();

                if (far != null) return far;
            }

            return standing
                .OrderBy(f => Distance(hero.Value, _board.CellOf(_fight.PieceFor(f)).Value))
                .ThenBy(f => f.Tier == Tier.Rabble ? 0 : 1)
                .FirstOrDefault();
        }

        static int Distance(Cell a, Cell b) =>
            Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));

        // ---- C1: over a threshold, and the smaller die has to reach the felt ----

        // OVER A VIGOR THRESHOLD ON PURPOSE, unless a foe got there first - which happens, and
        // used to make this report a failure that was its own arithmetic: it compared the felt
        // against the CURRENT Might die stepped down again, having already been stepped down
        //
        // What it wants to know is the base die and the current one. The Condition is what puts a
        // gap between them, and the felt has to show the current one
        void Wind()
        {
            _mightBefore = _hero.BaseAttribute(Attr.Might);

            if (_hero.HasCondition(Condition.Winded)) return;

            _hero.Damage(_hero.MaxVigor / 3 + 1);

            if (!_hero.HasCondition(Condition.Winded))
                Problem($"{_hero.MaxVigor / 3 + 1} damage did not put the hero over a vigor threshold - " +
                        $"he is on {_hero.Vigor} of {_hero.MaxVigor} and carrying nothing");
        }

        // C1: THE DIE THE PIPELINE SAYS IS THE DIE ON THE FELT, and a Condition is what makes it
        // smaller than the base. Two assertions and neither of them does its own arithmetic - the
        // trait pipeline is the one place that composes a die (F3) and this compares against it
        void CheckTheShrunkenDie(TrayThrow thrown)
        {
            string might = Attr.Might.Key();
            Die want = _hero.Attribute(Attr.Might);

            TraySlot slot = thrown.Slots.FirstOrDefault(t => t.LabelKey == might);

            if (slot.LabelKey != might)
            {
                Problem($"the hero threw {thrown.Slots.Count} dice and none of them was his {might} - " +
                        "a swing is Might + Blades + gear");
                return;
            }

            if (slot.Die != want)
                Problem($"the hero's Might is {want.Label()} and a {slot.Die.Label()} reached the " +
                        "felt - the pool was not built from the pipeline's die");
            else if (want == _mightBefore)
                Problem($"the hero is Winded and his Might is still his base {want.Label()} - " +
                        "the Condition stepped nothing down");
            else
                GD.Print($"        Winded: the hero threw {want.Label()} for Might, base is " +
                         $"{_mightBefore.Label()}");

            _conditionChecked = true;
        }

        // the mark on the mat says what the localizer says, in words, and never the key. this is
        // what would catch a hardcoded English string or a condition with no line in game.csv -
        // both of which look completely fine until somebody switches language
        void CheckMarks()
        {
            foreach (Piece piece in _fight.Pieces)
            {
                if (piece.Marks == null)
                {
                    Problem($"{piece.Actor.DebugName} has no condition marks beside it");
                    continue;
                }

                foreach (Condition c in piece.Actor.Conditions)
                {
                    Label3D mark = piece.Marks.LabelFor(c);

                    if (mark == null)
                    {
                        Problem($"{piece.Actor.DebugName} is {c} and nothing is written beside the piece");
                        continue;
                    }

                    string key = c.Key();
                    string words = _text.Get(key);

                    if (words == key)
                        Problem($"'{key}' has no words in the locale - the mat would read the key itself");

                    if (mark.Text != words)
                        Problem($"the mark beside {piece.Actor.DebugName} reads '{mark.Text}' and the " +
                                $"localizer says '{words}' for '{key}'");
                }
            }
        }

        // ---- reading the felt ----

        // every throw this swing puts on the felt: the opening handful, then the Impact die going
        // back in the hand however many times it comes up its maximum. Nothing is decided here -
        // the arithmetic waits until the fight has finished with the swing (Read below)
        void OnThrown(TrayThrow thrown)
        {
            if (!_inFlight) return;

            _waited = 0.0;

            if (_opening == null)
            {
                _opening = thrown;

                _widestPool = Mathf.Max(_widestPool, thrown.Slots.Count);
                _sawTrouble = thrown.Result.Trouble;

                CheckTheReroll(thrown);

                if (_wasChannelling) CheckTheCast(thrown);

                if (_wantsAFourthDie)
                {
                    _wantsAFourthDie = false;

                    // AGAINST THE HERO'S OWN POOL, not against a number. A Barbarian swings
                    // Might + Blades + axe and a Nerve makes that four; a Mage has no Blades, so
                    // his swing is two dice and a Nerve makes it three. Both are "one more die
                    // than he would have thrown", and that is the rule (CORE_RULES.md section 7)
                    int plain = _hero.BuildPool(Attr.Might, Skill.Blades).Count;

                    if (thrown.Slots.Count != plain + 1)
                        Problem($"a Nerve bought the Heart die and {thrown.Slots.Count} dice reached " +
                                $"the felt - {_hero.DebugName} throws {plain} without it");
                    else if (thrown.Slots.All(t => t.LabelKey != Attr.Heart.Key()))
                        Problem("a Nerve bought the Heart die and no die on the felt is the hero's Heart");
                }

                return;
            }

            _explosions++;
            _explosionsSeen++;

            GD.Print($"        explosion {_explosions}: the die came back {thrown.Slots[0].Value}");
        }

        // the swing has landed, whatever it took to get there
        void Read()
        {
            _inFlight = false;

            if (_opening == null)
            {
                Problem($"swing {_swings} finished without a throw ever reaching the felt");
                return;
            }

            bool beat = _opening.Result.Beats(_target.Defense);
            int fell = _targetVigorBefore - _target.Vigor;

            if (beat) _hits++;

            if (_conditionSwing) CheckTheShrunkenDie(_opening);

            CheckTheBlow(_opening, beat, fell);
            CheckMarks();
        }

        void CheckTheBlow(TrayThrow thrown, bool beat, int fell)
        {
            if (!_target.Tier.HasHealthTrack())
            {
                // C2: RABBLE DIE TO ANY HIT AND TO NOTHING ELSE. Both halves - a miss has to leave
                // one standing, which is what Actor.Damage(0) used to fail at (SEAMS.md section 5)
                if (beat && !_target.IsDown)
                    Problem($"swing {_swings}: {_target.DebugName} was hit and is still standing - " +
                            "a Rabble has no health track");

                if (!beat && _target.IsDown)
                    Problem($"swing {_swings}: {_target.DebugName} was missed and went down anyway");

                if (beat) _rabbleFelled++;

                return;
            }

            // C0: the marked Impact die's value is the damage dealt, capped at what was left - Vigor
            // clamps at 0 in its one write (F3), so a killing blow of 3 into 1 takes 1 and the die on
            // the felt still reads 3
            int expected = Mathf.Min(thrown.ImpactValue, _targetVigorBefore);

            if (beat && _explosions == 0 && fell != expected)
                Problem($"swing {_swings}: the felt's Impact die shows {thrown.ImpactValue} and " +
                        $"{_target.DebugName} lost {fell} of the {_targetVigorBefore} it had - " +
                        "the damage is not the die on the table");

            // and when it exploded, the felt's last throw is only the tail of the chain, so the
            // blow has to be at least the whole first die and more than the tail alone
            if (beat && _explosions > 0 && fell < expected)
                Problem($"swing {_swings}: the Impact die exploded {_explosions} times and " +
                        $"{_target.DebugName} lost {fell}, less than the last throw of the chain");

            if (!beat && fell != 0)
                Problem($"swing {_swings}: {thrown.Result.Total} did not beat defense {_target.Defense} " +
                        $"and {_target.DebugName} lost {fell} vigor anyway");
        }

        // ---- one fight over, and then the verdict ----

        void Finished(Encounter fight)
        {
            // a boss that fell past half Vigor without turning is the one way a phase change can be
            // wrong that nothing else here would notice
            if (_fight.Boss != null && _fight.Boss.Due)
                Problem($"{_fight.Boss.Actor.DebugName} finished the fight at " +
                        $"{_fight.Boss.Actor.Vigor}/{_fight.Boss.Actor.MaxVigor} and never changed");

            CheckTheLoot();

            if (_fighting && _fight.Boss != null && !_fight.Boss.Turned)
                GD.Print("        (the boss never reached half Vigor, so the phase change went " +
                         "untested this run)");

            _fought++;
            _rounds += fight.Result.Rounds;

            if (fight.Result.HeroWon) _won++;

            GD.Print($"        {fight.Result}");

            _tray.Resolved -= OnThrown;
            _fight.Unwatch(this);

            if (_fought >= Fights) { Conclude(); return; }

            Restart();
        }

        void Conclude()
        {
            if (_finished) return;

            _finished = true;

            if (_tray != null) _tray.Resolved -= OnThrown;
            _fight?.Unwatch(this);

            GD.Print("");
            GD.Print($"fights  {_fought} played, {_won} won" +
                     (_fought > 0 ? $", {(double)_rounds / _fought:0.0} rounds on average" : ""));
            GD.Print($"swings  {_swings} thrown on the felt, {_hits} beat the defense, " +
                     $"{_rabbleFelled} Rabble removed");
            GD.Print($"turns   {_turnsWatched} taken and {_blowsWatched} blows narrated, " +
                     "every turn to somebody who was owed one");
            GD.Print($"impact  {_explosionsSeen} exploding {(_explosionsSeen == 1 ? "die went" : "dice went")} " +
                     "back in the hand");
            GD.Print($"react   {_readied} readied, {_reactions} struck out of turn");
            GD.Print($"nerve   {_nerveSpent} spent, {_nerveBanked} banked; widest pool " +
                     $"{_widestPool} dice");

            GD.Print($"loot    {(_looted.Length > 0 ? _looted : "nothing dropped this run")}");
            GD.Print($"content {(_campaignFoesFought > 0 ? $"fought {_campaignFoesFought} monster(s) read off a campaign folder" : "no campaign monster was fought this run")}");
            if (_blocked > 0)
                GD.Print($"blocked {_blocked} turn(s) ended with no way to reach anything - the " +
                         "hero stopped and watched instead");
            GD.Print($"save    {(_saves > 0 ? $"took {_saves} mid-fight, wrote {_saves} to disk, and reopened each on a table that had never seen the fight" : "no save was taken this run")}");
            GD.Print($"room    {(_campaignRoomsFought > 0 ? $"played {_campaignRoomsFought} encounter(s) defined entirely by a campaign's own files - its map, its spawn slots, its monsters and its loot" : "every fight was in the room that ships")}");
            GD.Print("boss    " + (
                _bossFights == 0 && _phases == 0 ? "none in this run"
                : _phases == 1 ? $"fought {_bossFights}, changed phase once at half Vigor"
                : _phases == 0 ? $"fought {_bossFights} and never got one to half - the phase change went untested"
                : $"fought {_bossFights} and saw {_phases} phase changes"));
            GD.Print($"channel {(_breathed ? "cast, strained to the floor and rested" : _castsSeen + " cast")}" +
                     (_castsSeen == 0 ? " - NOTHING WAS CHANNELLED" : ""));

            // only when there WAS a casting fight - a short run is all boss, and a plain run has
            // no milestones in it at all
            if (_castingFights > 0 && _castsSeen == 0)
                Problem("the casting fight never channelled anything - the gesture was never reached");
            GD.Print($"        heart {(_spentOnHeart ? "yes" : "NO")}, " +
                     $"reroll {(_spentOnReroll ? "yes" : "NO")}, " +
                     $"push {(_spentOnPush ? "yes" : "NO")}, " +
                     $"shrug {(_shrugged ? "yes" : "no trouble came up")}");

            if (!Plain && (!_spentOnHeart || !_spentOnReroll || !_spentOnPush))
                Problem("not every Nerve spend was reachable in this run - " +
                        $"heart {_spentOnHeart}, reroll {_spentOnReroll}, push {_spentOnPush}");

            // the count itself is reported and not asserted: how wide a Nerve makes a pool depends
            // on whose pool it is - a Barbarian's three becomes four and a Mage's two becomes
            // three. The precise assertion is made on the throw, against that hero's own pool

            if (_readied > 0 && _reactions == 0)
                GD.Print("        (nothing walked into reach while he was watching - not a fault, " +
                         "but the readied strike went untested this run)");
            GD.Print("        SIMULATION.md section 5 predicts ~85% survival at ~6.6 rounds against " +
                     "4 Rabble and a Rival - with no geometry in it, so the table should sit near " +
                     "that and not on it");

            if (!Plain)
                GD.Print("        AND THIS RUN IS HANDICAPPED ON PURPOSE - the hero is Winded to " +
                         "order, gives up a turn to watch, spends Nerve on whatever is reachable " +
                         "and plays the last fight as a caster. Use --plain for a win rate that " +
                         "means anything");

            Finish();
        }
    }
}
