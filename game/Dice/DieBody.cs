using System;
using Godot;
using Core.Dice;

// one physical die - weight, tumble, a shape, and a face that can be read when it settles
// named DieBody rather than Die because Core.Dice.Die is the enum for die SIZE
// owns throw impulses, rest detection, and which collisions were hard enough to be worth hearing
// owns no opinion about a bad throw (IDieRecovery), about noise (DieAudio), or about meaning (core/)
// deliberately not a [Tool] script - building in the editor would bake a stale mesh and mass into die.tscn

public partial class DieBody : RigidBody3D
{
    [Signal] public delegate void SettledEventHandler(int value);

    // one signal per recovery action, because a listener counting "how often was a die cocked"
    // and a listener counting "how often did we re-throw it" are asking different questions
    // these carried one signal and two different counters until 2026-08-20, and every consumer
    // that added them up got a number roughly 3x the truth - see SEAMS.md
    [Signal] public delegate void NudgedEventHandler(float alignment, int attempt);

    [Signal] public delegate void CockedEventHandler(float alignment, int attempt);

    [Signal] public delegate void LeftTrayEventHandler(int attempt);

    // every collision hard enough to be worth hearing - see Listen
    // a plain C# event, not a [Signal], because DieHit is a struct and signals carry only Variants
    // it also doesn't survive Duplicate(), which is right - the fairness sweep clones dice and wants none of it
    public event Action<DieHit> Struck;

    Die _size = Die.D6;

    // everything about the die's body is rebuilt from this, in the inspector or at runtime
    [Export] public Die Size
    {
        get => _size;
        set
        {
            _size = value;
            if (IsNodeReady()) Build();
        }
    }

    // the tuning below is all [Export] - throw feel is a question you answer by playing with it

    // launch speed, metres per second, before the drop adds to it
    [Export] public float ThrowSpeedMin { get; set; } = 0.8f;
    [Export] public float ThrowSpeedMax { get; set; } = 1.3f;

    // upward share of the launch direction - higher arcs hang longer
    [Export] public float LiftMin { get; set; } = 0.10f;
    [Export] public float LiftMax { get; set; } = 0.35f;

    // sideways spread, as a share of forward - kept low
    // a wide spread makes three dice converge and collide mid-flight, the main cause of cocked landings
    [Export] public float Spread { get; set; } = 0.12f;

    [Export] public float SpinMin { get; set; } = 12f;
    [Export] public float SpinMax { get; set; } = 30f;

    // below these for RestHoldSeconds counts as settled
    [Export] public float RestLinearSpeed { get; set; } = 0.02f;
    [Export] public float RestAngularSpeed { get; set; } = 0.15f;
    [Export] public float RestHoldSeconds { get; set; } = 0.2f;

    // how squarely the resting face must point at the felt, as a dot product
    // 1.0 is dead flat, 0.9 allows about 26 degrees of tilt
    // a FLOOR, not the figure used - some shapes need tighter and get it, see RequiredAlignment
    [Export] public float CockedAlignment { get; set; } = 0.9f;

    // give-up count for cocked re-throws, so a wedged die can't loop forever
    [Export] public int MaxCockedRethrows { get; set; } = 3;

    // a cocked die is nudged before it is re-thrown - it almost always drops flat,
    // and doesn't yank the die across the tray the way a re-throw does
    [Export] public float NudgeSpeed { get; set; } = 0.45f;

    [Export] public int MaxNudges { get; set; } = 2;

    // out of the tray: below this world Y, or beyond LostRadius from the centre
    // assumes the tray sits at the world origin - move the tray and both move with it
    [Export] public float LostBelowY { get; set; } = -0.2f;

    [Export] public float LostRadius { get; set; } = 0.6f;

    // re-throws for an escaped die, before the default policy takes whatever is showing
    [Export] public int MaxLostRethrows { get; set; } = 3;

    // dice are given a material, not a mass - a bigger, rounder solid comes out heavier,
    // as a real set does. 400 reproduces the 0.05 kg cube M0 was tuned against
    // only collisions feel it: speed and spin are set directly, so a throw ignores shape
    [Export] public float Density { get; set; } = 400f;

    [Export] public Color Ink { get; set; } = new(0.12f, 0.1f, 0.09f);

    bool _showNumbers = true;

    // off for the fairness sweep, which builds fifty dice for a run that has no screen
    [Export] public bool ShowNumbers
    {
        get => _showNumbers;
        set
        {
            _showNumbers = value;
            if (IsNodeReady()) Build();
        }
    }

    // a line per settle and per cocked re-throw - on in the tray, off for the sweep
    // the give-up case stays loud either way; if that one is frequent, that IS the finding
    [Export] public bool LogSettles { get; set; } = true;

    bool _reportContacts = true;

    // watch collisions and raise Struck - off for the sweep, same reason the numbers come off
    [Export] public bool ReportContacts
    {
        get => _reportContacts;
        set
        {
            _reportContacts = value;

            // both halves switched together - clearing only the count would leave the monitor
            // running and reporting nothing, which looks identical to a die that touches nothing
            ContactMonitor = value;
            MaxContactsReported = value ? ContactsWatched : 0;
        }
    }

    // a d12 landing flat reports several points at once and they are summed into one hit,
    // so this only has to be enough that the sum isn't clipped
    const int ContactsWatched = 6;

    // how hard a collision has to be to count, as the speed the die lost to it
    // mass-independent on purpose, so a d4 and a d12 need the same real bump
    // it has to clear gravity - the felt pushes a resting die back up 0.08 m/s a tick at 120 Hz
    [Export] public float MinHitSpeed { get; set; } = 0.18f;

    // shortest gap between two hits
    // one bounce lasts several steps, and without this one collision fires four times and rips
    [Export] public float MinHitGap { get; set; } = 0.045f;

    // what to do about a throw that can't be read - substitutable without touching any physics
    public IDieRecovery Recovery { get; set; }

    // derived rather than stored, so it answers before the node is ready and can never
    // disagree with Size; the solids are immutable and cached, so asking is a lookup
    public DieSolid Solid => DieSolid.For(Size);

    // CockedAlignment or the shape's own minimum, whichever is stricter
    public float RequiredAlignment => Mathf.Max(CockedAlignment, Solid.MinFlatAlignment);

    DieFaceTable _faces;

    readonly RandomNumberGenerator _rng = new();

    // a state change queued for the next physics step - see _IntegrateForces
    struct Kick
    {
        public bool Reposition;
        public Transform3D Where;
        public Vector3 Linear;
        public Vector3 Angular;
    }

    bool _kickQueued;
    Kick _kick;

    bool _inFlight;
    double _flightTime;
    double _stillTime;

    // where the last throw came from, so a cocked die can be thrown again identically
    Transform3D _lastThrowFrom;
    int _cockedRethrows;
    int _lostRethrows;
    int _nudges;

    // hits since the current throw left the hand - one means the first impact
    int _hits;

    // hits fire on the rising edge, so the last step's answer is kept
    bool _wasStruck;

    double _sinceHit;

    public bool IsSettled => !_inFlight;

    // physically still right now, unlike IsSettled, which latches when the settle was declared
    // a re-thrown die can bump one that had already settled, and nothing re-arms its flight
    // tracking - so IsSettled stays true while the face on top changes
    // a caller reading several dice at once has to wait for this as well
    public bool IsAtRest =>
        LinearVelocity.Length() < RestLinearSpeed && AngularVelocity.Length() < RestAngularSpeed;

    // zero while a throw is in the air, so a caller that forgets to check IsSettled gets an
    // obviously wrong number rather than a stale plausible one
    public int SettledValue { get; private set; }

    public override void _Ready()
    {
        _rng.Randomize();

        Recovery ??= new NudgeThenRethrow(MaxNudges, MaxCockedRethrows, MaxLostRethrows);

        // re-applied rather than assumed: an [Export] setter does not run for the default value,
        // so without this the field would say true while the physics server watched nothing
        ReportContacts = _reportContacts;

        Build();
    }

    // cut the die from its solid: mesh, collision hull, numerals, face table, mass
    // idempotent - a Size change calls it again, and so does a duplicate, which arrives
    // carrying a copy of the numbers the original had already grown
    void Build()
    {
        DieSolid solid = Solid;
        _faces = solid.FaceTable();

        Mass = Density * solid.Volume;

        GetNode<CollisionShape3D>("CollisionShape3D").Shape = DieParts.BuildHull(solid);
        GetNode<MeshInstance3D>("MeshInstance3D").Mesh = DieParts.BuildMesh(solid);

        Node existing = GetNodeOrNull(DieParts.NumbersNode);
        if (existing != null)
        {
            RemoveChild(existing);
            existing.Free();
        }

        if (ShowNumbers) AddChild(DieParts.BuildNumbers(solid, Ink));
    }

    // roughly along -Z, up the tray and away from the camera. safe to call mid-flight
    public void Throw(Transform3D from)
    {
        _cockedRethrows = 0;
        _lostRethrows = 0;
        Launch(from);
    }

    // split from Throw so the cocked and escaped paths can re-throw without resetting the
    // attempt counters that bound them
    // energy scales the horizontal push - at zero the die drops inside the tray and cannot escape
    // the tumble is unscaled: a gentle throw should still look like a roll, not a placement
    void Launch(Transform3D from, float energy = 1f)
    {
        _lastThrowFrom = from;
        SettledValue = 0;
        _nudges = 0;

        // the next contact is this throw's first impact, whatever the last one left behind
        _hits = 0;
        _wasStruck = false;
        _sinceHit = MinHitGap;

        // normalising a zero vector yields NaN, so a pure drop is its own case
        Vector3 direction = energy <= 0.001f
            ? Vector3.Zero
            : new Vector3(
                _rng.RandfRange(-Spread, Spread),
                _rng.RandfRange(LiftMin, LiftMax),
                -1f).Normalized();

        Queue(new Kick
        {
            Reposition = true,

            // random start orientation, so a throw never begins on the same face twice
            Where = new Transform3D(new Basis(RandomAxis(), _rng.RandfRange(0f, Mathf.Tau)), from.Origin),

            Linear = direction * _rng.RandfRange(ThrowSpeedMin, ThrowSpeedMax) * energy,
            Angular = RandomAxis() * _rng.RandfRange(SpinMin, SpinMax),
        });
    }

    // a small lift and spin in place, to topple a die resting on an edge or on a neighbour
    // deliberately weak - enough to unseat it, not enough to move it, so one throw still looks like one
    void Nudge()
    {
        Vector3 dir = new Vector3(
            _rng.RandfRange(-0.4f, 0.4f), 1f, _rng.RandfRange(-0.4f, 0.4f)).Normalized();

        Queue(new Kick
        {
            Reposition = false,
            Linear = dir * NudgeSpeed,
            Angular = RandomAxis() * _rng.RandfRange(SpinMin, SpinMax) * 0.35f,
        });
    }

    // in flight from THIS instant, not from whenever the physics server next runs
    // the kick lands in _IntegrateForces, several idle frames away in a headless run with no vsync
    // a caller polling IsSettled in between would read the last throw's face as this one's
    void Queue(Kick kick)
    {
        _kick = kick;
        _kickQueued = true;

        // a sleeping body is skipped by _IntegrateForces entirely
        Sleeping = false;

        _inFlight = true;
        _flightTime = 0;
        _stillTime = 0;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        // before the kick, not after: the contacts on the state are what the last step solved,
        // so reading them after a teleport describes a collision at an address the die has left
        if (_reportContacts) Listen(state);

        if (!_kickQueued) return;
        _kickQueued = false;

        if (_kick.Reposition) state.Transform = _kick.Where;

        // velocities set outright rather than as impulses
        // the same thing for a cube, whose inertia is equal about every axis, but not for the rest
        // a die's tumble should be the spin it was given, not what its inertia made of a torque
        state.LinearVelocity = _kick.Linear;
        state.AngularVelocity = _kick.Angular;

        _inFlight = true;
        _flightTime = 0;
        _stillTime = 0;
    }

    // turns this step's contacts into at most one Struck, or the die buzzes instead of clattering
    // SUMMED, not per contact - a cube landing flat touches the felt at four corners in one step,
    // and those four impulses added are what makes a flat slam bigger than a corner tap
    // RISING EDGE - a bounce is in contact for several steps at 120 Hz, so only the onset fires
    // MASS-INDEPENDENT - impulse over mass is the speed the collision cost the die
    void Listen(PhysicsDirectBodyState3D state)
    {
        _sinceHit += state.Step;

        int contacts = state.GetContactCount();

        float impulse = 0f;
        float flatness = 0f;
        bool againstDie = false;

        for (int i = 0; i < contacts; i++)
        {
            // magnitudes, not vectors: a die wedged between two walls is being hit twice,
            // and adding those as vectors would cancel them into silence
            float force = state.GetContactImpulse(i).Length();
            impulse += force;

            // weighted by force, so the surface reported is the one that did the hitting
            flatness += Mathf.Abs(state.GetContactLocalNormal(i).Dot(Vector3.Up)) * force;

            // the tray is a StaticBody3D and dice are RigidBody3D, so the cases separate themselves
            if (!againstDie && state.GetContactColliderObject(i) is RigidBody3D) againstDie = true;
        }

        float speed = Mass > 0f ? impulse / Mass : 0f;
        bool struck = speed >= MinHitSpeed;

        if (struck && !_wasStruck && _sinceHit >= MinHitGap)
        {
            _hits++;
            _sinceHit = 0;

            Struck?.Invoke(new DieHit(
                Size,
                impulse,
                speed,
                impulse > 0f ? flatness / impulse : 0f,
                againstDie,

                // the fastest-moving point rather than the centre - a die can drift at walking
                // pace while spinning hard, and that is a tumble, not a settle
                state.LinearVelocity.Length() + state.AngularVelocity.Length() * Solid.Circumradius,

                _hits == 1));
        }

        _wasStruck = struck;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_inFlight) return;

        _flightTime += delta;

        // an escaped die never comes to rest, so without this the tray waits forever for a
        // Settled that will never arrive. roughly one throw in 2,000 gets out
        if (HasLeftTray())
        {
            _inFlight = false;
            _lostRethrows++;

            DieRecoveryStep step = Recovery.Escaped(new EscapedDie(_lostRethrows, _flightTime));

            if (step.Action == DieRecoveryAction.Rethrow)
            {
                GD.Print($"{Name}: left the tray after {_flightTime:0.00}s - rethrow {_lostRethrows} at {step.Energy:0.00} energy");
                EmitSignal(SignalName.LeftTray, _lostRethrows);
                Launch(_lastThrowFrom, step.Energy);
                return;
            }

            // a nudge cannot help a die off the table, so anything but a re-throw ends it here
            GD.Print($"{Name}: STILL outside the tray after {_lostRethrows} attempts - forcing a settle");
            (SettledValue, _) = ReadFace();
            EmitSignal(SignalName.Settled, SettledValue);
            return;
        }

        // ignore the first moments: the kick may land a frame after this runs
        if (_flightTime < 0.1) return;

        bool still = LinearVelocity.Length() < RestLinearSpeed
                  && AngularVelocity.Length() < RestAngularSpeed;
        _stillTime = still ? _stillTime + delta : 0;

        if (!Sleeping && _stillTime < RestHoldSeconds) return;

        _inFlight = false;

        (int value, float alignment) = ReadFace();
        float required = RequiredAlignment;

        // every GD.Print below is developer diagnostic, never player-facing - not localized

        if (alignment < required)
        {
            DieRecoveryStep step = Recovery.Cocked(
                new CockedDie(value, alignment, required, _nudges, _cockedRethrows));

            if (step.Action == DieRecoveryAction.Nudge)
            {
                _nudges++;
                if (LogSettles)
                    GD.Print($"{Name}: cocked at {alignment:0.000} (needs {required:0.000}) - nudge {_nudges}");
                EmitSignal(SignalName.Nudged, alignment, _nudges);
                Nudge();
                return;
            }

            if (step.Action == DieRecoveryAction.Rethrow)
            {
                _cockedRethrows++;
                if (LogSettles)
                    GD.Print($"{Name}: still cocked at {alignment:0.000} after {_nudges} nudges - rethrow {_cockedRethrows}");
                EmitSignal(SignalName.Cocked, alignment, _cockedRethrows);
                Launch(_lastThrowFrom, step.Energy);
                return;
            }

            // wedged somewhere the throw can't shake it out of
            // the nearest face is a guess, not a reading - but looping silently is worse, so say so loudly
            GD.Print($"{Name}: STILL cocked at {alignment:0.000} (needs {required:0.000}) - taking {value} anyway");
        }

        SettledValue = value;

        if (LogSettles)
            GD.Print($"{Name}: settled after {_flightTime:0.00}s showing {value} (alignment {alignment:0.000}){(Sleeping ? " (asleep)" : "")}");

        EmitSignal(SignalName.Settled, value);
    }

    // alignment is a dot product - 1.0 is dead flat, below RequiredAlignment means cocked
    // not always the face on top: a d4 has a corner up there and is read from the face on the felt
    // live, not cached - valid mid-tumble, it just won't mean much until the die stops
    public (int Value, float Alignment) ReadFace() => _faces.Read(GlobalBasis);

    bool HasLeftTray()
    {
        Vector3 p = GlobalPosition;
        return p.Y < LostBelowY
            || new Vector2(p.X, p.Z).Length() > LostRadius;
    }

    Vector3 RandomAxis()
    {
        Vector3 v;
        do
        {
            v = new Vector3(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f));
        }
        while (v.LengthSquared() < 0.001f);

        return v.Normalized();
    }
}
