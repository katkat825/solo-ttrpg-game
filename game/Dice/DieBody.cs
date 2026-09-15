using System;
using Godot;
using Core.Dice;
using Game.Audio;

namespace Game.Dice
{
    // named DieBody rather than Die because Core.Dice.Die is the die-size enum
    // deliberately not a [Tool] script, or the editor would bake a stale mesh and mass into die.tscn
    public partial class DieBody : RigidBody3D
    {
        [Signal] public delegate void SettledEventHandler(int value);

        // one signal per recovery action: a merged signal once let consumers add the counters and overcount ~3x
        [Signal] public delegate void NudgedEventHandler(float alignment, int attempt);

        [Signal] public delegate void CockedEventHandler(float alignment, int attempt);

        [Signal] public delegate void LeftTrayEventHandler(int attempt);

        // a plain C# event, not a [Signal]: DieHit is a struct and signals carry only Variants
        public event Action<DieHit> Struck;

        Die _size = Die.D6;

        [Export] public Die Size
        {
            get => _size;
            set
            {
                _size = value;
                if (IsNodeReady()) Build();
            }
        }

        [Export] public float ThrowSpeedMin { get; set; } = 0.8f;
        [Export] public float ThrowSpeedMax { get; set; } = 1.3f;

        [Export] public float LiftMin { get; set; } = 0.10f;
        [Export] public float LiftMax { get; set; } = 0.35f;

        // kept low: a wide spread makes three dice converge and collide mid-flight, the main cause of cocked landings
        [Export] public float Spread { get; set; } = 0.12f;

        [Export] public float SpinMin { get; set; } = 12f;
        [Export] public float SpinMax { get; set; } = 30f;

        [Export] public float RestLinearSpeed { get; set; } = 0.02f;
        [Export] public float RestAngularSpeed { get; set; } = 0.15f;
        [Export] public float RestHoldSeconds { get; set; } = 0.2f;

        // how squarely the resting face must point at the felt, as a dot product (1.0 flat, 0.9 ~26deg)
        // a floor, not the value used: some shapes need stricter, see RequiredAlignment
        [Export] public float CockedAlignment { get; set; } = 0.9f;

        // give-up count for cocked re-throws, so a wedged die can't loop forever
        [Export] public int MaxCockedRethrows { get; set; } = 3;

        [Export] public float NudgeSpeed { get; set; } = 0.45f;

        [Export] public int MaxNudges { get; set; } = 2;

        // null means world space: die.tscn opened on its own, with nothing to leave
        // not [Export]: a node reference exported on a scene instanced three times is a NodePath waiting to break
        public Node3D TraySpace { get; set; }

        // out of the tray: below this Y or beyond this radius, both measured in TraySpace so the tray can stand anywhere
        // defaults for a die nothing has told otherwise; DiceTray measures the real tray and hands these over
        public float LostBelowY { get; set; } = -0.2f;

        public float LostRadius { get; set; } = 0.6f;

        [Export] public int MaxLostRethrows { get; set; } = 3;

        // dice get a density, not a mass, so a bigger solid comes out heavier like a real set
        // only collisions feel it: speed and spin are set directly, so a throw ignores shape
        [Export] public float Density { get; set; } = 400f;

        [Export] public Color Ink { get; set; } = new(0.12f, 0.1f, 0.09f);

        bool _showNumbers = true;

        [Export] public bool ShowNumbers
        {
            get => _showNumbers;
            set
            {
                _showNumbers = value;
                if (IsNodeReady()) Build();
            }
        }

        // the give-up case logs regardless; if that one is frequent, that is the finding
        [Export] public bool LogSettles { get; set; } = true;

        bool _reportContacts = true;

        [Export] public bool ReportContacts
        {
            get => _reportContacts;
            set
            {
                _reportContacts = value;

                // set both together: ContactMonitor on with MaxContactsReported at 0 reports nothing, looking like a die that never touches anything
                ContactMonitor = value;
                MaxContactsReported = value ? ContactsWatched : 0;
            }
        }

        // a d12 landing flat reports several contacts at once; enough that the summed hit isn't clipped
        const int ContactsWatched = 6;

        // collision threshold, as the speed the die lost to it; mass-independent so a d4 and d12 need the same bump
        // has to clear gravity: the felt pushes a resting die back up ~0.08 m/s per tick at 120 Hz
        [Export] public float MinHitSpeed { get; set; } = 0.18f;

        // one bounce lasts several steps; without this gap one collision fires several times and rips
        [Export] public float MinHitGap { get; set; } = 0.045f;

        public IDieRecovery Recovery { get; set; }

        // derived, not stored, so it answers before the node is ready and can't disagree with Size; solids are cached
        public DieSolid Solid => DieSolid.For(Size);

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

        // physically still now, unlike IsSettled which latches when the settle was declared
        // a re-thrown die can bump one already settled, so IsSettled can stay true while its face changes; wait on this too
        public bool IsAtRest =>
            LinearVelocity.Length() < RestLinearSpeed && AngularVelocity.Length() < RestAngularSpeed;

        // zero while a throw is in the air, so a caller that skips IsSettled gets an obviously wrong number, not a stale plausible one
        public int SettledValue { get; private set; }

        public override void _Ready()
        {
            _rng.Randomize();

            Recovery ??= new NudgeThenRethrow(MaxNudges, MaxCockedRethrows, MaxLostRethrows);

            // re-apply: an [Export] setter never runs for the default value, so without this the physics server would watch nothing while the field says true
            ReportContacts = _reportContacts;

            Build();
        }

        // rebuild mesh, hull, numerals, face table and mass from the solid; idempotent - Size changes and duplicates call it again
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

        // split from Throw so the cocked and escaped paths re-throw without resetting the attempt counters that bound them
        // energy scales the horizontal push; at zero the die drops inside the tray and cannot escape
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

        // a small lift and spin in place to topple a die on an edge or a neighbour, deliberately weak so one throw still looks like one
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

        // in flight from this instant, not when the physics server next runs: the kick lands in _IntegrateForces frames later headless, and a caller polling IsSettled between would read the last throw's face
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
            // listen before the kick: the state's contacts are what the last step solved, so reading them after a teleport describes an address the die has left
            if (_reportContacts) Listen(state);

            if (!_kickQueued) return;
            _kickQueued = false;

            if (_kick.Reposition) state.Transform = _kick.Where;

            // set velocities outright, not as impulses: a die's tumble should be the spin it was given, not what its inertia makes of a torque
            state.LinearVelocity = _kick.Linear;
            state.AngularVelocity = _kick.Angular;

            _inFlight = true;
            _flightTime = 0;
            _stillTime = 0;
        }

        // summed, not per contact: a flat landing touches at several corners in one step, and the sum is what makes a flat slam bigger than a corner tap
        // rising edge only: a bounce is in contact for several steps at 120 Hz, so only the onset fires
        // mass-independent: impulse over mass is the speed the collision cost the die
        void Listen(PhysicsDirectBodyState3D state)
        {
            _sinceHit += state.Step;

            int contacts = state.GetContactCount();

            float impulse = 0f;
            float flatness = 0f;
            bool againstDie = false;

            for (int i = 0; i < contacts; i++)
            {
                // sum magnitudes, not vectors: a die wedged between two walls is hit twice, and vector-adding would cancel them into silence
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

                    // fastest-moving point, not the centre: a die can drift slowly while spinning hard, which is a tumble not a settle
                    state.LinearVelocity.Length() + state.AngularVelocity.Length() * Solid.Circumradius,

                    _hits == 1));
            }

            _wasStruck = struck;
        }

        public override void _PhysicsProcess(double delta)
        {
            if (!_inFlight) return;

            _flightTime += delta;

            // an escaped die never comes to rest, so without this the tray waits forever for a Settled that never arrives
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

                // the nearest face is a guess, not a reading, but looping silently is worse, so say it loudly
                GD.Print($"{Name}: STILL cocked at {alignment:0.000} (needs {required:0.000}) - taking {value} anyway");
            }

            SettledValue = value;

            if (LogSettles)
                GD.Print($"{Name}: settled after {_flightTime:0.00}s showing {value} (alignment {alignment:0.000}){(Sleeping ? " (asleep)" : "")}");

            EmitSignal(SignalName.Settled, value);
        }

        // alignment is a dot product: 1.0 flat, below RequiredAlignment is cocked
        // not always the top face: a d4 has a corner up and is read from the face on the felt
        public (int Value, float Alignment) ReadFace() => _faces.Read(GlobalBasis);

        // Duplicate() copies [Export]s only; the three properties above are not, so a cloned sweep die would measure against the world origin instead of the tray
        public void BoundLike(DieBody other)
        {
            TraySpace = other.TraySpace;
            LostBelowY = other.LostBelowY;
            LostRadius = other.LostRadius;
        }

        // asked in the tray's own space, so moving the tray moves what 'out' means with it
        bool HasLeftTray()
        {
            Vector3 p = TraySpace?.ToLocal(GlobalPosition) ?? GlobalPosition;

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
}
