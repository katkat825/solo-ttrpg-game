using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Characters;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Game.Audio;
using Game.Diagnostics;
using Game.Dice;
using Game.Localization;

namespace Game.Tray
{
    // the tray scene's only wiring - press throw, all three dice get kicked, and when the
    // last one stops the tray hands what is on the felt to the rules
    // deliberately thin: it owns no rules, TrayResolution owns faces to PoolResult
    // the outcome is read off the felt via TrayMarks - nothing here is in screen space
    public partial class DiceTray : Node3D
    {
        // children are read in scene order and paired with the throw points by index
        [Export] public NodePath DiceRootPath { get; set; } = "Dice";

        [Export] public NodePath ThrowPointsRootPath { get; set; } = "ThrowPoints";

        // floor and walls are two bodies because physics_material_override belongs to a
        // StaticBody3D and not to a CollisionShape3D - one body cannot have a felt floor
        // and wooden walls at all
        [Export] public NodePath TrayFloorPath { get; set; } = "TrayFloor";

        [Export] public NodePath TrayWallsPath { get; set; } = "TrayWalls";

        // dresses both bodies and tells the dice what they are landing on
        [Export] public TraySkin Skin { get; set; }

        // physics ticks between each die leaving the hand
        // at 120Hz 3 ticks is 25ms - under the threshold where the ear hears two impacts,
        // but enough that the dice are not occupying the same air
        // set to 0 to launch on one frame and hear why that was a problem
        [Export] public int LaunchStaggerTicks { get; set; } = 3;

        // physics ticks between the shake and the dice leaving the hand - 14 is about 120ms
        // deliberate latency: nobody drops dice, they shake them and then let go
        // set to 0 to take the shake out
        [Export] public int RattleLeadTicks { get; set; } = 14;

        // dice queued to launch, with ticks remaining before each goes
        private readonly List<(DieBody Die, Transform3D From, int Delay)> _pending = new();

        [Export] public int TargetDifficulty { get; set; } = Difficulty.Standard;

        // what the shape key steps through, after the pool the scene was authored with
        static readonly Die[] ShapeTour = { Die.D4, Die.D6, Die.D8, Die.D10, Die.D12 };

        // WHOSE POOL THE TRAY THROWS WHEN NOBODY HAS ASKED FOR ONE.
        //
        // The same roster Main.cs names, and named in exactly one place here, so swapping in a
        // data-backed source is this line - the whole reason nothing below reaches past
        // IArchetypeSource to build a fighter. Until B4 the labels lived in TrayResolution as
        // three constants with a comment promising this swap.
        //
        // The tray still throws whatever SIZES the scene was saved with: the pool says which
        // trait each die is, the felt says how many sides it has, and the D key changes the
        // second without touching the first.
        private readonly IArchetypeSource _archetypes = Game.Campaigns.Library.Load(quiet: true);

        // what is on the felt right now - each die with the key of the trait it came from
        private Pool _pool;

        // the traits the tray labels its own dice with, in throw order
        private string[] _labels;

        // the last throw's answer, for whoever asked for it. C# rather than a Godot signal
        // because a TrayThrow is not a Variant and must not become one - the board wants the
        // PoolResult, not a dictionary of numbers
        public event Action<TrayThrow> Resolved;

        private readonly List<DieBody> _dice = new();
        private readonly List<Node3D> _throwPoints = new();

        // THE DICE ACTUALLY IN THIS THROW - a prefix of _dice, and the whole of SEAMS.md section 8.
        //
        // Pool size 3 was assumed in five places plus the scene, and it stopped being true the
        // moment a fight asked the tray to throw one die on its own: a Nerve-bought Heart die
        // makes a pool of four (C4), an untrained attempt makes one of two, and an exploding
        // Impact die is picked up and thrown ALONE (C2). None of those is a special case - they
        // are all "throw this pool", and the tray sits out whatever it does not need.
        //
        // A prefix rather than a chosen subset, because the pool and the throw points pair by
        // index and always have. The dice that are sitting out are frozen, hidden and off every
        // collision layer, so they cannot be knocked into by the ones in play and cannot be read
        // by mistake - a benched die still reports its last settled face, which is exactly the
        // sort of thing that would resolve silently and wrongly.
        private readonly List<DieBody> _active = new();

        // what each die's layers were before it was benched, so putting it back is exact rather
        // than a guess at what the scene said
        private readonly Dictionary<DieBody, (uint Layer, uint Mask)> _benched = new();

        // each die's voice, same order, null where a die has none
        // found by type rather than node name, so a renamed audio node keeps its voice
        // and a deleted one is silent rather than a crash
        private readonly List<DieAudio> _voices = new();

        // the sizes the scene was saved with, to come back to at the end of the tour
        private Die[] _authored;

        // where in ShapeTour we are, or -1 for the authored pool
        private int _tour = -1;

        private TrayResolution _resolution;

        private TrayMarks _marks;

        // the companion's cue, before there is a companion - which bark a Snag would have fired
        // and how often the felt really asks for one
        private SnagCue _cue;

        // held as the interface so this file cannot start reaching for TranslationServer
        private readonly ILocalizer _text = new GodotLocalizer();

        // the fairness sweep has the dice and this is not a tray any more - see _Ready
        private bool _sweeping;

        // the last throw, so switching locale can reprint it rather than roll again
        private TrayThrow _lastThrow;

        // WHAT IS LYING ON THE FELT RIGHT NOW: which trait threw each die, how big it is, and what
        // it is showing. Empty before the first throw of a fight.
        //
        // For a save (CONTENT_PIPELINE.md P6), and it is the whole of what a save needs to know
        // about the tray - the faces, and nothing about what they mean. What a throw MEANS is read
        // back with `TrayThrow.Read`, which is the same arithmetic that read it the first time,
        // so a reloaded fight cannot disagree with the one that was saved (SEAMS.md section 9)
        public IReadOnlyList<TraySlot> Felt =>
            _lastThrow?.Slots ?? (IReadOnlyList<TraySlot>)System.Array.Empty<TraySlot>();

        // and what it added up to, for a check that wants to hold a reloaded reading against the
        // one the table made. Null before the first throw
        public TrayThrow LastThrow => _lastThrow;

        // also the guard that stops one throw resolving twice
        // three dice settling means three chances to try
        private bool _awaitingSettle;

        // SOMEBODY ELSE'S QUESTION IS ON THE FELT: the tray threw a pool it was handed and the
        // answer has not been read yet. True between Throw(pool) and Resolved.
        //
        // The tray's own keys - space, D, T - re-throw, and until Phase C that was harmless
        // because nothing was waiting on an answer. It is not harmless now: pressing space with a
        // door check or a swing in the air throws the same pool again and the asker reads THAT,
        // which is a free re-roll for anyone who knows about the key. The dice are the game
        // (CORE_RULES.md pillar 1), so a re-roll has to cost a Nerve (C4), never a keystroke
        private bool _answering;

        // and readable from outside: whoever is about to ask the tray a question has to know the
        // felt is not already holding somebody else's
        public bool IsAnswering => _answering;

        public override void _Ready()
        {
            _dice.AddRange(GetNode(DiceRootPath).GetChildren().OfType<DieBody>());
            _throwPoints.AddRange(GetNode(ThrowPointsRootPath).GetChildren().OfType<Node3D>());

            // gathered before the skin is applied, because applying it hands each of them
            // a voice built from the tray they are about to land in
            _voices.AddRange(_dice.Select(d => d.GetChildren().OfType<DieAudio>().FirstOrDefault()));

            if (_dice.Count != _throwPoints.Count)
                GD.PushError($"dice tray: {_dice.Count} dice but {_throwPoints.Count} throw points - they pair by index");

            bool sweeping = DiceFairness.RequestedFrom(
                OS.GetCmdlineUserArgs(),
                out int throws, out int poolSize, out int pools, out Die shape, out string skinName);

            // before anything is thrown, because the skin sets the friction and bounce
            // a sweep that measured the scene's authored physics and reported a skin's name
            // would be worse than no sweep at all
            if (skinName != null) Skin = TraySkin.Load(skinName) ?? Skin;
            ApplySkin();

            // before anything is thrown, and before the sweep branch below, because a die that
            // has not been told where the tray is measures its escape from the world origin
            _bounds = Measure();
            foreach (DieBody die in _dice) Bind(die);

            GD.Print($"tray    {_bounds}");

            // the sweep runs in this scene, so the thing measured is the thing played
            // handed over BEFORE any wiring below: the sweep clones dice, and Duplicate()
            // copies signal connections along with them
            if (sweeping)
            {
                // AND THE TRAY STOPS BEING A TRAY. Everything below this branch is what makes it
                // one - the marks, the pool it reads a throw against, the handler that reads the
                // felt when the dice stop - and none of it is built, because the sweep owns the
                // dice from here on and measures them itself. `_sweeping` is what stops the
                // leftovers of the other half firing: `_Process` polls `TryResolve` for a die
                // knocked loose after its flight tracking was disarmed, and during a sweep that
                // poll reached a `TrayMarks` that was never made
                _sweeping = true;

                AddChild(new DiceFairness
                {
                    SceneDice = _dice,
                    ThrowPoints = _throwPoints,
                    Throws = throws,
                    PoolSize = poolSize,
                    Pools = pools,
                    Shape = shape,
                    TrayName = SkinName,
                });
                return;
            }

            // the hero's own check pool - attribute, skill, gear - which is what the board
            // throws too. the tray is not a different game when it is played on its own
            _labels = _archetypes.Create(EngineIds.Barbarian)
                                 .BuildPool(Attr.Might, Skill.Blades).Dice
                                 .Select(d => d.LabelKey).ToArray();

            // FEWER DICE THAN A POOL NEEDS is a scene that cannot answer the question. More is
            // fine and is now the shipped case: the tray carries four so a Nerve can put the Heart
            // die on the felt (CORE_RULES.md section 7), and sits the fourth out for every pool
            // that does not want it
            if (_dice.Count < _labels.Length)
                GD.PushError($"dice tray: the hero's pool is {_labels.Length} dice and the scene has " +
                             $"{_dice.Count} - add another die and throw point");

            _authored = _dice.Select(d => d.Size).ToArray();

            // the hero's own pool decides how many are on the felt to begin with, not how many the
            // scene happens to hold
            Seat(_labels.Length);

            Rebuild();

            AddChild(_marks = new TrayMarks { Name = "Marks", Text = _text, Bounds = _bounds });

            // one handler for all three - each die reports for itself and the handler asks
            // whether the throw as a whole is over, so there is no per-die state to keep in sync
            foreach (DieBody die in _dice) die.Settled += _ => TryResolve();

            // developer diagnostic, not player-facing text
            GD.Print($"dice tray M9 - space throws, {(char)KeyToCycle} changes the shapes, " +
                     $"{(char)KeyToSwitchTray} changes the tray, {(char)KeyToSwitchLocale} switches language");
            GD.Print($"tray    {SkinName}");

            ThrowAll();
        }

        const Key KeyToCycle = Key.D;

        const Key KeyToSwitchLocale = Key.L;

        const Key KeyToSwitchTray = Key.T;

        private StaticBody3D _floorBody;
        private StaticBody3D _wallsBody;

        // how big this tray is, read off the scene once. everything that needs to know - escape
        // detection, label clamping, where the rings lie - is handed it from here
        private TrayBounds _bounds = TrayBounds.Shipped;

        // the scene is the one description of how big the tray is. four constants in three files
        // said so too until F2 and nothing derived them from it, so resizing the floor left a die
        // reported as outside the tray while visibly on the felt - SEAMS.md 5
        //
        // measured in THIS node's space, so the tray can be parented anywhere and still knows its
        // own shape. the walls contribute only their thickness: they lean outward a few degrees,
        // which makes their inner faces a function of height, and the felt a label may use is the
        // floor they stand on rather than the gap between them at any particular altitude
        private TrayBounds Measure()
        {
            _floorBody ??= GetNodeOrNull<StaticBody3D>(TrayFloorPath);
            _wallsBody ??= GetNodeOrNull<StaticBody3D>(TrayWallsPath);

            (CollisionShape3D shape, BoxShape3D box) = FirstBox(_floorBody);

            if (box == null)
            {
                GD.PushError($"dice tray: no box-shaped floor under '{TrayFloorPath}' to measure - " +
                             $"falling back to the authored tray, {TrayBounds.Shipped}");
                return TrayBounds.Shipped;
            }

            // the top of the floor box, not its centre - dice rest on the felt, not inside it
            float feltY = ToLocal(shape.GlobalPosition).Y + box.Size.Y * 0.5f;

            float thickness = WallThickness();

            var measured = new TrayBounds(box.Size.X * 0.5f, box.Size.Z * 0.5f, feltY, thickness);

            if (measured.IsUsable) return measured;

            GD.PushError($"dice tray: measured a tray with no felt inside it ({measured}) - " +
                         $"falling back to the authored tray, {TrayBounds.Shipped}");
            return TrayBounds.Shipped;
        }

        // the thinnest horizontal dimension across the wall boxes
        // a wall is a slab: long one way, tall another, thin the third, and the thin one is the
        // only one that eats into the felt. asking every wall and taking the smallest means an
        // uneven tray reports the wall a label is most likely to slide under
        private float WallThickness()
        {
            float thinnest = float.MaxValue;

            foreach (CollisionShape3D shape in Shapes(_wallsBody))
                if (shape.Shape is BoxShape3D box)
                    thinnest = Mathf.Min(thinnest, Mathf.Min(box.Size.X, box.Size.Z));

            if (thinnest < float.MaxValue) return thinnest;

            GD.PushError($"dice tray: no box-shaped walls under '{TrayWallsPath}' to measure - " +
                         "taking the authored thickness, so a long name may reach the woodwork");

            return TrayBounds.Shipped.WallThickness;
        }

        private static (CollisionShape3D Shape, BoxShape3D Box) FirstBox(Node body)
        {
            foreach (CollisionShape3D shape in Shapes(body))
                if (shape.Shape is BoxShape3D box) return (shape, box);

            return (null, null);
        }

        // walks rather than naming anything, the same way Meshes does and for the same reason:
        // a tray with more walls, or with its shapes nested differently, still measures
        private static IEnumerable<CollisionShape3D> Shapes(Node node)
        {
            if (node == null) yield break;

            foreach (Node child in node.GetChildren())
            {
                if (child is CollisionShape3D shape) yield return shape;

                foreach (CollisionShape3D deeper in Shapes(child)) yield return deeper;
            }
        }

        // one die told where it is being thrown. the tray owns the numbers; the die owns the test
        private void Bind(DieBody die)
        {
            die.TraySpace = this;
            die.LostBelowY = _bounds.LostBelowY;
            die.LostRadius = _bounds.LostRadius;
        }

        // the skin's bare file name - wood, gamblers - or "none"
        public string SkinName =>
            Skin?.ResourcePath is { Length: > 0 } path ? path.GetFile().GetBaseName() : "none";

        // idempotent, and it has to be - this is what changing trays at runtime calls
        // physics and look both come off the same TraySurface, so a tray that looks like
        // felt and bounces like a plank is impossible
        private void ApplySkin()
        {
            _floorBody ??= GetNodeOrNull<StaticBody3D>(TrayFloorPath);
            _wallsBody ??= GetNodeOrNull<StaticBody3D>(TrayWallsPath);

            if (_floorBody == null || _wallsBody == null)
            {
                GD.PushError($"dice tray: the tray needs two StaticBody3D at '{TrayFloorPath}' and " +
                             $"'{TrayWallsPath}' - physics materials belong to bodies, not to shapes");
                return;
            }

            if (Skin == null)
            {
                GD.PushError("dice tray: no skin - the tray will be untextured and will bounce like Godot's default");
                return;
            }

            Dress(_floorBody, Skin.Floor, "floor");
            Dress(_wallsBody, Skin.Walls, "walls");

            // one voice for the whole tray - it holds nothing per-die and its pools are
            // cached by folder, so three dice is three references
            var voice = new SurfaceVoice(Skin);
            foreach (DieAudio audio in _voices) if (audio != null) audio.Voice = voice;
        }

        private static void Dress(StaticBody3D body, TraySurface surface, string which)
        {
            if (surface == null)
            {
                GD.PushError($"dice tray: the skin has no {which} surface");
                return;
            }

            body.PhysicsMaterialOverride = surface.Physics;

            foreach (MeshInstance3D mesh in Meshes(body)) mesh.MaterialOverride = surface.Material;
        }

        // the meshes hang off the collision shapes rather than off the body
        // walks rather than naming anything, so a tray with more walls still works
        private static IEnumerable<MeshInstance3D> Meshes(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh) yield return mesh;

                foreach (MeshInstance3D deeper in Meshes(child)) yield return deeper;
            }
        }

        // checking a skin means throwing on it and looking, which is only bearable at one key
        // also proves ApplySkin really is idempotent
        private void CycleSkin()
        {
            var skins = TraySkin.All().ToList();

            if (skins.Count == 0)
            {
                GD.Print("");
                GD.Print($"tray    no skins in {TraySkin.Folder}");
                return;
            }

            int next = (skins.IndexOf(SkinName) + 1) % skins.Count;

            Skin = TraySkin.Load(skins[next]) ?? Skin;
            ApplySkin();

            GD.Print("");
            GD.Print($"tray    {SkinName} - floor friction {Skin?.Floor?.Physics?.Friction:0.00} " +
                     $"bounce {Skin?.Floor?.Physics?.Bounce:0.00}, " +
                     $"walls friction {Skin?.Walls?.Physics?.Friction:0.00} " +
                     $"bounce {Skin?.Walls?.Physics?.Bounce:0.00}");
            GD.Print($"        every skin needs its own sweep: .\\check-fairness.ps1 -Tray {SkinName} -Dice 3");

            ThrowAll();
        }

        // HOW MANY DICE ARE IN THIS THROW. The first `count` of them play; the rest are taken off
        // the felt - frozen, hidden, and off every collision layer so nothing in play can knock
        // into them and nothing can read them by mistake.
        //
        // The reading is the part that matters. A benched die still answers ReadFace with
        // whatever it settled on last time, so a tray that merely IGNORED the surplus would be one
        // stale number away from resolving a pool nobody threw - and it would look completely
        // fine, because the die really is showing that face.
        //
        // Idempotent, like ApplySkin, because this is called on every throw.
        private void Seat(int count)
        {
            count = Mathf.Clamp(count, 0, _dice.Count);

            _active.Clear();

            for (int i = 0; i < _dice.Count; i++)
            {
                DieBody die = _dice[i];

                if (i < count)
                {
                    _active.Add(die);
                    Unbench(die);
                }
                else
                {
                    Bench(die);
                }
            }
        }

        // off the felt. its layers are remembered rather than assumed, so putting it back is what
        // the scene said and not what this file guessed
        private void Bench(DieBody die)
        {
            if (_benched.ContainsKey(die)) return;

            _benched[die] = (die.CollisionLayer, die.CollisionMask);

            die.CollisionLayer = 0;
            die.CollisionMask = 0;
            die.Freeze = true;
            die.Visible = false;
        }

        private void Unbench(DieBody die)
        {
            if (!_benched.TryGetValue(die, out (uint Layer, uint Mask) was)) return;

            die.CollisionLayer = was.Layer;
            die.CollisionMask = was.Mask;
            die.Freeze = false;
            die.Visible = true;

            _benched.Remove(die);
        }

        // has to run every time the dice change
        // a d8 in the tray resolving as a d6 is the quiet lie the face table exists to prevent
        private void Rebuild()
        {
            var sizes = _active.Select(d => d.Size).ToList();

            // the pool the board handed over keeps its own labels; otherwise the felt is
            // relabelled with the tray's hero, one trait per throw point
            _pool = Relabelled(sizes);
            _resolution = new TrayResolution(_pool);

            // the snag tally restarts with the pool, and has to: bigger dice snag less, so a count
            // carried across a change of shapes is measuring a table that no longer exists
            if (_cue == null) _cue = new SnagCue(sizes);
            else _cue.Reset(sizes);
        }

        // the traits currently on the felt, wearing whatever sizes are currently on the felt
        //
        // the two are tracked apart because they change apart: a pool arrives from the board with
        // both, and the D key changes only the sizes. a d12 labelled Might is still Might
        private Pool Relabelled(IReadOnlyList<Die> sizes)
        {
            var pool = new Pool();

            for (int i = 0; i < sizes.Count; i++)
                pool.Add(i < _labels.Length ? _labels[i] : _labels[_labels.Length - 1], sizes[i]);

            return pool;
        }

        // THROW THIS. the board's whole side of B4: build a pool out of a hero and hand it over.
        //
        // The tray takes the sizes AND the labels from it, so a check thrown from the board is
        // the hero's own dice rather than whatever the scene was saved with - and the marks name
        // the traits that were actually used. Every bit of M0-M9 does the rest; there is no
        // second throwing path and there must never be one.
        //
        // The answer comes back through Resolved, once the last die has stopped.
        public void Throw(Pool pool)
        {
            if (pool == null || pool.Count == 0)
            {
                GD.PushError("dice tray: asked to throw a pool with no dice in it - nothing thrown");
                return;
            }

            if (pool.Count > _dice.Count)
            {
                // MORE DICE THAN THE TRAY HAS. A tray can sit a die out (Seat below) and cannot
                // conjure one - the scene owns how many solids exist, and adding one is a scene
                // edit. Named rather than silently truncated, because a pool thrown short is a
                // check answered by the wrong handful
                GD.PushError($"dice tray: asked to throw {pool.Count} dice and the tray has " +
                             $"{_dice.Count} - add another die and throw point to the scene");
                return;
            }

            _labels = pool.Dice.Select(d => d.LabelKey).ToArray();
            _answering = true;

            Seat(pool.Count);

            for (int i = 0; i < _active.Count; i++) _active[i].Size = pool.Dice[i].Die;

            // the tour is over: these are somebody's real dice now, and D goes back to the top
            _tour = -1;

            Rebuild();
            ThrowAll();
        }

        // ONE DIE, PICKED UP AND THROWN AGAIN, WITH THE REST LEFT LYING WHERE THEY ARE.
        //
        // A Nerve re-throws any one die in a pool and you take the new result (CORE_RULES.md
        // section 7), and at a table that is exactly what it looks like: you pick that one die
        // back up and roll it, and the other two sit there. So this launches one and reads all of
        // them - the answer that comes back through `Resolved` is the whole pool re-resolved,
        // because the best two and the Impact die are properties of the handful and not of the die
        // that moved.
        //
        // Nothing here decides whether a re-throw is allowed or what it costs. That is a Nerve,
        // and Nerve is the fight's business.
        public bool Rethrow(int slot)
        {
            if (_resolution == null || slot < 0 || slot >= _active.Count)
            {
                GD.PushError($"dice tray: asked to re-throw die {slot} of {_active.Count} on the felt");
                return false;
            }

            _answering = true;
            _awaitingSettle = true;
            _pending.Clear();

            // the marks go the moment the die does, for the same reason ThrowAll clears them:
            // there must never be a frame with a ring drawn round a die that is in the air
            _marks?.Clear();

            Transform3D from = _throwPoints[slot].GlobalTransform;

            _voices[slot]?.Rattle(from.Origin);
            _pending.Add((_active[slot], from, RattleLeadTicks));

            GD.Print("");
            GD.Print($"reroll  {_active[slot].Name} {_active[slot].Size.Label()} goes back in the hand");

            return true;
        }

        // ---- picking a die up (COMBAT_LOOP.md C4) ----

        // WHICH DIE THE PLAYER JUST TOUCHED, in throw order. Armed by whoever is offering a
        // re-throw and off the rest of the time, so a stray click on the felt during a foe's turn
        // is a stray click on the felt
        public bool Picking { get; set; }

        public event Action<int> Picked;

        // met with the die's own collision body rather than with a plane, because a die is a solid
        // lying at an angle and the whole point is to hit THAT one. The board does the opposite
        // and for the opposite reason - see Board.CellUnder
        private int DieUnder(Vector2 at)
        {
            Camera3D camera = GetViewport()?.GetCamera3D();

            if (camera == null) return -1;

            var query = PhysicsRayQueryParameters3D.Create(
                camera.ProjectRayOrigin(at),
                camera.ProjectRayOrigin(at) + camera.ProjectRayNormal(at) * PickRange);

            query.CollideWithAreas = false;

            Godot.Collections.Dictionary hit = GetWorld3D()?.DirectSpaceState?.IntersectRay(query);

            if (hit == null || hit.Count == 0) return -1;

            var body = hit["collider"].As<GodotObject>() as DieBody;

            return body == null ? -1 : _active.IndexOf(body);
        }

        // far enough to cross a table from any seat, short enough to be a ray and not a search
        private const float PickRange = 8f;

        // queue every die, a couple of physics ticks apart
        // all three on the identical frame collide before reaching the felt,
        // which is where most cocked landings came from
        private void ThrowAll()
        {
            _awaitingSettle = true;
            _pending.Clear();

            // the moment the dice go up, last throw's answer stops being true
            // clearing here rather than on resolve means there is never a frame where a ring
            // is drawn around a die that is already in the air
            _marks?.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                Transform3D from = _throwPoints[i].GlobalTransform;

                // the shake is one handful making one noise, so it is not staggered
                // only the release is
                _voices[i]?.Rattle(from.Origin);

                _pending.Add((_active[i], from, RattleLeadTicks + i * LaunchStaggerTicks));
            }
        }

        public override void _PhysicsProcess(double delta)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                (DieBody die, Transform3D from, int delay) = _pending[i];

                if (delay > 0)
                {
                    _pending[i] = (die, from, delay - 1);
                    continue;
                }

                die.Throw(from);
                _pending.RemoveAt(i);
            }
        }

        public override void _Process(double delta)
        {
            // belt and braces alongside the Settled handler
            // a die knocked loose by another die's cocked re-throw starts moving again without
            // re-arming its flight tracking, so no further signal is coming
            // polling here catches the tray coming to rest
            if (_awaitingSettle) TryResolve();
        }

        // waits on IsAtRest as well as IsSettled, so a die still rolling after a bump
        // can never be read mid-tumble
        private void TryResolve()
        {
            // the sweep owns the dice and reads them itself - see _Ready
            if (_sweeping) return;

            if (!_awaitingSettle) return;

            // a die still waiting in the stagger queue has not been thrown, so it reports
            // settled on last throw's value - resolving now would read stale faces
            if (_pending.Count > 0) return;

            if (!_active.All(d => d.IsSettled && d.IsAtRest)) return;

            _awaitingSettle = false;

            // read live rather than trusting each die's latched SettledValue
            // what the rules get must be what is on the felt at this instant, including a die
            // that was nudged after it first came to rest
            var faces = _active.Select(d => d.ReadFace()).ToList();
            List<int> values = faces.Select(f => f.Value).ToList();

            // every GD.Print here is developer diagnostic and exempt from localization
            GD.Print("");
            GD.Print("tray   " + string.Join(", ", _active.Select(
                (d, i) => $"{d.Name} {d.Size.Label()} {faces[i].Value} ({faces[i].Alignment:0.00})")));

            _lastThrow = _resolution.Resolve(values);

            // AND IF THE POOL AND THE FELT HAVE COME APART, SAY SO. `TrayThrow` used to throw here
            // and stopped in P6, because the same class has to read a SAVED throw and a save is a
            // thing to report and carry on from (SEAMS.md section 9). At the live table it is
            // still a bug and still gets shouted about - the difference is that the fight
            // continues with unmarked dice rather than ending mid-swing
            if (!_lastThrow.Agrees)
                GD.PushError("tray: " + _lastThrow.Disagreement +
                             " The dice are left unmarked rather than marked wrongly.");

            // dice and slots are both in throw order, which is why the marks can be handed
            // over without the view working anything out for itself
            _marks.Show(_lastThrow, _active);

            foreach (string line in _lastThrow.DebugLines(TargetDifficulty, _text))
                GD.Print(line);

            // the key a companion would have spoken, not the words - there are no words, and
            // there must not be: dialogue.* is campaign content and game/locale/ never grows one
            string bark = _cue.Watch(_lastThrow);

            if (bark != null) GD.Print($"cue    {bark} - nothing says it yet");

            // every throw, not only the ones that snag, so the running count is exact at the
            // moment you stop rather than as of the last time it fired
            GD.Print($"snags  {_cue.Tally}");

            // LAST, after everything the tray does for itself. whoever asked for this throw gets
            // the answer only once the felt is finished with it, so a listener can read the marks
            // and the dice as they are rather than as they are about to be
            _answering = false;
            Resolved?.Invoke(_lastThrow);
        }

        // the tray's own affordances are refused while somebody is waiting on an answer. not an
        // error and nothing is printed at the player: a key that does nothing for a second and a
        // half is the felt saying the question already on it has not been answered
        private bool Busy(string affordance)
        {
            if (!_answering) return false;

            GD.Print($"tray    {affordance} ignored - a check is in the air and the answer is not " +
                     "the tray's to re-throw");
            return true;
        }

        // switch language and reprint the throw already on the table
        // the pseudolocale mangles every string that came through the translation system
        // and leaves everything else alone, so anything still in plain English is hardcoded
        // it pads by 30% too, which shows what will overflow before a translator does
        private void SwitchLocale()
        {
            TranslationServer.PseudolocalizationEnabled = !TranslationServer.PseudolocalizationEnabled;

            bool pseudo = TranslationServer.PseudolocalizationEnabled;

            // every mark listens for the notification itself, this makes certain
            // a label that stayed English would look exactly like a label that is hardcoded
            _marks?.Retranslate();

            GD.Print("");
            GD.Print($"locale  {TranslationServer.GetLocale()}{(pseudo ? " + pseudolocale" : "")} - " +
                     (pseudo
                         ? "anything below still in plain English is hardcoded"
                         : "back to plain English"));

            // the same throw, not a new one - two languages over one set of numbers
            if (_lastThrow != null)
                foreach (string line in _lastThrow.DebugLines(TargetDifficulty, _text))
                    GD.Print(line);
        }

        // step the whole tray to the next solid, then round to the authored pool
        // checking a shape means twenty throws of it against your own eyes
        private void CycleShape()
        {
            _tour = _tour + 1 >= ShapeTour.Length ? -1 : _tour + 1;

            // the shape tour is about the tray and not about anybody's pool, so it puts every die
            // back on the felt first - otherwise D after a one-die throw tours one die
            Seat(_dice.Count);

            for (int i = 0; i < _active.Count; i++)
                _active[i].Size = _tour < 0 ? _authored[i] : ShapeTour[_tour];

            Rebuild();

            GD.Print("");
            GD.Print(_tour < 0
                ? "shapes  back to the pool the scene was saved with: " +
                  string.Join(" + ", _authored.Select(d => d.Label()))
                : $"shapes  three {ShapeTour[_tour].Label()}s");

            // says why the count below just went back to zero, and what to expect of the new one
            GD.Print($"        snags {_cue.Expected:0.0%} on this pool - the tally starts again");

            ThrowAll();
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            // A DIE, PICKED UP. Offered before the board sees the click, which is free: Godot
            // delivers unhandled input up the tree from the bottom, and the tray is a later sibling
            // than the board. A click that misses every die falls through untouched
            if (Picking && @event.IsActionPressed("place_piece") && @event is InputEventMouse mouse)
            {
                int slot = DieUnder(mouse.Position);

                if (slot >= 0)
                {
                    Picked?.Invoke(slot);
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }

            if (@event.IsActionPressed("throw_dice"))
            {
                if (!Busy("space")) ThrowAll();
                GetViewport().SetInputAsHandled();
                return;
            }

            // not input actions, because these are development affordances rather than
            // controls the game has - neither needs to survive past the tray
            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            if (key.Keycode == KeyToCycle)
            {
                if (!Busy((char)KeyToCycle + "")) CycleShape();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == KeyToSwitchLocale)
            {
                // reads the throw already on the felt in another language and throws nothing, so
                // it is safe with a check in the air - and is the one that most needs to be
                SwitchLocale();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == KeyToSwitchTray)
            {
                if (!Busy((char)KeyToSwitchTray + "")) CycleSkin();
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
