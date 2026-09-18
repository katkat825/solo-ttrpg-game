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
    // the tray scene's wiring: kick the dice, and when the last stops hand the felt to the rules; owns no rules itself
    public partial class DiceTray : Node3D
    {
        // children are read in scene order and paired with the throw points by index
        [Export] public NodePath DiceRootPath { get; set; } = "Dice";

        [Export] public NodePath ThrowPointsRootPath { get; set; } = "ThrowPoints";

        // floor and walls are two bodies because physics_material_override belongs to a StaticBody3D, not a shape - one body can't be felt and wood
        [Export] public NodePath TrayFloorPath { get; set; } = "TrayFloor";

        [Export] public NodePath TrayWallsPath { get; set; } = "TrayWalls";

        // dresses both bodies and tells the dice what they are landing on
        [Export] public TraySkin Skin { get; set; }

        // physics ticks between each die leaving the hand: enough that they aren't in the same air, under the threshold where the ear hears two impacts
        [Export] public int LaunchStaggerTicks { get; set; } = 3;

        // physics ticks between the shake and the release: deliberate latency, nobody drops dice, they shake then let go
        [Export] public int RattleLeadTicks { get; set; } = 14;

        private readonly List<(DieBody Die, Transform3D From, int Delay)> _pending = new();

        [Export] public int TargetDifficulty { get; set; } = Difficulty.Standard;

        static readonly Die[] ShapeTour = { Die.D4, Die.D6, Die.D8, Die.D10, Die.D12 };

        // the default pool when nobody hands one in; the pool names the traits, the scene's dice give the sizes, and D changes sizes only
        private readonly IArchetypeSource _archetypes = Game.Campaigns.Library.Load(quiet: true);

        private Pool _pool;

        private string[] _labels;

        // a plain C# event, not a Godot signal: a TrayThrow is not a Variant and must not become one
        public event Action<TrayThrow> Resolved;

        private readonly List<DieBody> _dice = new();
        private readonly List<Node3D> _throwPoints = new();

        // the dice actually in this throw, a prefix of _dice: the pool decides how many play and the tray sits out the rest
        // benched dice are frozen, hidden and off every collision layer, so a stale last face can't be knocked into play or read by mistake
        private readonly List<DieBody> _active = new();

        // each die's layers before benching, so Unbench restores exactly what the scene set
        private readonly Dictionary<DieBody, (uint Layer, uint Mask)> _benched = new();

        // each die's voice, same order, null where none; found by type not name, so a rename keeps it and a deletion is silence, not a crash
        private readonly List<DieAudio> _voices = new();

        private Die[] _authored;

        // where in ShapeTour we are, or -1 for the authored pool
        private int _tour = -1;

        private TrayResolution _resolution;

        private TrayMarks _marks;

        // the companion's cue placeholder: which bark a Snag would fire, and how often the felt asks
        private SnagCue _cue;

        // optional. The companion whose bark bank the Snag cue draws on (W2); left unset, the cue
        // falls back to M9's placeholder keys and the tray behaves exactly as it did before W
        [Export] public NodePath CompanionPath { get; set; }

        // held as the interface so this file cannot start reaching for TranslationServer
        private readonly ILocalizer _text = new GodotLocalizer();

        // the fairness sweep has the dice and this is not a tray any more - see _Ready
        private bool _sweeping;

        // the last throw, so switching locale can reprint it rather than roll again
        private TrayThrow _lastThrow;

        // what's on the felt now (trait, size, face), for a save; the faces only - the meaning is read back with TrayThrow.Read, the same arithmetic, so a reload can't disagree
        public IReadOnlyList<TraySlot> Felt =>
            _lastThrow?.Slots ?? (IReadOnlyList<TraySlot>)System.Array.Empty<TraySlot>();

        // the last throw, for a check comparing a reloaded reading against the table's; null before the first throw
        public TrayThrow LastThrow => _lastThrow;

        // guards against one throw resolving twice: three dice settling is three chances to fire
        private bool _awaitingSettle;

        // true between Throw(pool) and Resolved: a handed-in question is on the felt, and the tray's own re-throw keys must not answer it - a re-roll costs a Nerve, not a keystroke
        private bool _answering;

        public bool IsAnswering => _answering;

        public override void _Ready()
        {
            _dice.AddRange(GetNode(DiceRootPath).GetChildren().OfType<DieBody>());
            _throwPoints.AddRange(GetNode(ThrowPointsRootPath).GetChildren().OfType<Node3D>());

            // gathered before ApplySkin, which hands each die a voice built from the tray it lands in
            _voices.AddRange(_dice.Select(d => d.GetChildren().OfType<DieAudio>().FirstOrDefault()));

            if (_dice.Count != _throwPoints.Count)
                GD.PushError($"dice tray: {_dice.Count} dice but {_throwPoints.Count} throw points - they pair by index");

            bool sweeping = DiceFairness.RequestedFrom(
                OS.GetCmdlineUserArgs(),
                out int throws, out int poolSize, out int pools, out Die shape, out string skinName);

            // apply the skin before anything is thrown: it sets the friction and bounce the sweep measures
            if (skinName != null) Skin = TraySkin.Load(skinName) ?? Skin;
            ApplySkin();

            // measure and bind before the sweep branch: a die not told where the tray is measures its escape from the world origin
            _bounds = Measure();
            foreach (DieBody die in _dice) Bind(die);

            GD.Print($"tray    {_bounds}");

            // hand the dice to the sweep before wiring below: Duplicate() copies signal connections, so the clones would carry them
            if (sweeping)
            {
                // in a sweep, none of the tray wiring below is built; _sweeping also stops _Process polling TryResolve into a TrayMarks that was never made
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

            // the hero's own check pool, the same one the board throws, so the tray isn't a different game on its own
            _labels = _archetypes.Create(EngineIds.Barbarian)
                                 .BuildPool(Attr.Might, Skill.Blades).Dice
                                 .Select(d => d.LabelKey).ToArray();

            // fewer dice than the pool needs can't answer the check; more is fine - the tray carries a spare for a Nerve's Heart die and sits it out otherwise
            if (_dice.Count < _labels.Length)
                GD.PushError($"dice tray: the hero's pool is {_labels.Length} dice and the scene has " +
                             $"{_dice.Count} - add another die and throw point");

            _authored = _dice.Select(d => d.Size).ToArray();

            Seat(_labels.Length);

            Rebuild();

            AddChild(_marks = new TrayMarks { Name = "Marks", Text = _text, Bounds = _bounds });

            // one handler for all dice: each reports for itself and the handler asks whether the whole throw is done, so there's no per-die state
            foreach (DieBody die in _dice) die.Settled += _ => TryResolve();

            // THE COCKED DIE IS THE COMPANION'S JOB. The recovery itself is unchanged and is still
            // IDieRecovery's - nudge it flat, keep the number you actually threw, rethrow only when
            // nudging has run out. What is new is who the player sees doing it: the creature leans
            // over and noses the die down, with something to say about it, instead of the die
            // appearing to right itself.
            foreach (DieBody die in _dice)
                die.Nudged += (_, attempt) => Nudged(attempt);

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

        // the tray's size, read off the scene once and handed to everything that needs it
        private TrayBounds _bounds = TrayBounds.Shipped;

        // measured off the scene, in this node's space, so the tray can be parented anywhere and still know its own shape
        // walls contribute only their thickness; they lean outward, so the usable felt is the floor they stand on, not the gap between them
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

        // the thinnest horizontal wall dimension: the thin side is the one that eats into the felt, and the smallest is the one a label is likeliest to slide under
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

        // walks the tree rather than naming nodes, so a tray with more walls or nested shapes still measures
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

        public string SkinName =>
            Skin?.ResourcePath is { Length: > 0 } path ? path.GetFile().GetBaseName() : "none";

        // idempotent, since changing trays at runtime calls it; physics and look come off one TraySurface, so felt-look-plank-bounce is impossible
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

            // one voice for the whole tray: it holds nothing per-die, so three dice share three references
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

        // meshes hang off the collision shapes, not the body; walks rather than naming so more walls still work
        private static IEnumerable<MeshInstance3D> Meshes(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is MeshInstance3D mesh) yield return mesh;

                foreach (MeshInstance3D deeper in Meshes(child)) yield return deeper;
            }
        }

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

        // how many dice play; the rest are benched (frozen, hidden, off collision layers) so a stale last face can't resolve a pool nobody threw
        // idempotent, called on every throw
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

        // runs whenever the dice change: a d8 resolving as a d6 is the quiet lie this prevents
        private void Rebuild()
        {
            var sizes = _active.Select(d => d.Size).ToList();

            // a board-handed pool keeps its labels; otherwise the felt is relabelled with the tray's hero, one trait per point
            _pool = Relabelled(sizes);
            _resolution = new TrayResolution(_pool);

            // restart the snag tally with the pool: bigger dice snag less, so a count across a change of shapes measures a table that's gone
            if (_cue == null) _cue = new SnagCue(sizes, bank: Bank());
            else _cue.Reset(sizes);
        }

        // whose barks the felt draws on. Read once, off the shelf, and null where no companion is
        // installed - a tray with nobody at it still counts its Snags and still prints the key
        private Content.Dialogue.BarkBank Bank()
        {
            Game.Companion.Companion friend = Friend();

            return friend == null || friend.Speaker.Length == 0
                ? null
                : Game.Campaigns.Library.Load(quiet: true).BarksFor(friend.Speaker);
        }

        private Game.Companion.Companion Friend() =>
            CompanionPath == null || CompanionPath.IsEmpty
                ? null
                : GetNodeOrNull<Game.Companion.Companion>(CompanionPath);

        // only the first nudge of a die. A die that needs two is not twice as interesting, and a
        // creature commenting on every shove reads as a stuck line rather than as a character
        private void Nudged(int attempt)
        {
            if (attempt > 1) return;

            Friend()?.NosesACockedDie();
        }

        // labels and sizes are tracked apart because they change apart: a board pool sets both, but D changes only the sizes
        private Pool Relabelled(IReadOnlyList<Die> sizes)
        {
            var pool = new Pool();

            for (int i = 0; i < sizes.Count; i++)
                pool.Add(i < _labels.Length ? _labels[i] : _labels[_labels.Length - 1], sizes[i]);

            return pool;
        }

        // the one way in: the tray takes both the sizes and the labels from the pool, so a board check throws the hero's own dice; the answer returns through Resolved
        public void Throw(Pool pool)
        {
            if (pool == null || pool.Count == 0)
            {
                GD.PushError("dice tray: asked to throw a pool with no dice in it - nothing thrown");
                return;
            }

            if (pool.Count > _dice.Count)
            {
                // more dice than the tray has: it can't conjure one, and truncating silently would answer the check with the wrong handful
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

        // throw one die again and read the whole pool: best-two and Impact are properties of the handful, not the die that moved
        // doesn't decide whether a re-throw is allowed or what it costs; that's a Nerve, the fight's business
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

            // clear the marks the moment the die goes up: never a frame with a ring round a die in the air
            _marks?.Clear();

            Transform3D from = _throwPoints[slot].GlobalTransform;

            _voices[slot]?.Rattle(from.Origin);
            _pending.Add((_active[slot], from, RattleLeadTicks));

            GD.Print("");
            GD.Print($"reroll  {_active[slot].Name} {_active[slot].Size.Label()} goes back in the hand");

            return true;
        }

        // armed only while a re-throw is on offer, so a stray click on the felt during a foe's turn does nothing
        public bool Picking { get; set; }

        public event Action<int> Picked;

        // raycast against the die's own body, not a plane: the point is to hit that exact solid (the board does the opposite, see Board.CellUnder)
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

        // queue the dice a couple of ticks apart: launching them on one frame makes them collide mid-air, the main source of cocked landings
        private void ThrowAll()
        {
            _awaitingSettle = true;
            _pending.Clear();

            // clear the marks as the dice go up, not on resolve, so no frame has a ring round a die in the air
            _marks?.Clear();

            for (int i = 0; i < _active.Count; i++)
            {
                Transform3D from = _throwPoints[i].GlobalTransform;

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
            // belt-and-braces poll: a die knocked loose by another's re-throw moves without re-arming its flight tracking, so no Settled signal is coming
            if (_awaitingSettle) TryResolve();
        }

        // waits on IsAtRest as well as IsSettled, so a die bumped after settling isn't read mid-tumble
        private void TryResolve()
        {
            // the sweep owns the dice and reads them itself - see _Ready
            if (_sweeping) return;

            if (!_awaitingSettle) return;

            // a die still in the stagger queue hasn't been thrown, so resolving now would read its stale last face
            if (_pending.Count > 0) return;

            if (!_active.All(d => d.IsSettled && d.IsAtRest)) return;

            _awaitingSettle = false;

            // read the faces live, not each die's latched SettledValue, so a die nudged after settling is read as it is now
            var faces = _active.Select(d => d.ReadFace()).ToList();
            List<int> values = faces.Select(f => f.Value).ToList();

            // every GD.Print here is developer diagnostic and exempt from localization
            GD.Print("");
            GD.Print("tray   " + string.Join(", ", _active.Select(
                (d, i) => $"{d.Name} {d.Size.Label()} {faces[i].Value} ({faces[i].Alignment:0.00})")));

            _lastThrow = _resolution.Resolve(values);

            // if the pool and felt disagree, shout but carry on with unmarked dice rather than ending mid-swing
            if (!_lastThrow.Agrees)
                GD.PushError("tray: " + _lastThrow.Disagreement +
                             " The dice are left unmarked rather than marked wrongly.");

            // dice and slots are both in throw order, so the marks hand over without the view working anything out
            _marks.Show(_lastThrow, _active);

            foreach (string line in _lastThrow.DebugLines(TargetDifficulty, _text))
                GD.Print(line);

            // the key a companion would have spoken, not words: dialogue is campaign content and never enters game/locale
            string bark = _cue.Watch(_lastThrow);

            if (bark != null)
                GD.Print(_cue.HasLines
                    ? $"cue    {bark} - {_cue.Speaking} says it"
                    : $"cue    {bark} - nothing says it yet");

            // logged every throw, not only snags, so the running count is exact when you stop
            GD.Print($"snags  {_cue.Tally}");

            // fired last, after the tray finishes with the felt, so a listener reads the marks and dice as they are
            _answering = false;
            Resolved?.Invoke(_lastThrow);
        }

        // the tray's own keys are refused while a handed-in check is unanswered; silent to the player on purpose
        private bool Busy(string affordance)
        {
            if (!_answering) return false;

            GD.Print($"tray    {affordance} ignored - a check is in the air and the answer is not " +
                     "the tray's to re-throw");
            return true;
        }

        // switch language and reprint the current throw; the pseudolocale mangles every translated string and pads 30%, so anything still English is hardcoded and anything that overflows shows now
        private void SwitchLocale()
        {
            TranslationServer.PseudolocalizationEnabled = !TranslationServer.PseudolocalizationEnabled;

            bool pseudo = TranslationServer.PseudolocalizationEnabled;

            // marks already re-read on the notification; this makes certain, since a stuck-English label looks like a hardcoded one
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

        // step every die to the next solid, then back to the authored pool
        private void CycleShape()
        {
            _tour = _tour + 1 >= ShapeTour.Length ? -1 : _tour + 1;

            // the tour is about the tray, so reseat every die first, or D after a one-die throw would tour one die
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
            // offered before the board sees the click: Godot bubbles unhandled input from the bottom and the tray is the later sibling; a miss falls through untouched
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

            // raw keys, not input actions: these are development affordances that needn't survive past the tray
            if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

            if (key.Keycode == KeyToCycle)
            {
                if (!Busy((char)KeyToCycle + "")) CycleShape();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == KeyToSwitchLocale)
            {
                // reads the current throw in another language and throws nothing, so it's safe with a check in the air
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
