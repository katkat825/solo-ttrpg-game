using System.Collections.Generic;
using Godot;
using Game.Dice;

namespace Game.Audio
{
    // owns the plumbing, not the sound: a pool of players so overlapping hits don't cut each other off, and the shake timing
    public partial class DieAudio : Node3D
    {
        // set before the node is ready, or it defaults to SurfaceVoice.Shared
        public IDieVoice Voice { get; set; }

        // a player plays one thing at a time, so a pool is the difference between a clatter and half-samples cut short
        [Export] public int Voices { get; set; } = 6;

        // samples are normalised to -3 dBFS and three dice land together
        [Export] public float MasterDb { get; set; } = -6f;

        [Export] public int RattleTaps { get; set; } = 5;   // zero turns the rattle off

        // must stay under the tray's rattle lead, or it is still going when the die flies
        [Export] public float RattleSeconds { get; set; } = 0.11f;

        // metres to full volume; at tray distances the 3D player is mostly buying stereo position
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

        // rattle from the hand, not from where each die lies: three scattered rattles sound like three dice knocked over, not one handful shaken
        // parks at the throw point and goes back to following the die on first impact
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

        // on the physics clock, matching DiceTray.RattleLeadTicks: two clocks would drift when the frame rate dips and leave the hand rattling after the dice land
        public override void _PhysicsProcess(double delta)
        {
            if (_tapsLeft <= 0) return;

            _rattleAge += delta;

            // a loop, not an if: taps are finer than a 60 Hz tick, so a stalled frame compresses the rattle rather than pushing it past the throw
            while (_tapsLeft > 0 && _rattleAge >= _tapDue)
            {
                Play(Voice.Shaken(_die.Size, RattleTaps - _tapsLeft, RattleTaps));
                _tapsLeft--;

                // each tap jitters inside its own slot, not the gaps: accumulated jitter would make the burst length random and overrun the throw
                _tapDue = RattleSeconds * (RattleTaps - _tapsLeft + _rng.RandfRange(0f, 0.85f)) / RattleTaps;
            }
        }

        void OnStruck(DieHit hit)
        {
            // on first hit the handful is no longer a handful, so the sound goes back to following the die
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

            // clamp pitch: zero stops the resampler dead and a large value is a click
            player.PitchScale = Mathf.Clamp(sound.PitchScale, 0.1f, 4f);

            player.Play();
        }

        // round-robin over the pool, built lazily so the fairness sweep's fifty headless dice never allocate players
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

                        // 0 dB, not Godot's +3: nothing should come back louder than the sample, and at tray distances the curve would peg everything
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
}
