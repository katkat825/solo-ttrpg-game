using System.Collections.Generic;
using System.Linq;
using Godot;
using Rules.Dice;
using Rules.Localization;
using Rules.Resolution;

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

    private readonly List<DieBody> _dice = new();
    private readonly List<Node3D> _throwPoints = new();

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

    // the last throw, so switching locale can reprint it rather than roll again
    private TrayThrow _lastThrow;

    // also the guard that stops one throw resolving twice
    // three dice settling means three chances to try
    private bool _awaitingSettle;

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

        // the sweep runs in this scene, so the thing measured is the thing played
        // handed over BEFORE any wiring below: the sweep clones dice, and Duplicate()
        // copies signal connections along with them
        if (sweeping)
        {
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

        if (_dice.Count != TrayResolution.PoolSize)
            GD.PushError($"dice tray: the pool is {TrayResolution.PoolSize} dice but the scene has {_dice.Count}");

        _authored = _dice.Select(d => d.Size).ToArray();
        Rebuild();

        AddChild(_marks = new TrayMarks { Name = "Marks", Text = _text });

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
        var voice = new WoodenDice(Skin);
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

    // has to run every time the dice change
    // a d8 in the tray resolving as a d6 is the quiet lie the face table exists to prevent
    private void Rebuild()
    {
        var sizes = _dice.Select(d => d.Size).ToList();

        _resolution = new TrayResolution(sizes);

        // the snag tally restarts with the pool, and has to: bigger dice snag less, so a count
        // carried across a change of shapes is measuring a table that no longer exists
        if (_cue == null) _cue = new SnagCue(sizes);
        else _cue.Reset(sizes);
    }

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

        for (int i = 0; i < _dice.Count; i++)
        {
            Transform3D from = _throwPoints[i].GlobalTransform;

            // the shake is one handful making one noise, so it is not staggered
            // only the release is
            _voices[i]?.Rattle(from.Origin);

            _pending.Add((_dice[i], from, RattleLeadTicks + i * LaunchStaggerTicks));
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
        if (!_awaitingSettle) return;

        // a die still waiting in the stagger queue has not been thrown, so it reports
        // settled on last throw's value - resolving now would read stale faces
        if (_pending.Count > 0) return;

        if (!_dice.All(d => d.IsSettled && d.IsAtRest)) return;

        _awaitingSettle = false;

        // read live rather than trusting each die's latched SettledValue
        // what the rules get must be what is on the felt at this instant, including a die
        // that was nudged after it first came to rest
        var faces = _dice.Select(d => d.ReadFace()).ToList();
        List<int> values = faces.Select(f => f.Value).ToList();

        // every GD.Print here is developer diagnostic and exempt from localization
        GD.Print("");
        GD.Print("tray   " + string.Join(", ", _dice.Select(
            (d, i) => $"{d.Name} {d.Size.Label()} {faces[i].Value} ({faces[i].Alignment:0.00})")));

        _lastThrow = _resolution.Resolve(values);

        // dice and slots are both in throw order, which is why the marks can be handed
        // over without the view working anything out for itself
        _marks.Show(_lastThrow, _dice);

        foreach (string line in _lastThrow.DebugLines(TargetDifficulty, _text))
            GD.Print(line);

        // the key a companion would have spoken, not the words - there are no words, and
        // there must not be: dialogue.* is campaign content and game/locale/ never grows one
        string bark = _cue.Watch(_lastThrow);

        if (bark != null) GD.Print($"cue    {bark} - nothing says it yet");

        // every throw, not only the ones that snag, so the running count is exact at the
        // moment you stop rather than as of the last time it fired
        GD.Print($"snags  {_cue.Tally}");
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

        for (int i = 0; i < _dice.Count; i++)
            _dice[i].Size = _tour < 0 ? _authored[i] : ShapeTour[_tour];

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
        if (@event.IsActionPressed("throw_dice"))
        {
            ThrowAll();
            GetViewport().SetInputAsHandled();
            return;
        }

        // not input actions, because these are development affordances rather than
        // controls the game has - neither needs to survive past the tray
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;

        if (key.Keycode == KeyToCycle)
        {
            CycleShape();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == KeyToSwitchLocale)
        {
            SwitchLocale();
            GetViewport().SetInputAsHandled();
        }
        else if (key.Keycode == KeyToSwitchTray)
        {
            CycleSkin();
            GetViewport().SetInputAsHandled();
        }
    }
}
