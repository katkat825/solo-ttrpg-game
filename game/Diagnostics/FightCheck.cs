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

// aliased: each namespace holds a type of the same name
using BoardNode = Game.Board.Board;
using FightNode = Game.Fight.Fight;

namespace Game.Diagnostics
{
    // plays the real table.tscn through Board.Claims, so the thing measured is the thing played
    // watches through the observer seam, not by polling: a poll raced _Process and missed whole turns
    public partial class FightCheck : HeadlessCheck, ICombatObserver
    {
        protected override string Subject => "fight";

        [Export] public PackedScene Table { get; set; }

        [Export] public int Fights { get; set; } = 3;

        // the caster fight needs a Mage: the Barbarian is untrained in Channeling; empty plays the board's own hero
        [Export] public string CastingHero { get; set; } = EngineIds.Mage;

        // one Dread alone: the phase change fires at half Vigor and a duel is the only way a check reaches it; off-board means no boss
        [Export] public Vector2I BossAt { get; set; } = new Vector2I(5, 2);

        // one fight uses a foe from a campaign folder by id; skipped when no campaign on disk has it
        [Export] public string CampaignFoe { get; set; } = "ashfall.ghoul";

        [Export] public string CampaignWeapon { get; set; } = "cold_iron_axe";

        [Export] public string CampaignArmour { get; set; } = "scale_coat";

        // the campaign fight uses the campaign's own differently-shaped room; both empty plays the shipped room
        [Export] public string CampaignId { get; set; } = "ashfall";

        // one string names the encounter; everything else is read from the folder and checked against it
        [Export] public string CampaignEncounter { get; set; } = "ash_yard";

        // reload is a second fresh table.tscn, so anything the restore forgot to say is simply not there
        [Export] public bool SaveAndReload { get; set; } = true;

        [Export] public int SaveInRound { get; set; } = 2;

        // with Plain on, none of the milestone handicaps run, so the win rate is measurable (~85% survival)
        [Export] public bool Plain { get; set; }

        // fail-safe measured since the last thing that happened, not since the start
        [Export] public double StuckSeconds { get; set; } = 45.0;

        // the same localizer the fight uses, so marks are compared against what the system would say
        readonly ILocalizer _text = new GodotLocalizer();

        BoardNode _board;

        DiceTray _tray;

        FightNode _fight;

        Actor _hero;

        int _fought;

        int _won;

        int _rounds;

        int _swings;

        int _hits;

        int _rabbleFelled;

        int _readied;

        int _nerveSpent;

        int _nerveBanked;

        // each of the four Nerve spends ticked once: the point is that every one was reachable
        bool _spentOnHeart;

        bool _spentOnReroll;

        bool _spentOnPush;

        bool _shrugged;

        // biggest pool seen, so a fourth die is provable not hoped for
        int _widestPool;

        bool _casting;

        // a kit that loads and is never thrown proves nothing
        int _abilitiesUsed;

        bool _fighting;

        bool _campaignFight;

        int _campaignFoesFought;

        bool _campaignRoom;

        int _campaignRoomsFought;

        bool _saved;

        int _saves;

        // turns that ended with nowhere to go: a few is a corridor, many is a room nobody can cross
        int _blocked;

        public const int BlockedTurnsWorthReporting = 12;

        int _casts;

        Die _heartBefore;

        bool _breathed;

        int _castsSeen;

        int _castingFights;

        int _phases;

        // by reference: 'twice' is a question about one Dread, not the whole run
        readonly HashSet<Actor> _turned = new HashSet<Actor>(ReferenceEqualityComparer.Instance);

        int _bossFights;

        int _bossActions;

        int _reactions;

        // the order cannot be checked at muster: initiative is a real throw, not yet made when the room is set up
        bool _orderChecked;

        bool _sawTrouble;

        bool _inFlight;

        Actor _target;

        int _targetVigorBefore;

        // the swing's opening throw; explosions add more, but damage is measured against this one's Impact die
        TrayThrow _opening;

        int _explosions;

        int _explosionsSeen;

        Die _mightBefore;

        bool _conditionSwing;

        bool _conditionChecked;

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

        // a fresh table each fight: reviving the dead would be a second, private idea of how a room is set up
        void Restart()
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            // set exports before AddChild: Godot readies the subtree inside it, the last moment an export can be set
            Node table = Table.Instantiate();

            // one milestone per fight: a Mage fighting a campaign ghoul would be two milestones sharing one set of dice
            _fighting = BossAt != FightNode.Nowhere && !Plain && _fought == Fights - 1;

            _casting = !Plain && !string.IsNullOrEmpty(CastingHero) && Fights >= 2
                       && _fought == Fights - 2;

            // the campaign room comes first: its map and encounter arrive together
            _campaignRoom = !Plain && Fights >= 3 && _fought == 0 && Planned() != null;

            // named-foe checks are gated separately, so a --campaign run plays its own encounter, not ashfall's ghoul
            _campaignFight = _campaignRoom && !string.IsNullOrEmpty(CampaignFoe)
                             && Game.Campaigns.Library.Load(quiet: true).Has(CampaignFoe);

            if (_casting) _castingFights++;

            foreach (Node node in Descendants(table))
            {
                if (_casting && node is BoardNode board) board.HeroId = CastingHero;

                // two strings set before the board's _Ready: it reads the encounter, which names the map and musters the foes
                if (node is BoardNode room3)
                {
                    if (_campaignRoom)
                    {
                        room3.Campaign = CampaignId;
                        room3.Encounter = CampaignEncounter;
                    }
                    else
                    {
                        // the caster and boss fights stay in the shipped room even under --campaign; a multi-fight check must say Fixed
                        room3.Fixed = true;
                    }
                }

                if (node is not FightNode room) continue;

                if (_campaignFight)
                {
                    room.HeroWeapon = CampaignWeapon;
                    room.HeroArmour = CampaignArmour;
                }

                // clear the scene's squares, so a failed encounter read is a loud empty room, not the cellar's Rabble standing in
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

            // after AddChild: Godot readies the subtree bottom-up before the call returns
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

            // subscribe last, so the fight's own handler applies the outcome before this one looks
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

            // a campaign monster is an Actor like any other; the fight has no idea it came from a file
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

        // every expectation is read back from the plan and map, so this checks the game against the folder, not a list here
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

            // read the shipped room the same way the board does, so 'differently shaped' is measured against the real thing
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

            // the hero is slot 0, placed by the board not the fight: a second loader checked in one line
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

            // triggers printed, not asserted: another test owns that the slot parses and round-trips
            foreach (Trigger trigger in plan.Triggers)
                GD.Print($"        trigger: {trigger} - read and carried; nothing runs it until " +
                         "the campaign shell (Phase R)");

            foreach (Cue cue in plan.Cues)
                GD.Print($"        cue: {cue} - a slot for a gesture the DM has not learned yet " +
                         "(Phase D)");
        }

        // two tables asked the same question; nothing is compared against a constant, so both cannot be wrong the same way
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
                try { System.IO.File.Delete(path); } catch { }
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

                    // a grown hero is base dice plus named steps: the reopened table is told the steps and re-derives the dice, never copying them
                    if (read.Hero != null)
                    {
                        board.HeroId = read.Hero.Id;
                        board.Grown = read.Hero.Growth.ToArray();
                    }
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
            // compare the standing only: a fallen foe is not in the save, so the reload re-musters and kills it at the back of the order
            Same("the turn order", Names(live.Order.Where(a => !a.IsDown)),
                 Names(resumed.Order.Where(a => !a.IsDown)));

            SameActor("the hero", _hero, again.Hero, board, again);

            // matched by spawn slot, so this compares the two tables, not the restore against itself
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

            // compare the faces and the derived verdict both: a verdict that disagreed would mean the reading itself moved
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

        // everything a fight can do to an actor, which is the list a save has to carry
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

        // find the map through Library the way the game does, not by building a path; null when no campaign has that encounter
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

        // compare gear against the hero's own bare statblock, not a constant
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
                            "is the same size - a better weapon is a bigger die");
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

        // assert the draw goes through IRng (same seed, same answer), not whatever happened to fall
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

            // ordinals, so 'Rabble 3' comes from one key with {0}, not an integer glued onto a translated name
            foreach (Actor mook in _fight.Foes.Where(f => f.Tier == Tier.Rabble))
                if (mook.Ordinal <= 0)
                    Problem($"a Rabble was mustered unnumbered - '{mook.NameKey}' takes no ordinal");
        }

        // the margin is where a hardcoded name or missing locale line shows, and the first place an ordinal is formatted into a key
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

                // a numbered line must carry its number, or four Rabble are four identical words
                if (actor.Ordinal > 0 && !line.Text.Contains(actor.Ordinal.ToString()))
                    Problem($"{actor.DebugName} is numbered {actor.Ordinal} and the margin reads " +
                            $"'{line.Text}' - the ordinal never reached the string");
            }

            GD.Print($"order   {margin}");
        }

        // the script passes only what it was given, so every default lives here and the two cannot drift
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

            // --campaign plays a campaign the check never names in source, a boundary worth pinning
            if (Game.Campaigns.Requested.Campaign.Length > 0)
            {
                CampaignId = Game.Campaigns.Requested.Campaign;

                // a changed campaign drops the old one's named foe and gear: the checks read its encounter, not ashfall's axe
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

            // a swing is over when Fight.Settled says so, not when a throw returns: explosions add throws to the same swing
            if (_inFlight)
            {
                if (_fight.Reading) { ReadTheFelt(); return; }

                if (!_fight.Settled) return;

                Read();
                return;
            }

            Encounter fight = _fight.Encounter;

            if (fight == null) return;

            if (fight.IsOver) { Finished(fight); return; }

            // save while the table is at rest, before the hero's turn is driven, so it describes a moment not a moment and a half
            if (_campaignRoom && SaveAndReload && !_saved && fight.Round >= SaveInRound &&
                !_tray.IsAnswering && _fight.Settled)
            {
                _saved = true;
                SaveAndReloadIt();
            }

            if (!_orderChecked && fight.Round > 0)
            {
                _orderChecked = true;
                CheckTheOrder();
            }

            if (_tray.IsAnswering || !_fight.Settled) return;

            // the fight drives the foes itself: that is watched, not driven
            if (!fight.AwaitingHero) return;

            // out of actions but still holding the turn: the push, and the proof the turn waited for a Nerve
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

            // use a checked ability once per fight, only when the hero has one; built-in heroes do not
            if (!Plain && _abilitiesUsed <= _fought && _fight.HasChecked)
            {
                _abilitiesUsed++;
                _fight.UseAbilityKey();
                _waited = 0.0;
                return;
            }

            Swing(fight);
        }

        // every standing foe must get a turn each round: a skipped turn looks exactly like a foe choosing not to move
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

            // a Dread has two actions: the fields were set but never read until now, so it looked like it acted twice and did not
            if (actor.Tier == Tier.Dread)
            {
                _bossActions = _fight.Encounter?.ActionsLeft ?? 0;

                if (_bossActions != 2)
                    Problem($"{actor.DebugName} began its turn with {_bossActions} actions and a " +
                            "Dread has two");
            }

            _turnsWatched++;
        }

        // a hit takes the damage it reported, a miss takes nothing
        public void AttackResolved(AttackOutcome outcome)
        {
            _blowsWatched++;

            // a blow out of turn is a reaction, and the only one the game has is the readied strike
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

        // the table's observer runs before this one, so the piece is already toppled and its square freed by now
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

        // counted, not judged: a run should show all four spends reachable and nothing changing by itself
        public void NerveChanged(Actor actor, int was, int now)
        {
            if (now < was) _nerveSpent += was - now;
            else _nerveBanked += now - was;

            if (now < 0 || now > actor.NerveCap)
                Problem($"{actor.DebugName} has {now} nerve and a cap of {actor.NerveCap}");
        }

        // at the threshold, once, and the dice actually bigger: all three look fine in motion if wrong
        public void PhaseChanged(Actor actor)
        {
            _phases++;

            // once per boss, not per run: a campaign boss and this check's own both turn, and both are correct
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

        // deliberately a poor player: the point is that the loop holds together, not that it wins cleverly
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

            // ready a strike only in round one, the only round anything is still walking; later the Rabble are already in reach
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

            // spend Nerve through the same call a token click makes; Fight.OnNerve's ordering decides which of the four it buys
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
            // not on the boss fight: winding the hero would keep him from getting a Dread to half, the one thing that fight is for
            _conditionSwing = !Plain && !_fighting && !_conditionChecked && Adjacent(at.Value);

            if (_conditionSwing) Wind();

            int actionsBefore = fight.ActionsLeft;

            // Board.Claims is the same entry point a mouse click uses, so the fight is driven through the seam, not round it
            if (_board.Claims == null || !_board.Claims(at.Value))
            {
                Problem($"a click on {at}, where {prey.DebugName} is standing, was not taken by the fight");
                Conclude();
                return;
            }

            _waited = 0.0;

            if (_tray.IsAnswering)
            {
                _inFlight = true;
                _swings++;
                return;
            }

            // a click that costs nothing is March refusing (no route this turn); the hero stops and watches, which ends the turn
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

        // both halves: the pool is Heart + Channeling + focus and the Heart die stepped down; one without the other is half a mechanic
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
                        $"strain steps it down one");

            if (_hero.Strain != _casts)
                Problem($"{_casts} casts and {_hero.Strain} strain on the caster");

            GD.Print($"        cast {_casts}: Heart {_heartBefore.Label()} -> {now.Label()}, " +
                     $"power is the leftover {thrown.Result.Impact.Label()} showing {thrown.ImpactValue}");

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

        // one re-throw and one shrug through the player's own gestures
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

                _opening = null;
                _waited = 0.0;
                return;
            }

            _fight.TakeTheThrow();
            _waited = 0.0;
        }

        int _rerollingSlot = -1;

        int[] _rerolledFrom;

        // exactly one die moved: a re-throw that kicked the whole handful would look fine and be a different rule
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

        // closest, Rabble first; but a caster picks the far in-sight one, because that is the click that channels
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

        // wind the hero over a vigor threshold on purpose; a foe may get there first
        void Wind()
        {
            // the die as it stands, not the base: a growth step can compose a Winded die back to the base, so compare against the die before the Condition
            _mightBefore = _hero.Attribute(Attr.Might);

            if (_hero.HasCondition(Condition.Winded)) return;

            _hero.Damage(_hero.MaxVigor / 3 + 1);

            if (!_hero.HasCondition(Condition.Winded))
                Problem($"{_hero.MaxVigor / 3 + 1} damage did not put the hero over a vigor threshold - " +
                        $"he is on {_hero.Vigor} of {_hero.MaxVigor} and carrying nothing");
        }

        // compare the felt's die against the trait pipeline, the one place that composes a die; neither assertion does its own arithmetic
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
                Problem($"the hero is Winded and his Might is still the {want.Label()} he threw " +
                        "before - the Condition stepped nothing down");
            else
                GD.Print($"        Winded: the hero threw {want.Label()} for Might, base is " +
                         $"{_mightBefore.Label()}");

            _conditionChecked = true;
        }

        // the mark must say what the localizer says, never the key: catches a hardcoded string or a missing locale line
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

        // every throw of the swing lands here; the arithmetic waits until the fight finishes the swing (Read)
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

                    // against the hero's own pool: a Nerve makes it one more die than he would have thrown, Barbarian or Mage
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
                // Rabble die to any hit and nothing else: a miss must leave one standing, which Actor.Damage(0) once failed
                if (beat && !_target.IsDown)
                    Problem($"swing {_swings}: {_target.DebugName} was hit and is still standing - " +
                            "a Rabble has no health track");

                if (!beat && _target.IsDown)
                    Problem($"swing {_swings}: {_target.DebugName} was missed and went down anyway");

                if (beat) _rabbleFelled++;

                return;
            }

            // damage is the marked Impact die, capped at vigor left: a 3 into 1 takes 1 and the die still reads 3
            int expected = Mathf.Min(thrown.ImpactValue, _targetVigorBefore);

            if (beat && _explosions == 0 && fell != expected)
                Problem($"swing {_swings}: the felt's Impact die shows {thrown.ImpactValue} and " +
                        $"{_target.DebugName} lost {fell} of the {_targetVigorBefore} it had - " +
                        "the damage is not the die on the table");

            // after an explosion the felt shows only the chain's tail, so the blow must exceed it
            if (beat && _explosions > 0 && fell < expected)
                Problem($"swing {_swings}: the Impact die exploded {_explosions} times and " +
                        $"{_target.DebugName} lost {fell}, less than the last throw of the chain");

            if (!beat && fell != 0)
                Problem($"swing {_swings}: {thrown.Result.Total} did not beat defense {_target.Defense} " +
                        $"and {_target.DebugName} lost {fell} vigor anyway");
        }

        void Finished(Encounter fight)
        {
            // a boss that fell past half without turning is the one broken phase change nothing else would catch
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

            // what the hero's kit is and whether any of it was thrown
            GD.Print($"kit     {(_fight?.Kit == null || _fight.Kit.Count == 0 ? "nothing" : string.Join(", ", _fight.Kit.Select(a => a.Id)))}" +
                     (_abilitiesUsed > 0 ? $" - {_abilitiesUsed} thrown on the felt out of the hero's own" : ""));

            // only when there was a casting fight: a short run is all boss, a plain run has no milestones
            if (_castingFights > 0 && _castsSeen == 0)
                Problem("the casting fight never channelled anything - the gesture was never reached");
            GD.Print($"        heart {(_spentOnHeart ? "yes" : "NO")}, " +
                     $"reroll {(_spentOnReroll ? "yes" : "NO")}, " +
                     $"push {(_spentOnPush ? "yes" : "NO")}, " +
                     $"shrug {(_shrugged ? "yes" : "no trouble came up")}");

            if (!Plain && (!_spentOnHeart || !_spentOnReroll || !_spentOnPush))
                Problem("not every Nerve spend was reachable in this run - " +
                        $"heart {_spentOnHeart}, reroll {_spentOnReroll}, push {_spentOnPush}");

            if (_readied > 0 && _reactions == 0)
                GD.Print("        (nothing walked into reach while he was watching - not a fault, " +
                         "but the readied strike went untested this run)");
            GD.Print("        ~85% survival at ~6.6 rounds is expected against " +
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
