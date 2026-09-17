using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Content.Dialogue;
using Content.Kits;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Core.Space;
using Game.Board;
using Game.Localization;
using Game.Tray;

// Game.Board is both a namespace and the type; alias so Board binds to the node
using BoardNode = Game.Board.Board;

namespace Game.Fight
{
    public partial class Fight : Node3D
    {
        [Export] public NodePath BoardPath { get; set; } = "../Board";

        [Export] public NodePath TrayPath { get; set; } = "../DiceTray";

        [Export] public PackedScene RivalPiece { get; set; }

        [Export] public PackedScene RabblePiece { get; set; }

        [Export] public PackedScene DreadPiece { get; set; }

        // only so a pack can vary the hero's mini; unset uses the board's own
        [Export] public PackedScene HeroPiece { get; set; }

        [Export] public Vector2I RivalAt { get; set; } = new Vector2I(4, 4);

        // a square off the board: the only way to say 'none' with a Vector2I
        public static readonly Vector2I Nowhere = new Vector2I(-1, -1);

        // Godot array, not Vector2I[]: a C# struct array is not a Variant, exporter refuses it (GD0102)
        [Export] public Godot.Collections.Array<Vector2I> RabbleAt { get; set; } = new Godot.Collections.Array<Vector2I>();

        [Export] public Vector2I DreadAt { get; set; } = Nowhere;

        [Export] public string RabbleId { get; set; } = EngineIds.Rabble;

        [Export] public string RivalId { get; set; } = EngineIds.Rival;

        [Export] public string DreadId { get; set; } = EngineIds.Dread;

        // empty means whatever his statblock gave him
        [Export] public string HeroWeapon { get; set; } = "";

        [Export] public string HeroArmour { get; set; } = "";

        // one id per spawn slot, in order; empty falls back to the squares above
        [Export] public Godot.Collections.Array<string> Spawns { get; set; } =
            new Godot.Collections.Array<string>();

        [Export] public float Beat { get; set; } = 0.45f;

        // grace period to spend a Nerve before a throw resolves; opens only when there is something to decide
        [Export] public float ReadSeconds { get; set; } = 1.8f;

        // any non-zero value seeds the resolver for a repeatable fight; 0 means the clock
        [Export] public int Seed { get; set; }

        readonly Game.Campaigns.Library _archetypes = Game.Campaigns.Library.Load(quiet: true);

        MiniMaker _minis;

        // loot's own rng stream, so a seeded fight repeats its drops
        IRng _luck;

        readonly Pieces _pieces = new Pieces();

        // held as the interface so this file never reaches for TranslationServer
        readonly ILocalizer _text = new GodotLocalizer();

        readonly List<Actor> _foes = new List<Actor>();

        BoardNode _board;

        DiceTray _tray;

        CombatEngine _engine;

        CompositeCombatObserver _watching;

        Encounter _fight;

        Actor _hero;

        // non-null while the hero's dice are in the air; also the guard against a second throw
        Actor _swingingAt;

        // null for an ordinary swing, deliberately not modelled as an ability
        Ability _ability;

        // empty for built-in heroes with no class card; they fall back to the shipped abilities
        IReadOnlyList<Ability> _kit = System.Array.Empty<Ability>();

        // optional; a fight with no DM plays exactly as before
        [Export] public NodePath DmPath { get; set; }

        Game.Dm.Dm _dm;

        // and optional in exactly the same way (W1/W2). The companion is never in the fight - it is
        // told what the table did and has opinions about it. It has no turn, no square and no
        // statblock, and there is nowhere in this file that could give it one.
        [Export] public NodePath CompanionPath { get; set; }

        Game.Companion.Companion _friend;

        // the day watches the same fights the table does, so camp can be about them (W3)
        public Content.Dialogue.Day Today { get; private set; }

        // reveal a foe's Defence once: a number you already know is not a reveal
        readonly HashSet<Actor> _guardsSeen = new HashSet<Actor>();

        // closing cue plays once; _Process sees the fight over on every later frame
        bool _cleared;

        PoolResult _roll;

        ImpactChain _impact;

        double _beat;

        TurnOrder _order;

        // the round the DM last reached behind the screen in; -1 is "not yet this fight"
        int _rattled = -1;

        // dice in the air are the initiative throw, not a blow
        bool _rollingOrder;

        // this swing is a readied reaction: out of turn, advances nobody's turn
        bool _reacting;

        NerveTokens _nerve;

        PhaseChange _boss;

        // non-null exactly while the read window is open
        TrayThrow _felt;

        double _read;

        // a Nerve committed (not spent) to the next throw: undoing it costs nothing
        bool _heart;

        // a Trouble already shrugged, so closing the window does not charge for it again
        bool _shrugged;

        // the attribute the read throw was made with; decides where a Trouble lands
        Attr _using = Attr.Might;

        // a reaction lands before the mover's action is booked; without this the round could roll over and drop the readied strike
        Actor _interrupted;

        public Actor Hero => _hero;

        public IReadOnlyList<Actor> Foes => _foes;

        public IReadOnlyList<Piece> Pieces => _pieces.All;

        public Encounter Encounter => _fight;

        public TurnOrder Order => _order;

        public PhaseChange Boss => _boss;

        public Mini PieceFor(Actor actor) => _pieces.Of(actor)?.Mini;

        public bool Reading => _felt != null;

        public void NerveClicked() => OnNerve();

        public void DiePicked(int slot) => OnPicked(slot);

        public void TakeTheThrow() { if (_felt != null) Read(); }

        public void Done() { if (_fight != null && _fight.AwaitingHero) Watch(); }

        public void Breathe() => Breather();

        public void UseAbilityKey() => UseChecked();

        public IReadOnlyList<Ability> Kit => _kit;

        public bool HasChecked => FirstOf(Primitive.Check) != null;

        // observer, not polling: a turn that began and ended between two samples is invisible to a poller
        public bool Watch(ICombatObserver observer) => _watching != null && _watching.Add(observer);

        public bool Unwatch(ICombatObserver observer) => _watching != null && _watching.Remove(observer);

        public bool Settled =>
            _swingingAt == null && !_rollingOrder && _felt == null && _beat <= 0.0
            && !_pieces.All.Any(p => p.Mini.IsMoving);

        public override void _Ready()
        {
            _board = GetNodeOrNull<BoardNode>(BoardPath);
            _tray = GetNodeOrNull<DiceTray>(TrayPath);

            if (_board == null)
            {
                GD.PushError($"fight: no board at '{BoardPath}' - there is nowhere to have a fight");
                return;
            }

            if (_tray == null)
            {
                GD.PushError($"fight: no dice tray at '{TrayPath}' - a swing has nothing to throw");
                return;
            }

            _hero = _board.Hero;

            if (_hero == null)
            {
                GD.PushError("fight: the board has no hero - it has not finished loading, or its map is broken");
                return;
            }

            _kit = KitFor(_hero);

            int seed = Seed != 0 ? Seed : (int)Time.GetTicksMsec();

            IRng rng = new SeededRng(seed);

            _luck = new SeededRng(seed + 1);

            Today = new Content.Dialogue.Day(_hero);

            // table and transcript both watch, so a mismatch between them is visible; the day
            // watches too, and learns what tonight's camp is about without a second source of truth
            _watching = new CompositeCombatObserver(
                new TableObserver(_board, _pieces) { Loot = Drops },
                Today,
                new RecordingCombatObserver(GD.Print));

            _engine = new CombatEngine(new StandardResolver(rng), observer: _watching);

            // built here, not as a field: it needs the board's metrics and paint
            _minis = new MiniMaker(
                new MiniScenes(HeroPiece, RabblePiece, RivalPiece, DreadPiece),
                _archetypes.Shelf)
            {
                Painted = _board.Paint is ShaderMaterial shader ? shader.Shader : null,
                BasePaint = _board.Paint,
                CellSize = _board.Metrics.CellSize,
            };

            Muster();
            Equip();

            _fight = new Encounter(_engine, _hero, _foes);

            if (_boss != null) _fight.Phases(_boss);

            _dm = DmPath != null && !DmPath.IsEmpty ? GetNodeOrNull<Game.Dm.Dm>(DmPath) : null;

            _friend = CompanionPath != null && !CompanionPath.IsEmpty
                ? GetNodeOrNull<Game.Companion.Companion>(CompanionPath)
                : null;

            _board.Claims = OnClick;
            _tray.Resolved += OnThrown;
            _tray.Picked += OnPicked;

            _board.AddChild(_order = new TurnOrder { Name = "TurnOrder", Text = _text });

            // developer diagnostics, not player-facing text
            GD.Print("");
            GD.Print($"fight   {_hero} - beside a foe to strike it, anywhere else to move; " +
                     $"{_engine.Options.HeroActionsPerRound} actions a round, {_engine.Options.Pace} squares a move");
            GD.Print($"        {(FirstOf(Primitive.Channel) != null ? "a foe in sight and out of reach is channelled at" : "not a caster - a foe out of reach is walked at")}" +
                     $", {(char)KeyToBreathe} takes a breather" +
                     $", {(char)KeyToUseAbility} uses a checked ability");

            GD.Print($"kit     {(_kit.Count == 0 ? "nothing" : string.Join(", ", _kit.Select(a => a.Id)))}");

            foreach (Piece piece in _pieces.All)
                GD.Print($"        {piece.Actor} on {_board.CellOf(piece.Mini)}");

            if (Resuming != null) PickItBackUp();
            else RollTheOrder();
        }

        // set before AddChild: Godot readies the subtree there, the last instant before muster
        public Content.Saves.SaveGame Resuming { get; set; }

        // the saved throw is read back, not re-seated: a resumed fight knows the throw but has a clear felt
        public TrayThrow Restored { get; private set; }

        public Content.Saves.SaveGame Save() => Game.Saves.Savepoint.Of(this, _board, _tray);

        void PickItBackUp()
        {
            foreach (Content.Schema.ContentProblem problem in
                     Game.Saves.Savepoint.Apply(Resuming, this, _board))
                GD.PushWarning("save: " + problem);

            Restored = FeltOf(Resuming);

            // a save from before the fight still starts one on the real tray
            if (!Resuming.MidFight) { RollTheOrder(); return; }

            _order.Write(_fight.Order, _board.Metrics);

            GD.Print("");
            GD.Print($"resumed round {_fight.Round}, {_fight.Acting?.DebugName} to act with " +
                     $"{_fight.ActionsLeft} action(s) left");
            GD.Print("order   " + string.Join(", ", _fight.Order.Select(
                a => $"{a.DebugName} {_fight.InitiativeOf(a)}")));

            if (Restored != null)
                foreach (string line in Restored.DebugLines(0, _text)) GD.Print("felt   " + line);
        }

        static TrayThrow FeltOf(Content.Saves.SaveGame save)
        {
            if (save.Felt.Count == 0) return null;

            var slots = new List<TraySlot>();

            foreach (Content.Saves.SavedDie die in save.Felt)
                slots.Add(new TraySlot(die.Trait, die.Die, die.Value));

            return TrayThrow.Read(slots);
        }

        // hero's initiative is a real visible throw; no Grace means nothing to throw and no score
        void RollTheOrder()
        {
            Pool pool = Initiative.PoolFor(_hero);

            if (pool.Count == 0)
            {
                Started(null);
                return;
            }

            _rollingOrder = true;

            GD.Print("");
            GD.Print($"order   {_hero.DebugName} throws for it - {Initiative.Attribute} + " +
                     $"{Initiative.Skill}, {pool.Count} " + (pool.Count == 1 ? "die" : "dice"));

            _tray.Throw(pool);
        }

        void Started(int? heroInitiative)
        {
            _rollingOrder = false;

            _fight.Begin(heroInitiative);
            _order.Write(_fight.Order, _board.Metrics);

            // no campaign means no plan and the DM does nothing, which is correct not a gap
            _dm?.Perform(_board.Plan, Content.Places.When.Entered, _board.Campaign);

            Prompt(Content.Places.When.Entered);

            GD.Print("");
            GD.Print("order   " + string.Join(", ", _fight.Order.Select(
                a => $"{a.DebugName} {_fight.InitiativeOf(a)}")));
        }

        public override void _ExitTree()
        {
            if (_board != null && _board.Claims == OnClick) _board.Claims = null;

            if (_tray != null)
            {
                _tray.Resolved -= OnThrown;
                _tray.Picked -= OnPicked;
                _tray.Picking = false;
            }
        }

        void Muster()
        {
            Enlist(_hero, _board.Piece);

            if (FromTheMap()) return;

            // numbered from 1 so a key with {0} can say 'Rabble 3' without gluing on an integer
            int ordinal = 1;

            foreach (Vector2I at in RabbleAt ?? new Godot.Collections.Array<Vector2I>())
                Recruit(RabbleId, at, $"Rabble{ordinal}", ordinal++);

            if (RivalAt != Nowhere) Recruit(RivalId, RivalAt, "Rival", 0);

            if (DreadAt != Nowhere) Recruit(DreadId, DreadAt, "Dread", 0);
        }

        // false when there is nothing to place this way; the scene's squares are the fallback
        bool FromTheMap()
        {
            IReadOnlyList<string> spawns = Roster();

            if (spawns.Count == 0) return false;

            int ordinal = 1;
            int placed = 0;

            for (int slot = 1; slot <= spawns.Count; slot++)
            {
                string id = spawns[slot - 1];

                // a hole in the list is a slot the room does not use
                if (string.IsNullOrWhiteSpace(id)) continue;

                Cell? at = _board.Map.SpawnAt(slot);

                if (at == null)
                {
                    GD.PushError($"fight: the encounter puts '{id}' on spawn {slot} and " +
                                 $"{_board.Loaded} has no such slot - it has " +
                                 $"{Listed(_board.Map)}");
                    continue;
                }

                if (!_archetypes.Has(id))
                {
                    GD.PushError($"fight: there is no '{id}' in the roster " +
                                 $"({string.Join(", ", _archetypes.Ids)}) - spawn {slot} is empty");
                    continue;
                }

                bool crowd = _archetypes.Create(id).Tier == Tier.Rabble;

                Recruit(id, new Vector2I(at.Value.X, at.Value.Y),
                        $"Spawn{slot}", crowd ? ordinal++ : 0, slot);

                placed++;
            }

            GD.Print($"fight: {placed} on the spawn slots of {_board.Loaded}");

            return true;
        }

        IReadOnlyList<string> Roster()
        {
            if (_board.Plan != null) return _board.Plan.Roster(_board.Campaign);

            return Spawns ?? (IReadOnlyList<string>)System.Array.Empty<string>();
        }

        static string Listed(MapLayout map) =>
            map.Spawns.Count == 0 ? "none at all" : string.Join(", ", map.Spawns.Keys);

        void Recruit(string id, Vector2I at, string name, int ordinal, int slot = 0)
        {
            if (!_archetypes.Has(id))
            {
                GD.PushError($"fight: there is no '{id}' in the roster " +
                             $"({string.Join(", ", _archetypes.Ids)}) - '{name}' is not in this fight");
                return;
            }

            Actor actor = _archetypes.Create(id);

            Mini piece = _board.Place(
                _minis.Make(Content.Campaigns.ContentId.CampaignOf(id), _archetypes.MiniFor(id),
                            actor.Tier, name),
                new Cell(at.X, at.Y), name);

            if (piece == null)
            {
                GD.PushError($"fight: '{name}' could not be put on {at} - it is not in this fight");
                return;
            }

            if (ordinal > 0) actor.Numbered(ordinal);

            _foes.Add(actor);
            Enlist(actor, piece, slot);

            // registered here, not in Encounter: a phase change is content
            if (actor.Tier == Tier.Dread) _boss = PhaseChange.Standard(actor);
        }

        void Equip()
        {
            Take(HeroWeapon, gear => _hero.Wielding(gear), "wielding");
            Take(HeroArmour, gear => _hero.Wearing(gear), "wearing");
        }

        void Take(string id, System.Action<Gear> equip, string how)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            Gear gear = _archetypes.Items.Of(id);

            if (gear == null)
            {
                GD.PushError($"fight: there is no item '{id}' - the hero keeps what his statblock " +
                             $"gave him ({string.Join(", ", _archetypes.Items.Ids)})");
                return;
            }

            equip(gear);

            GD.Print($"        {_hero.DebugName} is {how} {gear}");
        }

        void Drops(Actor fallen)
        {
            if (fallen == null || ReferenceEquals(fallen, _hero)) return;

            string item = _archetypes.LootFor(fallen.Id).Draw(_luck);

            if (item == null) return;

            _hero.Carry(item);

            Gear gear = _archetypes.Items.Of(item);

            GD.Print($"        {fallen.DebugName} was carrying {gear?.ToString() ?? item} - " +
                     $"{_hero.DebugName} has {_hero.Satchel.Count} thing(s) in the satchel");
        }

        // a Rabble gets condition marks but no tally: no health track to count
        void Enlist(Actor actor, Mini mini, int slot = 0)
        {
            VigorPips pips = null;

            if (actor.Tier.HasHealthTrack())
            {
                mini.AddChild(pips = new VigorPips { Name = "Vigor" });
                pips.Track(actor.MaxVigor);
            }

            var marks = new ConditionMarks { Name = "Conditions", Text = _text };
            mini.AddChild(marks);

            if (actor.NerveCap > 0)
            {
                var tokens = new NerveTokens { Name = "Nerve" };
                mini.AddChild(tokens);
                tokens.Track(actor.NerveCap);

                if (ReferenceEquals(actor, _hero)) _nerve = tokens;
            }

            _pieces.Add(actor, mini, pips, marks, slot);
        }

        // true means the fight took the click; false hands it back to the board
        bool OnClick(Cell cell)
        {
            if (_fight == null) return false;

            if (_fight.IsOver) return false;

            // while the felt is read, a click takes the throw and nothing else
            if (_felt != null) { Read(); return true; }

            // busy: a click now would be a piece walking away from its own swing
            if (!Settled || _board.IsChecking) return true;

            if (!_fight.AwaitingHero) return true;

            // parked with a Nerve but no actions: without this guard a swing lands nothing and a move is a free step
            if (_fight.ActionsLeft <= 0)
            {
                Piece here = _pieces.On(_board.Squares.At(cell));

                if (here != null && ReferenceEquals(here.Actor, _hero)) { Watch(); return true; }

                GD.Print($"        {_hero.DebugName} is out of actions - spend a Nerve to push, " +
                         "or end the turn");
                return true;
            }

            Piece target = _pieces.On(_board.Squares.At(cell));

            if (target != null && !ReferenceEquals(target.Actor, _hero) && !target.IsDown)
            {
                Engage(target.Actor);
                return true;
            }

            // clicking your own piece means stop and take a readied strike
            if (target != null)
            {
                if (ReferenceEquals(target.Actor, _hero)) Watch();
                return true;
            }

            March(cell);
            return true;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_fight == null) return;

            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: KeyToBreathe })
            {
                Breather();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: KeyToUseAbility })
            {
                UseChecked();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_nerve == null) return;
            if (!@event.IsActionPressed("place_piece") || @event is not InputEventMouse mouse) return;

            if (!TouchedAToken(mouse.Position)) return;

            GetViewport().SetInputAsHandled();
            OnNerve();
        }

        bool TouchedAToken(Vector2 at)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return false;

            var query = PhysicsRayQueryParameters3D.Create(
                camera.ProjectRayOrigin(at),
                camera.ProjectRayOrigin(at) + camera.ProjectRayNormal(at) * TokenReach);

            query.CollideWithAreas = false;

            Godot.Collections.Dictionary hit = GetWorld3D()?.DirectSpaceState?.IntersectRay(query);

            return hit != null && hit.Count > 0 && _nerve.Owns(hit["collider"].As<GodotObject>());
        }

        const float TokenReach = 8f;

        void OnNerve()
        {
            // shrug a Trouble before it resolves
            if (_felt != null && _felt.Result.Trouble && !_shrugged)
            {
                if (!_fight.SpendNerve(_hero)) return;

                _shrugged = true;
                _friend?.SeesNerveSpent();
                GD.Print($"        {_hero.DebugName} shrugs the Trouble off");
                return;
            }

            if (_felt != null) return;
            if (!_fight.AwaitingHero) return;

            // changed his mind about the Heart die
            if (_heart)
            {
                _heart = false;
                GD.Print($"        {_hero.DebugName} takes the Heart die back out of the pool");
                return;
            }

            // the Heart die, for the next throw
            if (_fight.ActionsLeft > 0 && _hero.Nerve > 0 &&
                Nerve.CanAddHeart(_hero, _hero.BuildPool(_engine.Options.HeroAttackAttr,
                                                         _engine.Options.HeroAttackSkill)))
            {
                _heart = true;
                GD.Print($"        {_hero.DebugName} commits a Nerve - the Heart die goes in the next pool");
                return;
            }

            // no actions left: push through for one more
            if (_fight.Push())
            {
                GD.Print($"        {_hero.DebugName} pushes through - {_fight.ActionsLeft} action left, " +
                         $"{_hero.Nerve} nerve");
                return;
            }

            GD.Print($"        {_hero.DebugName} has nothing to spend a Nerve on right now");
        }

        const Key KeyToBreathe = Key.B;

        const Key KeyToUseAbility = Key.K;

        void UseChecked()
        {
            Ability ability = FirstOf(Primitive.Check);

            if (ability == null || _swingingAt != null || !_fight.AwaitingHero) return;

            Actor target = ability.Target == Target.Self ? _hero : WithinReach();

            if (target == null)
            {
                GD.Print($"        nothing within reach for {ability.Id}");
                return;
            }

            Use(ability, target);
        }

        Actor WithinReach()
        {
            Cell? from = _board.CellOf(_board.Piece);

            if (from == null) return null;

            foreach (Piece piece in _pieces.All)
            {
                if (piece.Actor == _hero || piece.Actor.IsDown) continue;

                Cell? to = _board.CellOf(piece.Mini);

                if (to != null && Reaches(from.Value, to.Value)) return piece.Actor;
            }

            return null;
        }

        void Breather()
        {
            Die heart = _hero.Attribute(Attr.Heart);
            int shed = Rest.Breather(_hero);

            GD.Print("");
            GD.Print($"rest    a breather - {shed} strain shed, Heart {heart.Label()} back to " +
                     $"{_hero.Attribute(Attr.Heart).Label()}, {_hero.Nerve} nerve");
        }

        void Watch()
        {
            _heart = false;

            if (_fight.Ready())
            {
                GD.Print($"        {_hero.DebugName} stops and watches - a readied strike at the " +
                         "first thing that comes within reach");
            }
            else
            {
                _fight.EndTurn();
                GD.Print($"        {_hero.DebugName} is done");
            }

            _beat = Beat;
        }

        void Engage(Actor target)
        {
            Cell? from = _board.CellOf(_board.Piece);
            Cell? to = _board.CellOf(PieceFor(target));

            if (from == null || to == null) return;

            if (Reaches(from.Value, to.Value)) { Swing(target); return; }

            if (FirstOf(Primitive.Channel) != null && Sight.Clear(_board.Map, from.Value, to.Value))
            {
                Channel(target);
                return;
            }

            March(to.Value);
        }

        IReadOnlyList<Ability> KitFor(Actor hero)
        {
            Content.Classes.ClassCard card = _archetypes.ClassOf(hero?.Id);

            if (card == null) return SharedKit.All;

            Ability[] kit = _archetypes.KitOf(card).ToArray();

            return kit.Length > 0 ? kit : SharedKit.All;
        }

        Ability FirstOf(Primitive shape)
        {
            foreach (Ability ability in _kit)
                if (ability.Primitive == shape && ability.Offered(_hero) && ability.Affordable(_hero))
                    return ability;

            return null;
        }

        // cost comes off as the dice leave the hand, not on landing
        void Use(Ability ability, Actor target)
        {
            if (ability == null) return;

            Pool pool = ability.PoolFor(_hero);

            if (pool.Count == 0)
            {
                GD.PushError($"fight: the hero has nothing to throw for {ability.Id}");
                return;
            }

            if (!ability.Pay(_hero))
            {
                GD.Print($"        {_hero.DebugName} cannot pay for {ability.Id}");
                return;
            }

            _swingingAt = target;
            _ability = ability;
            _reacting = false;
            _interrupted = null;
            _impact = null;
            _using = ability.Attribute;

            int against = ability.Difficulty(target?.Defense ?? 0);

            // the readout must score against the ability's own difficulty, or a hit shows for what the rules call a miss
            _tray.TargetDifficulty = against;

            GD.Print("");
            GD.Print($"ability {_hero.DebugName} uses {ability.Id}" +
                     (target != null && target != _hero ? $" at {target.DebugName}" : " on himself") +
                     $" - {ability.Attribute} + {ability.Skill}" +
                     (ability.UseGear ? $" + {_hero.WeaponId}" : "") +
                     $", {pool.Count} dice vs {against}" +
                     (ability.Cost == Cost.None ? "" : $"; {ability.Cost} paid") +
                     $"; strain {_hero.Strain}, Heart now {_hero.Attribute(Attr.Heart).Label()}");

            _tray.Throw(pool);
        }

        void Channel(Actor target) => Use(FirstOf(Primitive.Channel), target);

        void March(Cell to)
        {
            Cell? from = _board.CellOf(_board.Piece);

            if (from == null) return;

            Cell landed = Approach(from.Value, to);

            if (landed == from.Value)
            {
                // a refused move costs no action: it did not happen
                _board.Refuse(_board.Piece, to, from.Value);
                return;
            }

            if (!_board.Walk(_board.Piece, landed)) return;

            GD.Print($"move    {_hero.DebugName} {from} to {landed}");
            _fight.Spend();
        }

        // when the target square is taken, aim beside it, so clicking a far enemy walks up to it
        Cell Approach(Cell from, Cell to)
        {
            IReadOnlyList<Cell> route = Route.Between(_board.Map, from, to, Taken) ?? Nearest(from, to);

            if (route == null) return from;

            IReadOnlyList<Cell> reached = Route.Within(_board.Map, route, _engine.Options.Pace);

            return reached[reached.Count - 1];
        }

        IReadOnlyList<Cell> Nearest(Cell from, Cell to)
        {
            IReadOnlyList<Cell> best = null;

            foreach (Cell beside in Around(to))
            {
                IReadOnlyList<Cell> way = Route.Between(_board.Map, from, beside, Taken);

                if (way == null) continue;

                if (best == null || Route.Cost(_board.Map, way) < Route.Cost(_board.Map, best)) best = way;
            }

            return best;
        }

        bool Taken(Cell cell) => _board.Squares.IsOccupied(cell);

        static IEnumerable<Cell> Around(Cell cell)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (dx != 0 || dy != 0)
                        yield return new Cell(cell.X + dx, cell.Y + dy);
        }

        // sight, not route: a swing crosses a line it cannot walk, so through a corner it can hit but not step
        bool Reaches(Cell from, Cell to)
        {
            if (from == to) return false;
            if (Math.Abs(from.X - to.X) > 1 || Math.Abs(from.Y - to.Y) > 1) return false;

            return Sight.Clear(_board.Map, from, to);
        }

        // the hero's pool on the real tray; there is no second throwing path and must never be one
        void Swing(Actor target)
        {
            Pool pool = HeroPool();

            if (pool.Count == 0)
            {
                GD.PushError("fight: the hero has no dice to throw - his statblock is empty");
                return;
            }

            _swingingAt = target;
            _impact = null;
            _reacting = false;
            _interrupted = null;

            // the readout must be the number the rules score against, or the console lies about hits
            _tray.TargetDifficulty = target.Defense;

            GD.Print("");
            _friend?.Watches();

            GD.Print($"swing   {_hero.DebugName} at {target.DebugName} - " +
                     $"{_engine.Options.HeroAttackAttr} + {_engine.Options.HeroAttackSkill} + {_hero.WeaponId}, " +
                     $"{pool.Count} dice vs defense {target.Defense}");

            _tray.Throw(pool);
        }

        // the tray also throws for the board; act only on an answer this fight asked for
        void OnThrown(TrayThrow thrown)
        {
            if (_rollingOrder) { Started(thrown.Result.Total); return; }

            if (_swingingAt == null) return;

            if (_impact != null)
            {
                _impact.Landed(thrown.Slots.Count > 0 ? thrown.Slots[0].Value : 0);

                Settle();
                return;
            }

            _felt = thrown;
            _shrugged = false;

            if (Offering())
            {
                _read = ReadSeconds;
                _tray.Picking = _hero.Nerve > 0;
                return;
            }

            Read();
        }

        bool Offering() => _hero.Nerve > 0 || _felt.Result.Trouble;

        // window closed, throw is real: an unshrugged Trouble lands and banks a Nerve
        void Read()
        {
            TrayThrow thrown = _felt;

            _felt = null;
            _read = 0.0;
            _tray.Picking = false;

            if (thrown == null) return;

            if (thrown.Result.Trouble && !_shrugged)
            {
                Trouble.Cost cost = _fight.Accept(_hero, _using);

                GD.Print($"trouble two or more 1s - {Told(cost)}, and a Nerve banked " +
                         $"({_hero.Nerve} of {_hero.NerveCap})");
            }

            _shrugged = false;

            _roll = thrown.Result;
            _impact = new ImpactChain(thrown);

            Settle();
        }

        // developer only, not localized, never reaches the screen
        string Told(Trouble.Cost cost) => cost switch
        {
            Trouble.Cost.Notched => $"the {_hero.WeaponId} is notched, now {_hero.Weapon.Label()}",
            Trouble.Cost.Condition => $"{_hero.DebugName} is {_using.Pressing()}",
            _ => "nothing left to take it",
        };

        // re-throw one die for a Nerve; the window reopens on the answer, so three Nerve is three re-throws
        void OnPicked(int slot)
        {
            if (_felt == null || _swingingAt == null) return;

            if (!_fight.SpendNerve(_hero))
            {
                GD.Print($"        {_hero.DebugName} has no Nerve left to re-throw with");
                return;
            }

            _felt = null;
            _read = 0.0;
            _tray.Picking = false;
            _shrugged = false;

            _tray.Rethrow(slot);
        }

        // skip the Impact throw where the number is unused: a miss, or a Rabble with no health track
        void Settle()
        {
            if (!_impact.GoesAgain) { Land(); return; }

            if (!_swingingAt.Tier.HasHealthTrack() || !_roll.Beats(_swingingAt.Defense))
            {
                Land();
                return;
            }

            if (_impact.IsExploding && !_engine.Options.ImpactExplodes) { Land(); return; }

            GD.Print(_impact.IsExploding
                ? $"        the {_impact.Die.Label()} came up {_impact.Die.Sides()} - it goes back " +
                  $"in the hand, {_impact.Total} so far"
                : $"        nothing was left over, so the {_impact.Die.Label()} comes out of the box");

            _tray.Throw(_impact.Again());
        }

        void Land()
        {
            Actor target = _swingingAt;
            int damage = _impact.Total;
            bool reaction = _reacting;
            Ability ability = _ability;

            _swingingAt = null;
            _impact = null;
            _reacting = false;
            _ability = null;

            // an ability reads its own outcome, but damage is still _impact.Total, the same as a swing
            if (ability != null)
            {
                Apply(ability, target, damage, reaction);
                return;
            }

            // a reaction costs a reaction and nobody's turn; a strike costs the actor's action
            if (reaction) _fight.React(_hero, target, _roll, damage);
            else _fight.Strike(target, _roll, damage);

            ReadItBack(target, damage);

            // a Snag (a single 1) has no mechanical effect; it is just the DM reacting -
            // and, since W2, the companion having an opinion about it out loud
            if (_roll is { Snag: true }) _dm?.Reacts();

            _friend?.Sees(_roll);

            // the interrupted mover gets its turn back, or loses it if the strike downed it
            if (_interrupted != null)
            {
                if (_interrupted.IsDown) _fight.EndTurn();
                else _fight.Spend();

                _interrupted = null;
            }

            // refresh now, not next frame, or a mark misses its own flare
            Refresh();

            _beat = Beat;
        }

        // damage goes through Encounter.Strike, keeping bolt and swing one mechanism; a no-damage ability still costs the action
        void Apply(Ability ability, Actor target, int magnitude, bool reaction)
        {
            AbilityOutcome outcome = ability.Read(_roll, magnitude, target?.Defense ?? 0);

            GD.Print($"        {outcome}");

            if (outcome.Does(Effect.Damage) && target != null)
            {
                if (reaction) _fight.React(_hero, target, _roll, outcome.Magnitude);
                else _fight.Strike(target, _roll, outcome.Magnitude);

                ReadItBack(target, outcome.Magnitude);
            }
            else
            {
                // the turn still goes, or a missed checked action would be a free retry
                _fight.Spend();
            }

            if (outcome.Does(Effect.Recoil) && outcome.Magnitude > 0)
            {
                IReadOnlyList<Condition> taken = _hero.Damage(outcome.Magnitude);

                GD.Print($"        {_hero.DebugName} takes {outcome.Magnitude} from it" +
                         (taken.Count > 0 ? " - and takes " + string.Join(", ", taken) : ""));
            }

            // on the target for a channel, on the thrower for a check
            if (outcome.Condition is { } condition)
            {
                Actor wearer = ability.Primitive == Primitive.Channel ? target : _hero;

                if (wearer != null && wearer.ApplyCondition(condition))
                    GD.Print($"        {wearer.DebugName} is {condition}");
            }

            if (outcome.Does(Effect.Steady))
            {
                Condition pressing = ability.Attribute.Pressing();

                GD.Print(_hero.ClearCondition(pressing)
                    ? $"        {_hero.DebugName} steadies - {pressing} shaken off"
                    : $"        {_hero.DebugName} steadies, and was not {pressing} to begin with");
            }

            Cell? standing = _board.CellOf(_board.Piece);
            Piece struck = target == null ? null : _pieces.Of(target);
            Cell? where = struck == null ? null : _board.CellOf(struck.Mini);

            if (outcome.Does(Effect.Rough) && where != null)
                GD.Print(_board.Foul(where.Value)
                    ? $"        {where.Value} is difficult ground now"
                    : $"        {where.Value} was not plain floor to foul");

            if (outcome.Does(Effect.Shove) && struck != null && standing != null)
                GD.Print(_board.Shove(struck.Mini, standing.Value)
                    ? $"        {target.DebugName} is shoved back to {_board.CellOf(struck.Mini)}"
                    : $"        {target.DebugName} has nowhere to be shoved to");

            Refresh();

            _beat = Beat;
        }

        // THE THROW, READ BACK IN A VOICE AT THE TABLE (W2, DM_PRESENCE.md D5).
        //
        // "Either the DM or the companion delivers these - whichever, they are lines like any other
        // bark." Which one is content: a companion whose bark bank says reads_the_throw takes it,
        // and otherwise the DM does exactly what it did before W. The one thing that must never
        // happen is that these numbers reach the player as a HUD panel (THE_TABLE.md section 6).
        void ReadItBack(Actor target, int impact)
        {
            if (target == null || _roll == null) return;

            int against = target.Defense;
            bool beat = _roll.Beats(against);

            // numbered names carry their own {0}; use Format not Get, or the braces show
            string named = target.Ordinal > 0
                ? _text.Format(target.NameKey, target.Ordinal)
                : _text.Get(target.NameKey);

            // the first time its guard is cleared, and never again: a number you already know is
            // not a reveal
            bool reveal = beat && _guardsSeen.Add(target);

            if (Reader() is { } voice)
            {
                voice.Line(Content.Dialogue.DialogueKeys.Readout(
                               _friend.Speaker, beat ? Readout.Hit : Readout.Miss),
                           beat ? new object[] { _roll.Total, against, impact }
                                : new object[] { _roll.Total, against });

                if (reveal)
                    voice.Line(Content.Dialogue.DialogueKeys.Readout(_friend.Speaker, Readout.Guard),
                               named, against);

                return;
            }

            if (_dm == null) return;

            _dm.Says(beat ? Game.Dm.DmLines.Hit : Game.Dm.DmLines.Miss,
                     beat ? new object[] { _roll.Total, against, impact }
                          : new object[] { _roll.Total, against });

            if (reveal) _dm.Says(Game.Dm.DmLines.Guard, named, against);
        }

        // the companion, but only where its own bank says it does this job
        Game.Companion.Companion Reader()
        {
            if (_friend == null || _friend.Speaker.Length == 0) return null;

            return _archetypes.BarksFor(_friend.Speaker) is { ReadsTheThrow: true } ? _friend : null;
        }

        // The cues at this moment that name a beat of the shared spine (W5). Which companion is at
        // the table changes the PHRASING and nothing else - and where this one has no phrasing,
        // the DM says it, so the information reaches the player either way.
        void Prompt(Content.Places.When moment)
        {
            if (_board?.Plan == null || _board.Campaign.Length == 0) return;

            foreach (Content.Places.Cue cue in _board.Plan.CuesFor(moment))
            {
                if (!cue.Prompts) continue;

                string voice = _friend == null ? "" : _friend.Speaker;

                if (voice.Length > 0 && _text.Has(cue.BeatKey(voice, _board.Campaign)))
                {
                    _friend.Line(cue.BeatKey(voice, _board.Campaign));
                    continue;
                }

                string dm = cue.BeatKey(Content.Dialogue.Beat.TheDm, _board.Campaign);

                if (_text.Has(dm)) _dm?.Says(dm);
            }
        }

        public override void _Process(double delta)
        {
            Refresh();

            // the beat must run down even when the fight is over, or Settled never comes true and a headless check hangs
            if (_beat > 0.0) _beat = Math.Max(0.0, _beat - delta);

            if (_fight is { IsOver: true } && !_cleared)
            {
                _cleared = true;
                _dm?.Perform(_board.Plan, Content.Places.When.Cleared, _board.Campaign);

                if (_hero.IsDown) _friend?.SeesTheHeroDown();
                else _friend?.SeesTheRoomCleared();

                // the beat every companion at this table has its own phrasing of (W5)
                Prompt(Content.Places.When.Cleared);
            }

            // only at an idle table: a rattle on top of a swing is a sound effect, not a tell
            if (_fight is { IsOver: false } && _fight.AwaitingHero && Settled)
                _dm?.MightRollForNothing(delta);

            if (_felt != null)
            {
                _read -= delta;

                if (_read <= 0.0) Read();

                return;
            }

            if (_fight == null || _fight.IsOver || _rollingOrder) return;

            if (_beat > 0.0) return;

            if (!Settled) return;

            if (_fight.AwaitingHero) return;

            TakeFoeStep();
        }

        void TakeFoeStep()
        {
            Actor foe = _fight.Acting;

            if (foe == null) return;

            if (foe.IsDown) { _fight.EndTurn(); return; }

            Actor prey = _fight.TargetFor(foe);

            if (prey == null) { _fight.EndTurn(); return; }

            Mini walking = PieceFor(foe);
            Cell? from = _board.CellOf(walking);
            Cell? to = _board.CellOf(PieceFor(prey));

            if (from == null || to == null) { _fight.EndTurn(); return; }

            if (Reaches(from.Value, to.Value))
            {
                // ONCE A ROUND, NOT ONCE A SWING (the eye check, 2026-09-16). A foe's attack is a
                // roll the player genuinely cannot see - the hero's dice go on the tray and the
                // foes' do not - so a rattle behind the screen is the right sound for it. Firing
                // it per swing is what was wrong: four Rabble and a Rival is five rattles a round,
                // back to back, and a sound that happens five times a round is furniture.
                //
                // A real DM picks the dice up once and rolls the room's attacks together, which is
                // both the fix and the reason it is the right fix.
                if (_rattled != _fight.Round)
                {
                    _rattled = _fight.Round;

                    _dm?.RollsForSomething();
                    _friend?.HearsASecretRoll();
                }

                _fight.Strike(prey, foe.Tier.AttackAttr(), foe.Tier.AttackSkill());
                Refresh();
                _beat = Beat;
                return;
            }

            Cell landed = Approach(from.Value, to.Value);

            if (landed == from.Value)
            {
                GD.Print($"        {foe.DebugName} cannot get to {prey.DebugName}");
                _fight.EndTurn();
                return;
            }

            _board.Walk(walking, landed);
            GD.Print($"move    {foe.DebugName} {from} to {landed}");

            if (React(foe, landed)) return;

            _fight.Spend();
            _beat = Beat;
        }

        Pool HeroPool()
        {
            Pool pool = _hero.BuildPool(_engine.Options.HeroAttackAttr, _engine.Options.HeroAttackSkill);

            _using = _engine.Options.HeroAttackAttr;

            if (!_heart) return pool;

            _heart = false;

            if (!Nerve.CanAddHeart(_hero, pool) || !_fight.SpendNerve(_hero)) return pool;

            GD.Print($"        a Nerve buys the Heart die - {pool.Count + 1} dice on the felt");

            return Nerve.WithHeart(_hero, pool);
        }

        // true means the dice are in the air and the mover's turn is on hold
        bool React(Actor foe, Cell landed)
        {
            if (!_fight.IsReadied(_hero) || _fight.ReactionsLeft(_hero) <= 0) return false;

            Cell? hero = _board.CellOf(_board.Piece);

            if (hero == null || !Reaches(hero.Value, landed)) return false;

            Pool pool = HeroPool();

            if (pool.Count == 0) return false;

            _swingingAt = foe;
            _reacting = true;
            _interrupted = foe;
            _impact = null;

            _tray.TargetDifficulty = foe.Defense;

            GD.Print("");
            GD.Print($"react   {_hero.DebugName} was watching - a readied strike at " +
                     $"{foe.DebugName} as it steps into reach");

            _tray.Throw(pool);
            return true;
        }

        // refreshed every frame, so state changed outside the fight still shows; the observer adds only the flare
        void Refresh()
        {
            foreach (Piece piece in _pieces.All)
            {
                if (piece.Vigor != null && piece.Vigor.Showing != piece.Actor.Vigor)
                    piece.Vigor.Show(piece.Actor.Vigor);

                piece.Marks?.Show(piece.Actor.Conditions);
            }

            _nerve?.Show(_hero.Nerve, _heart ? 1 : 0);
            _order?.Mark(_fight?.Acting);
        }
    }
}
