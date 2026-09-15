using Game.Audio;
using Godot;

namespace Game.Dm
{
    [GlobalClass]
    public partial class DmVoice : Node3D
    {
        public const string Root = "res://audio/samples/dm/";

        public const string Tone = Root + "tone/";          // the room, under everything
        public const string Busy = Root + "busy/";          // pages, pencil, chair, dice in a cup
        public const string Reacting = Root + "reacts/";    // the "hm"
        public const string Behind = Root + "behind/";      // the secret roll's rattle

        // how often an idle DM makes a small noise: rare enough to notice, not so rare the table sounds empty
        [Export] public float BusyEverySeconds { get; set; } = 14f;

        [Export] public float BusyJitter { get; set; } = 9f;

        // quiet, at the edge of hearing: a reaction you clearly hear is a reaction that is performing
        [Export] public float Volume { get; set; } = -14f;

        [Export] public float ReactVolume { get; set; } = -20f;

        AudioStreamPlayer3D _tone;
        AudioStreamPlayer3D _oneShot;

        double _untilBusy;
        Core.Dice.IRng _rng;

        public override void _Ready()
        {
            _rng = new Core.Dice.SeededRng((int)Time.GetTicksMsec());

            _tone = Add("Tone", loop: true);
            _oneShot = Add("OneShot", loop: false);

            AudioStream tone = Pick(Tone);

            if (tone != null)
            {
                _tone.Stream = tone;
                _tone.Play();
            }

            _untilBusy = Next();

            // printed once at load, not every frame: a DM with no recordings is a real state of this build
            if (!ImpactPool.Has(Root))
                GD.Print($"dm      no sounds in {Root} - the screen is silent " +
                         "(recordings go here)");
        }

        AudioStreamPlayer3D Add(string name, bool loop)
        {
            var player = new AudioStreamPlayer3D
            {
                Name = name,
                VolumeDb = Volume,

                // 3D so it comes from behind the screen, where the DM is, not the middle of your head
                UnitSize = 0.6f,
            };

            AddChild(player);

            return player;
        }

        public override void _Process(double delta)
        {
            if (_oneShot == null) return;

            _untilBusy -= delta;

            if (_untilBusy > 0.0) return;

            _untilBusy = Next();

            Play(Busy, Volume);
        }

        // the rattle in the cup: reuses the dice audio, muffled, until a cup of its own is recorded
        public void Rattles() =>
            Play(ImpactPool.Has(Behind) ? Behind : ImpactPool.Default, Volume - 8f);

        public void Reacts() => Play(Reacting, ReactVolume);

        void Play(string folder, float volume)
        {
            AudioStream stream = Pick(folder);

            if (stream == null || _oneShot == null) return;

            _oneShot.Stream = stream;
            _oneShot.VolumeDb = volume;
            _oneShot.Play();
        }

        static AudioStream Pick(string folder) =>
            ImpactPool.Has(folder) ? ImpactPool.For(folder).Stream : null;

        double Next() =>
            BusyEverySeconds + (_rng.Roll(1000) / 1000.0 - 0.5) * 2.0 * BusyJitter;
    }
}
