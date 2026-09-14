using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Core.Space;
using Game.Board;
using Game.Localization;
using Game.Tray;

// Game.Board is a namespace AND Game.Board.Board is the type in it, so an unqualified `Board`
// inside Game.Fight binds to the namespace and not to the node. Aliasing it once here is the
// whole of the fix, and it keeps every reference below reading as what it is
using BoardNode = Game.Board.Board;

namespace Game.Fight
{
    // THE FIGHT ON THE TABLE (COMBAT_LOOP.md C0-C2) - one hero, a room of enemies, and the handful
    // of dice that decides it.
    //
    // WHAT THIS IS: the thing that turns a click into a rule, and a rule into something to watch.
    // It owns no rules and draws nothing. `Encounter` owns the round structure and the action
    // economy, `CombatEngine` owns whether a swing landed, `DiceTray` throws the hero's own pool,
    // `Board` says which square was clicked and whether there is a way to another, and
    // `TableObserver` decides what any of it looks like. This introduces them - the way
    // `table.tscn` introduces the board and the tray, and for the same reason: neither of them may
    // know the other exists.
    //
    // IT LIVES ON THE TABLE, NOT ON THE BOARD. A fight needs both the board and the tray, and the
    // one place that knows both are standing on it is `table.tscn`. Putting this inside
    // `board.tscn` would make the board reach for a tray, which is exactly the coupling B4 avoided.
    //
    // THE HERO IS THE BOARD'S HERO. Not a copy - the same Actor the door check swings and damages,
    // so forcing a door the hard way means walking into the room already Winded and throwing a
    // smaller Might die at the first foe.
    //
    // THE HERO'S DICE ARE ON THE FELT AND EVERYBODY ELSE'S ARE BEHIND THE SCREEN. The hero's swing
    // is thrown on the real tray and read off it, because CORE_RULES.md pillar 1 says every roll
    // is a visible handful hitting the table. A foe's swing goes through the resolver: those are
    // the DM's dice, which THE_TABLE.md says you hear and do not see. What you watch is the piece
    // move and the blow land.
    //
    // MAGIC IS THE SAME ROLL AND THEREFORE THE SAME CLICK (COMBAT_LOOP.md C5). A caster clicking
    // something he cannot reach but can SEE throws `Heart + Channeling + focus` at it instead of
    // walking over - same tray, same best two, same Impact die driving the magnitude, and the cost
    // is a step off his Heart die until he rests. There is no second subsystem here because
    // CORE_RULES.md section 10 does not have one.
    //
    // GEOMETRY FEEDS THE RULES AS FAR AS REACH AND NO FURTHER (COMBAT_LOOP.md C2). You must be
    // beside something to hit it, and closing the distance costs the action you would have hit
    // with. That is what makes a room of Rabble pressure rather than a queue. There is deliberately
    // NO flanking die and NO cover bonus: both would move every number in SIMULATION.md, and
    // nothing in the sim has a position in it to re-measure them with. CONVENTIONS.md 8 - decide
    // it, then simulate it - and the simulating is not possible yet, so the decision is "not yet".
    public partial class Fight : Node3D
    {
        // both set in table.tscn, because only the table knows both are standing on it
        [Export] public NodePath BoardPath { get; set; } = "../Board";

        [Export] public NodePath TrayPath { get; set; } = "../DiceTray";

        // the pieces the foes stand as. art direction, so it is authored in the scene rather than
        // named in code - and a campaign naming its own models (Phase P) replaces these exports
        // and nothing else
        [Export] public PackedScene RivalPiece { get; set; }

        [Export] public PackedScene RabblePiece { get; set; }

        [Export] public PackedScene DreadPiece { get; set; }

        // WHERE THE ROOM STARTS. Content, hardcoded exactly as the door check is (Board/DoorCheck.cs)
        // and for the same reason: Phase P reads spawn points out of the campaign's own map, and
        // until then a room is a handful of squares said in one place. Set in table.tscn
        [Export] public Vector2I RivalAt { get; set; } = new Vector2I(4, 4);

        // WHAT "THIS ROOM HAS NONE OF THOSE" LOOKS LIKE. A square off the board, because every
        // square on it is somewhere a piece could legitimately stand and there is no other way to
        // say nothing with a Vector2I. Phase P reads an encounter out of a campaign and the whole
        // question goes away
        public static readonly Vector2I Nowhere = new Vector2I(-1, -1);

        // Godot's own array rather than Vector2I[] - a C# array of a struct is not a Variant and
        // the exporter refuses it (GD0102). This is the collection type a .tscn writes anyway
        [Export] public Godot.Collections.Array<Vector2I> RabbleAt { get; set; } = new Godot.Collections.Array<Vector2I>();

        // AND WHERE THE BOSS IS, IF THERE IS ONE. One per chapter (CORE_RULES.md section 8), so
        // most rooms have none - the sentinel below is what "no boss in this room" looks like
        // until Phase P reads an encounter out of a campaign
        [Export] public Vector2I DreadAt { get; set; } = Nowhere;

        // the beat between one automatic thing and the next, in seconds. foe turns are not the
        // player's to drive, so they are the one part of a fight that can run away from the eye -
        // long enough to follow a piece moving and a blow landing, short enough that a room of
        // eight is not a cutscene
        [Export] public float Beat { get; set; } = 0.45f;

        // HOW LONG THE FELT IS LEFT TO BE READ before a throw resolves itself (COMBAT_LOOP.md C4).
        //
        // A Nerve re-throws any one die in a pool and you take the new result (CORE_RULES.md
        // section 7), and there is no way to offer that without a moment in which the dice are
        // lying there and nothing has happened yet. At a table that moment is real - you look at
        // what you rolled and decide - so this is a grace period rather than a prompt: click a die
        // to spend a Nerve on it, click anywhere else to take the throw as it lies, or wait and it
        // takes itself.
        //
        // THE WINDOW ONLY OPENS WHEN THERE IS SOMETHING TO DECIDE. A hero with no Nerve and no
        // Trouble on the felt has nothing to spend and nothing to shrug, so the throw resolves the
        // instant it settles and the fight never pauses at all.
        //
        // This is the number to argue with after a week of playing. It is an export for that
        // reason
        [Export] public float ReadSeconds { get; set; } = 1.8f;

        // reproducible fights when you want one: any non-zero value seeds the resolver, so a fight
        // that produced something you did not believe can be re-run exactly (CONVENTIONS.md 6).
        // 0 means the clock, which is what an actual session wants
        [Export] public int Seed { get; set; }

        // the roster this fight is built from. one line, the same one Main.cs and Board name, so
        // a data-backed source (Phase P) is a change here and nowhere below
        readonly IArchetypeSource _archetypes = new BuiltInArchetypes();

        readonly Pieces _pieces = new Pieces();

        // held as the interface so this file cannot start reaching for TranslationServer, and
        // handed to every mark on the mat - GodotLocalizer is the ONE place a key becomes text
        readonly ILocalizer _text = new GodotLocalizer();

        readonly List<Actor> _foes = new List<Actor>();

        BoardNode _board;

        DiceTray _tray;

        CombatEngine _engine;

        // everything watching this fight. The table and the transcript are in it from the start;
        // anything that turns up later - a headless check, a replay recorder - joins through Watch
        CompositeCombatObserver _watching;

        Encounter _fight;

        Actor _hero;

        // the foe the hero is mid-swing at. non-null exactly while the dice are in the air on this
        // fight's behalf, which is also the guard that stops a second click throwing a second time
        Actor _swingingAt;

        // the throw being read, kept across an explosion: what the felt said about the swing, and
        // the Impact die going back in the hand (Tray/ImpactChain.cs)
        PoolResult _roll;

        ImpactChain _impact;

        // seconds before the next automatic step. the player's turn never waits on this
        double _beat;

        // the order, written down the side of the map (C3)
        TurnOrder _order;

        // the dice in the air are for the turn order rather than for a blow. one throw, at the
        // top, before anybody has acted
        bool _rollingOrder;

        // and this swing is a readied strike rather than an action - it costs a reaction, is
        // taken out of turn, and does not advance anybody's turn (Encounter.React)
        bool _reacting;

        // ---- heroic effort (C4) ----

        // the hero's own tokens, beside his piece
        NerveTokens _nerve;

        // the boss's second half, if this room has a boss in it
        PhaseChange _boss;

        // the throw lying on the felt, unread. Non-null exactly while the window is open
        TrayThrow _felt;

        double _read;

        // a Nerve committed to the NEXT throw, which puts the Heart die in the pool. Committed
        // rather than spent: the token stands up and the count does not drop until the dice
        // actually leave the hand, so changing your mind costs nothing
        bool _heart;

        // and a Trouble on the felt already shrugged off, so closing the window does not then
        // charge for it
        bool _shrugged;

        // which attribute the throw being read was made with. It decides where a Trouble lands
        // when there is no gear left to take it (Core.Combat.Trouble)
        Attr _using = Attr.Might;

        // the foe whose move was interrupted by that reaction, and whose own action is still owed.
        // A REACTION INTERRUPTS: it lands before the mover's action is booked, because booking it
        // is what ends the mover's turn - and the end of a turn is the end of a round when the
        // mover is last in the order, which wipes the readied flag before anything could use it.
        // That is exactly the bug this field exists to have fixed: the Rival walked into reach, the
        // round rolled over, and the hero was no longer watching by the time anybody asked
        Actor _interrupted;

        // ---- what a check can ask, and nothing a rule can (COMBAT_LOOP.md, the verify lists) ----
        //
        // FightCheck plays this table headless and holds it to what the felt says. It drives the
        // fight through Board.Claims - the same entry point a mouse click uses - so these are only
        // for LOOKING: who is in the fight, which model an Actor is standing as, and where the
        // round has got to

        public Actor Hero => _hero;

        public IReadOnlyList<Actor> Foes => _foes;

        public IReadOnlyList<Piece> Pieces => _pieces.All;

        public Encounter Encounter => _fight;

        public TurnOrder Order => _order;

        public PhaseChange Boss => _boss;

        public Mini PieceFor(Actor actor) => _pieces.Of(actor)?.Mini;

        // the felt is lying there unread and a Nerve could still change it (C4)
        public bool Reading => _felt != null;

        // ---- and the three gestures, so a headless check can make them ----
        //
        // A Nerve token and a die are both met with a physics ray from wherever the mouse is, and
        // a check has no mouse. These are the same calls those rays end in, so the check drives
        // the fight through what a player does rather than round it - the same argument
        // Board.Claims makes for a click on a square

        public void NerveClicked() => OnNerve();

        public void DiePicked(int slot) => OnPicked(slot);

        // take the throw as it lies, which is what a click on the board does while the felt is
        // being read
        public void TakeTheThrow() { if (_felt != null) Read(); }

        // and "I am done", which is what a click on the hero's own piece does
        public void Done() { if (_fight != null && _fight.AwaitingHero) Watch(); }

        // a breather, which is a key (C5)
        public void Breathe() => Breather();

        // ONE MORE THING WATCHING. `ICombatObserver` is the seam between the rules and anything
        // that cares what happened, and this is how something that was not here at construction
        // gets on the end of it - the headless check, and anything else that ever wants to wrap a
        // fight to look at it (CONVENTIONS.md 6). Polling `Encounter.Acting` from outside instead
        // is what a check tried first, and it raced this node's own _Process: a turn that began
        // and ended between two samples had never happened as far as the sampler could tell
        public bool Watch(ICombatObserver observer) => _watching != null && _watching.Add(observer);

        public bool Unwatch(ICombatObserver observer) => _watching != null && _watching.Remove(observer);

        // nothing is in the air, nothing is walking, and nobody is waiting out a beat
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

            IRng rng = Seed != 0 ? new SeededRng(Seed) : new SeededRng((int)Time.GetTicksMsec());

            // BOTH AT ONCE, which is the C0 verify step: the table animates what it is told and
            // the transcript says what it was told, so a mismatch between them is visible without
            // instrumenting anything (CONVENTIONS.md 6)
            _watching = new CompositeCombatObserver(
                new TableObserver(_board, _pieces),
                new RecordingCombatObserver(GD.Print));

            _engine = new CombatEngine(new StandardResolver(rng), observer: _watching);

            Muster();

            _fight = new Encounter(_engine, _hero, _foes);

            if (_boss != null) _fight.Phases(_boss);

            _board.Claims = OnClick;
            _tray.Resolved += OnThrown;
            _tray.Picked += OnPicked;

            _board.AddChild(_order = new TurnOrder { Name = "TurnOrder", Text = _text });

            // developer diagnostics, not player-facing text
            GD.Print("");
            GD.Print($"fight   {_hero} - beside a foe to strike it, anywhere else to move; " +
                     $"{_engine.Options.HeroActionsPerRound} actions a round, {_engine.Options.Pace} squares a move");
            GD.Print($"        {(TheEffect.Offered(_hero) ? "a foe in sight and out of reach is channelled at" : "not a caster - a foe out of reach is walked at")}" +
                     $", {(char)KeyToBreathe} takes a breather");

            foreach (Piece piece in _pieces.All)
                GD.Print($"        {piece.Actor} on {_board.CellOf(piece.Mini)}");

            RollTheOrder();
        }

        // ONE THROW, AT THE TOP, ON THE REAL TRAY (CORE_RULES.md section 8). The hero's initiative
        // is his own roll, so it is a visible handful like every other roll of his - Grace and
        // Insight and no gear die, which for the starting Barbarian is a single d6 on the felt.
        // Everybody else's is the DM's and is rolled behind the screen.
        //
        // A hero with no Grace at all has nothing to throw, and the encounter scores his the same
        // way it scores a Rabble's: not at all
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

        // ---- who is in the room ----

        void Muster()
        {
            Enlist(_hero, _board.Piece);

            // NUMBERED FROM 1, so presentation can say "Rabble 3" out of one key with a {0} in it
            // rather than gluing an integer onto a translated name (Actor.NameKey)
            int ordinal = 1;

            foreach (Vector2I at in RabbleAt ?? new Godot.Collections.Array<Vector2I>())
                Recruit(EngineIds.Rabble, RabblePiece, at, $"Rabble{ordinal}", ordinal++);

            if (RivalAt != Nowhere) Recruit(EngineIds.Rival, RivalPiece, RivalAt, "Rival", 0);

            if (DreadAt != Nowhere) Recruit(EngineIds.Dread, DreadPiece, DreadAt, "Dread", 0);
        }

        void Recruit(string id, PackedScene model, Vector2I at, string name, int ordinal)
        {
            Mini piece = _board.Place(model, new Cell(at.X, at.Y), name);

            if (piece == null)
            {
                GD.PushError($"fight: '{name}' could not be put on {at} - it is not in this fight");
                return;
            }

            Actor actor = _archetypes.Create(id);

            if (ordinal > 0) actor.Numbered(ordinal);

            _foes.Add(actor);
            Enlist(actor, piece);

            // AND WHAT IT TURNS INTO. A boss is the only thing that does, and what it turns into
            // is a statblock - so this is the placeholder roster's answer (PhaseChange.Standard)
            // for exactly as long as BuiltInArchetypes is the roster. Registered here rather than
            // inside Encounter because a phase change is content and Encounter owns rules
            if (actor.Tier == Tier.Dread) _boss = PhaseChange.Standard(actor);
        }

        // a piece, its tally and the mat beside it. A Rabble gets no tally, and that is the rule
        // showing rather than an omission: no health track, nothing to count (CORE_RULES.md
        // section 8). It DOES get condition marks - a Rabble can be Winded, it just cannot be
        // whittled down
        void Enlist(Actor actor, Mini mini)
        {
            VigorPips pips = null;

            if (actor.Tier.HasHealthTrack())
            {
                mini.AddChild(pips = new VigorPips { Name = "Vigor" });
                pips.Track(actor.MaxVigor);
            }

            var marks = new ConditionMarks { Name = "Conditions", Text = _text };
            mini.AddChild(marks);

            // and heroic effort, which only the hero has any of. A Rabble's cap is zero, so this
            // would build nothing for one anyway - the check is here to say so rather than to
            // leave an empty node beside every mook
            if (actor.NerveCap > 0)
            {
                var tokens = new NerveTokens { Name = "Nerve" };
                mini.AddChild(tokens);
                tokens.Track(actor.NerveCap);

                if (ReferenceEquals(actor, _hero)) _nerve = tokens;
            }

            _pieces.Add(actor, mini, pips, marks);
        }

        // ---- the hero's turn: a click is an action ----

        // A CLICK ON A SQUARE, offered to the fight before the board does anything with it. True
        // means the fight took it; false hands it back, so the board still walks its own piece
        // around a room when there is no fight on
        bool OnClick(Cell cell)
        {
            if (_fight == null) return false;

            // the fight is over - the board is somebody's to walk around again
            if (_fight.IsOver) return false;

            // THE FELT IS BEING READ, so this click is the answer to that and to nothing else:
            // take the throw as it lies. Swallowed rather than acted on, because a player looking
            // at the dice and reaching for the board means "these will do" and not "and also move
            // over there"
            if (_felt != null) { Read(); return true; }

            // the dice are in the air, on this fight's behalf or on the door's, or something is
            // still moving. the hero is busy, and a click now would be a piece walking away from
            // its own swing
            if (!Settled || _board.IsChecking) return true;

            // not his turn. clicks wait, and nothing is said about it: a table where the pieces do
            // not move when you push them is a table where it is somebody else's go
            if (!_fight.AwaitingHero) return true;

            Piece target = _pieces.On(_board.Squares.At(cell));

            if (target != null && !ReferenceEquals(target.Actor, _hero) && !target.IsDown)
            {
                Engage(target.Actor);
                return true;
            }

            // HIS OWN SQUARE IS THE ONE GESTURE THE FIGHT HAS LEFT, and C3 gives it a meaning:
            // stop, and watch. Give up what is left of the turn and take a readied strike at the
            // first thing that comes within reach (CORE_RULES.md section 8, one reaction).
            //
            // Clicking your own piece rather than pressing a key, because there is no menu on a
            // table and no keyboard on one either - and because "I stop here, and I am watching
            // you" is a thing somebody does by tapping their own model
            if (target != null)
            {
                if (ReferenceEquals(target.Actor, _hero)) Watch();
                return true;
            }

            March(cell);
            return true;
        }

        // A CLICK ON A NERVE TOKEN, met with the token's own body rather than with the mat -
        // see NerveTokens. What it buys is decided here and nowhere else, by what the table is
        // waiting for, in this order:
        //
        //   1. a Trouble on the felt      shrug it off before it resolves
        //   2. the Heart die already armed  change your mind, and it costs nothing
        //   3. actions left on this turn    arm the Heart die for the next throw
        //   4. no actions left              push through and act one more time
        //
        // Four spends, one gesture, and a total function - a token click always does exactly one
        // of them, so there is never a click that reads as nothing happening. The ordering is the
        // only thing here that is a judgement: a Trouble is the most urgent because it is about to
        // resolve, and pushing is last because it is the only one that is still available after
        // everything else on the turn is gone
        public override void _UnhandledInput(InputEvent @event)
        {
            if (_fight == null) return;

            if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: KeyToBreathe })
            {
                Breather();
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
            // 1. a Trouble is about to resolve
            if (_felt != null && _felt.Result.Trouble && !_shrugged)
            {
                if (!_fight.SpendNerve(_hero)) return;

                _shrugged = true;
                GD.Print($"        {_hero.DebugName} shrugs the Trouble off");
                return;
            }

            if (_felt != null) return;
            if (!_fight.AwaitingHero) return;

            // 2. changed his mind about the fourth die
            if (_heart)
            {
                _heart = false;
                GD.Print($"        {_hero.DebugName} takes the Heart die back out of the pool");
                return;
            }

            // 3. the fourth die, for the next throw
            if (_fight.ActionsLeft > 0 && _hero.Nerve > 0 &&
                Nerve.CanAddHeart(_hero, _hero.BuildPool(_engine.Options.HeroAttackAttr,
                                                         _engine.Options.HeroAttackSkill)))
            {
                _heart = true;
                GD.Print($"        {_hero.DebugName} commits a Nerve - the Heart die goes in the next pool");
                return;
            }

            // 4. one more action
            if (_fight.Push())
            {
                GD.Print($"        {_hero.DebugName} pushes through - {_fight.ActionsLeft} action left, " +
                         $"{_hero.Nerve} nerve");
                return;
            }

            GD.Print($"        {_hero.DebugName} has nothing to spend a Nerve on right now");
        }

        // I AM DONE - and watching, if there is a reaction left to watch with. The hero's turn
        // does not end itself while he still has a Nerve to push with (Encounter.Done), so saying
        // so is a gesture the fight needs, and this is it
        // A BREATHER (CORE_RULES.md section 11) - a few minutes anywhere safe, which clears Strain
        // and gives back one Nerve. A developer affordance on a key rather than a gesture, exactly
        // as the board's R is, and for the same reason: resting between rooms is Phase P's shape
        // and this is here so a caster can be watched shrinking and coming back in one sitting
        const Key KeyToBreathe = Key.B;

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

        // BESIDE IT, SO SWING. Out of reach but in sight, and a caster throws something at it.
        // Otherwise close the distance, which is what the action buys instead.
        //
        // One click, three meanings, and the geometry picks between them - which is the same
        // arrangement the whole fight uses: the board answers WHICH SQUARE and everything above it
        // decides what that means
        void Engage(Actor target)
        {
            Cell? from = _board.CellOf(_board.Piece);
            Cell? to = _board.CellOf(PieceFor(target));

            if (from == null || to == null) return;

            if (Reaches(from.Value, to.Value)) { Swing(target); return; }

            if (TheEffect.Offered(_hero) && Sight.Clear(_board.Map, from.Value, to.Value))
            {
                Channel(target);
                return;
            }

            March(to.Value);
        }

        // THE ONE EFFECT (Fight/TheEffect.cs), thrown on the real tray like everything else the
        // hero rolls. The pool is the caster's own and the cost comes off his Heart die as the
        // dice leave the hand - a spell that fizzled still took it out of you
        void Channel(Actor target)
        {
            Pool pool = TheEffect.Cast(_hero);

            if (pool.Count == 0)
            {
                GD.PushError("fight: the hero has nothing to channel with");
                return;
            }

            _swingingAt = target;
            _reacting = false;
            _interrupted = null;
            _impact = null;
            _using = TheEffect.Attribute;

            GD.Print("");
            GD.Print($"channel {_hero.DebugName} at {target.DebugName} - {TheEffect.Attribute} + " +
                     $"{TheEffect.Skill} + {_hero.WeaponId}, {pool.Count} dice vs defense " +
                     $"{target.Defense}; strain {_hero.Strain}, Heart now " +
                     $"{_hero.Attribute(TheEffect.Attribute).Label()}");

            _tray.Throw(pool);
        }

        // one move action: as far along the way there as Pace reaches, stopping short of a square
        // somebody is standing on
        void March(Cell to)
        {
            Cell? from = _board.CellOf(_board.Piece);

            if (from == null) return;

            Cell landed = Approach(from.Value, to);

            if (landed == from.Value)
            {
                // no way there, or nowhere to go - shown on the board rather than in a message box,
                // because there is no message box on a table (THE_TABLE.md 6). It costs no action:
                // a refused move is a move that did not happen
                _board.Refuse(_board.Piece, to, from.Value);
                return;
            }

            if (!_board.Walk(_board.Piece, landed)) return;

            GD.Print($"move    {_hero.DebugName} {from} to {landed}");
            _fight.Spend();
        }

        // ---- geometry, as far as it feeds the rules ----

        // as far as one move carries a piece toward a square. When the square is TAKEN - which is
        // what "walk at that foe" means - the destination is a square beside it instead, so
        // clicking an enemy across the room walks up to it rather than being refused
        Cell Approach(Cell from, Cell to)
        {
            IReadOnlyList<Cell> route = Route.Between(_board.Map, from, to, Taken) ?? Nearest(from, to);

            if (route == null) return from;

            IReadOnlyList<Cell> reached = Route.Within(_board.Map, route, _engine.Options.Pace);

            return reached[reached.Count - 1];
        }

        // the cheapest way to a square beside that one - the same shape as Board.NearestApproach,
        // which walks up to a door rather than into it
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

        // REACH. Beside it, and with nothing on the line between - a wall between two squares is a
        // wall you cannot reach through, which is the same question Route asks of the same corner
        // and gets the same answer to (EDGE_WALLS.md). Sight is the right test rather than Route's,
        // because a swing crosses a line and does not walk it: two pieces either side of the corner
        // where two walls meet can see and hit each other, and neither can step there
        bool Reaches(Cell from, Cell to)
        {
            if (from == to) return false;
            if (Math.Abs(from.X - to.X) > 1 || Math.Abs(from.Y - to.Y) > 1) return false;

            return Sight.Clear(_board.Map, from, to);
        }

        // ---- the swing, and the die that would not stop ----

        // the hero's own pool - attribute, skill, gear - thrown on the real tray. Every bit of
        // M0-M9 does the rest; there is no second throwing path and there must never be one
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

            GD.Print("");
            GD.Print($"swing   {_hero.DebugName} at {target.DebugName} - " +
                     $"{_engine.Options.HeroAttackAttr} + {_engine.Options.HeroAttackSkill} + {_hero.WeaponId}, " +
                     $"{pool.Count} dice vs defense {target.Defense}");

            _tray.Throw(pool);
        }

        // the felt has stopped moving. the tray throws for the board and for itself too, so only an
        // answer to a question this fight asked is one it may act on
        void OnThrown(TrayThrow thrown)
        {
            if (_rollingOrder) { Started(thrown.Result.Total); return; }

            if (_swingingAt == null) return;

            // the Impact die has come back down - either the one that went back in the hand, or
            // the d4 that came out of the box
            if (_impact != null)
            {
                _impact.Landed(thrown.Slots.Count > 0 ? thrown.Slots[0].Value : 0);

                Settle();
                return;
            }

            // THE FELT IS LEFT TO BE READ before anything is made of it, so a Nerve can re-throw
            // one die of it (CORE_RULES.md section 7). Nothing is decided until the window closes
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

        // is there anything to decide? A Nerve to re-throw a die with, or a Trouble that is about
        // to resolve and could be shrugged off or taken. With neither, the throw takes itself and
        // the fight never pauses
        bool Offering() => _hero.Nerve > 0 || _felt.Result.Trouble;

        // THE WINDOW IS CLOSED AND THE THROW IS REAL. Whatever is lying on the felt now is what
        // happened - including a Trouble that nobody shrugged, which lands and banks a Nerve
        // (CORE_RULES.md section 7, the loop worth protecting)
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

        // DEVELOPER ONLY - not localized, never reaches the screen
        string Told(Trouble.Cost cost) => cost switch
        {
            Trouble.Cost.Notched => $"the {_hero.WeaponId} is notched, now {_hero.Weapon.Label()}",
            Trouble.Cost.Condition => $"{_hero.DebugName} is {_using.Pressing()}",
            _ => "nothing left to take it",
        };

        // A DIE, PICKED UP AND THROWN AGAIN, FOR A NERVE. The window closes while it is in the
        // air and opens again on the answer, so a player with three Nerve can re-throw three times
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

        // THE IMPACT DIE EXPLODES BY BEING PICKED UP AND THROWN AGAIN (CORE_RULES.md section 2,
        // SIMULATION.md section 4). `StandardResolver` does it with a loop, which is right for a
        // fight nobody is watching. In front of a player it is the moment the whole rule exists
        // for - a d12 rolling 12 into another 12 is something people talk about - so the die goes
        // back in the hand and onto the felt, alone.
        //
        // A pool of one is an ordinary pool as far as the tray is concerned (SEAMS.md section 8,
        // lifted in C2): it sits the other dice out and throws the one it was handed.
        //
        // WHETHER IT IS WORTH PICKING UP AT ALL. The chain knows the die came up its maximum; this
        // knows whether a bigger number would change anything. A miss and a Rabble both settle
        // without it - the same short circuit CombatEngine.Attack makes before asking its resolver,
        // and for the same reason: a roll nobody is going to use is a roll nobody should watch
        // THE IMPACT DIE IS THROWN, OR THE BLOW LANDS. Two reasons it might still need throwing:
        // it has never been thrown, because the pool left nothing over and the rules hand you a d4
        // (CORE_RULES.md section 2); or it came up its maximum and explodes (section 4).
        //
        // The first happens whatever the options say - it is not an explosion, it is the die. The
        // second is `CombatOptions.ImpactExplodes`, which is a dial the sim has measured.
        //
        // Both are skipped where the number would not be used anyway: a miss, and a Rabble, which
        // has no health track for a bigger number to matter to. That is the same short circuit
        // CombatEngine.Attack makes before asking its resolver
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
                : $"        nothing was left over, so the {_impact.Die.Label()} comes out of the box " +
                  "(CORE_RULES 2)");

            _tray.Throw(_impact.Again());
        }

        void Land()
        {
            Actor target = _swingingAt;
            int damage = _impact.Total;
            bool reaction = _reacting;

            _swingingAt = null;
            _impact = null;
            _reacting = false;

            // a reaction costs a reaction and nobody's turn; a strike costs the action of whoever
            // is having one. Encounter is what knows the difference
            if (reaction) _fight.React(_hero, target, _roll, damage);
            else _fight.Strike(target, _roll, damage);

            // and the interrupted mover gets its turn back - or loses it, if the strike was the
            // one that took it off the board
            if (_interrupted != null)
            {
                if (_interrupted.IsDown) _fight.EndTurn();
                else _fight.Spend();

                _interrupted = null;
            }

            // BEFORE THE NEXT FRAME, not on it. The observer's flare is recorded inside the strike
            // above, and a mark that appeared a frame later would be a mark that missed its own
            // flare - and would leave anything reading the table immediately after an outcome
            // looking at the state before it
            Refresh();

            _beat = Beat;
        }

        // ---- the foes' turns, which the player does not drive ----

        public override void _Process(double delta)
        {
            Refresh();

            // THE BEAT RUNS DOWN EVEN WHEN THE FIGHT IS OVER. It used to be counted after the
            // early return below, so the last blow of a fight left the table permanently
            // unsettled - `Settled` false forever, and anything waiting for the fight to finish
            // waiting for good. Nothing in play noticed, because nothing in play was waiting;
            // the headless check was, and sat there for forty-five seconds
            if (_beat > 0.0) _beat = Math.Max(0.0, _beat - delta);

            // the felt is being read. it takes itself when the moment is up, which is what makes
            // this a grace period rather than a prompt somebody has to dismiss
            if (_felt != null)
            {
                _read -= delta;

                if (_read <= 0.0) Read();

                return;
            }

            if (_fight == null || _fight.IsOver || _rollingOrder) return;

            if (_beat > 0.0) return;

            // the dice are in the air, or a piece is still walking. one thing at a time on a table
            if (!Settled) return;

            // the hero's turn is the player's, and it waits as long as it likes
            if (_fight.AwaitingHero) return;

            TakeFoeStep();
        }

        // ONE ACTION of the foe whose turn it is. An ordinary foe gets one, so this is usually the
        // whole turn: close the distance, or swing if it is already close. That is what makes a
        // room of Rabble pressure rather than a queue - four of them spend their first turn
        // arriving, and after that the hero is surrounded and cannot walk away without spending an
        // action of his own to do it
        void TakeFoeStep()
        {
            Actor foe = _fight.Acting;

            if (foe == null) return;

            // cut down in the middle of its own turn - by a readied strike, and one day by
            // anything else that can happen out of turn. It does not get to finish it
            if (foe.IsDown) { _fight.EndTurn(); return; }

            Actor prey = _fight.TargetFor(foe);

            if (prey == null) { _fight.EndTurn(); return; }

            Mini walking = PieceFor(foe);
            Cell? from = _board.CellOf(walking);
            Cell? to = _board.CellOf(PieceFor(prey));

            if (from == null || to == null) { _fight.EndTurn(); return; }

            if (Reaches(from.Value, to.Value))
            {
                _fight.Strike(prey, foe.Tier.AttackAttr(), foe.Tier.AttackSkill());
                Refresh();
                _beat = Beat;
                return;
            }

            Cell landed = Approach(from.Value, to.Value);

            if (landed == from.Value)
            {
                // penned in behind the others, or no way round at all. it loses the turn, which is
                // exactly what a crowd of Rabble costs a crowd of Rabble
                GD.Print($"        {foe.DebugName} cannot get to {prey.DebugName}");
                _fight.EndTurn();
                return;
            }

            _board.Walk(walking, landed);
            GD.Print($"move    {foe.DebugName} {from} to {landed}");

            // AND WALKED ONTO THE HERO'S AXE. The one reaction trigger the game has: the hero gave
            // up an action to watch, and this is the thing that came within reach. It resolves
            // BEFORE the mover's action is booked - see _interrupted
            if (React(foe, landed)) return;

            _fight.Spend();
            _beat = Beat;
        }

        // THE HERO'S OWN HANDFUL, with the Heart die in it when a Nerve has been committed to
        // this throw. The commitment is only spent here, as the dice leave the hand - so a player
        // who armed it and then changed their mind has lost nothing, and a Nerve is never taken
        // for a throw that did not happen
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

        // the readied strike, if there is one to take and something to take it at. True means the
        // dice are in the air and the mover's turn is on hold until they land
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

            GD.Print("");
            GD.Print($"react   {_hero.DebugName} was watching - a readied strike at " +
                     $"{foe.DebugName} as it steps into reach");

            _tray.Throw(pool);
            return true;
        }

        // ---- the mat is a view of the actors, and never its own copy ----

        // Refreshed every frame rather than written when something happens, so anything that costs
        // vigor or lands a Condition outside this fight - a door forced the hard way is the one
        // that exists today - still shows on the table. What the observer adds is the flare: the
        // one thing a per-frame refresh cannot know is that a Condition is NEW rather than merely
        // still true
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
