using System.Collections.Generic;
using Godot;

// the noise one die makes
// a child of DieBody, listens to it, plays whatever its IDieVoice returns
// owns no opinion about how a die sounds - which sample, how loud, what pitch is the voice's
// what it owns is the plumbing: a pool of players so overlapping hits don't cut each other
// off, and the burst timing for the shake before a throw

public partial class DieAudio : Node3D
{
    // set before the node is ready, or it defaults to SurfaceVoice.Shared
    public IDieVoice Voice { get; set; }

    // one player can only play one thing, so a pool is the difference between
    // a clatter and half-samples cutting each other short
    [Export] public int Voices { get; set; } = 6;

    // samples are normalised to -3 dBFS and three dice land together
    [Export] public float MasterDb { get; set; } = -6f;

    [Export] public int RattleTaps { get; set; } = 5;   // zero turns the rattle off

    // must stay under the tray's rattle lead, or it is still going when the die flies
    [Export] public float RattleSeconds { get; set; } = 0.11f;

    // metres to full volume. the tray is 0.64 m across and the camera sits about a metre off
    // it, so at these distances the 3D player is mostly buying stereo position
    [Export] public float UnitSize { get; set; } = 0.9f;

    DieBody _die;

    readonly List<AudioStreamPlayer3D> _players = new();
    int _next;

    readonly RandomNumberGenerator _rng = new();

    int _tapsLeft;

    double _rattleAge;

    double _tapDue;

    // parked at the hand for a rattle rather than following the die
    bool _atHand;

    public override void _Ready()
    {
        _rng.Randomize();

        Voice ??= SurfaceVoice.Shared;

        _die = GetParent<DieBody>();

        if (_die == null)
        {
            // developer diagnostic, not player-facing
            GD.PushError($"{Name}: DieAudio has to be a child of a DieBody - this die will be silent");
            return;
        }

        _die.Struck += OnStruck;
    }

    public override void _ExitTree()
    {
        if (_die != null) _die.Struck -= OnStruck;
    }

    // the shake before the throw, from the hand rather than from wherever the die is lying
    // three dice scattered across the felt would rattle from three different places, which
    // sounds like three dice being knocked over rather than one handful being shaken
    // the node parks at the throw point and goes back to following the die on first impact
    public void Rattle(Vector3 hand)
    {
        if (RattleTaps <= 0 || _die == null) return;

        TopLevel = true;
        GlobalPosition = hand;
        _atHand = true;

        _tapsLeft = RattleTaps;
        _rattleAge = 0;
        _tapDue = 0;
    }

    // on the physics clock because DiceTray.RattleLeadTicks counts physics ticks to the release
    // two clocks would drift apart exactly when the frame rate dipped, and the symptom would be
    // the hand still rattling after the dice had hit the table
    public override void _PhysicsProcess(double delta)
    {
        if (_tapsLeft <= 0) return;

        _rattleAge += delta;

        // a loop, not an if: five taps in a tenth of a second is finer than a tick at 60 Hz,
        // and a stalled frame should compress the rattle rather than push it past the throw
        while (_tapsLeft > 0 && _rattleAge >= _tapDue)
        {
            Play(Voice.Shaken(_die.Size, RattleTaps - _tapsLeft, RattleTaps));
            _tapsLeft--;

            // each tap gets a slot and jitters inside it, rather than jittering the gaps and
            // adding them up - accumulated jitter makes the burst's LENGTH random, and a
            // rattle that overruns is still going after the dice have landed
            _tapDue = RattleSeconds * (RattleTaps - _tapsLeft + _rng.RandfRange(0f, 0.85f)) / RattleTaps;
        }
    }

    void OnStruck(DieHit hit)
    {
        // it hit something, so the handful has stopped being a handful
        // sound goes back to the object
        if (_atHand)
        {
            _atHand = false;
            TopLevel = false;
            Transform = Transform3D.Identity;
        }

        Play(Voice.Struck(hit));
    }

    void Play(in DieSound sound)
    {
        if (sound.IsSilent) return;

        AudioStreamPlayer3D player = Take();

        player.Stream = sound.Stream;
        player.VolumeDb = sound.VolumeDb + MasterDb;

        // pitch is the one value a voice can get catastrophically wrong
        // zero stops the engine's resampler dead and a large number is a click
        player.PitchScale = Mathf.Clamp(sound.PitchScale, 0.1f, 4f);

        player.Play();
    }

    // round-robin over the pool, built on first use
    // lazy because the fairness sweep clones fifty dice into a headless run that never asks
    AudioStreamPlayer3D Take()
    {
        if (_players.Count == 0)
        {
            for (int i = 0; i < Mathf.Max(1, Voices); i++)
            {
                var player = new AudioStreamPlayer3D
                {
                    Name = $"Voice{i}",
                    UnitSize = UnitSize,

                    // 0 dB rather than Godot's +3 - nothing should come back louder than the
                    // sample, and at tray distances the curve would sit everything on the ceiling
                    MaxDb = 0f,

                    AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                    PanningStrength = 1.2f,
                };

                AddChild(player);
                _players.Add(player);
            }
        }

        AudioStreamPlayer3D next = _players[_next];
        _next = (_next + 1) % _players.Count;
        return next;
    }
}
