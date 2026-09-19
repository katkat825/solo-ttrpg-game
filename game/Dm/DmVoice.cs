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

        // the caption card, handed over by the room. Four of the game's five captioned sounds come
        // from behind this screen, and the two that carry real information are both here: the "hm",
        // and the rattle that says something was rolled you are not being shown
        public Game.Access.Captioned Captions { get; set; }

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

            if (Play(Busy, Volume)) Captions?.Says(Game.Audio.Sound.Busy);
        }

        // the rattle in the cup: reuses the dice audio, muffled, until a cup of its own is recorded
        public void Rattles()
        {
            if (Play(ImpactPool.Has(Behind) ? Behind : ImpactPool.Default, Volume - 8f))
                Captions?.Says(Game.Audio.Sound.Behind);
        }

        public void Reacts()
        {
            if (Play(Reacting, ReactVolume)) Captions?.Says(Game.Audio.Sound.Reacting);
        }

        // CAPTIONED ONLY IF IT ACTUALLY PLAYED, which is why this reports. There are no recordings
        // behind the screen yet, and a caption for a silence would tell a deaf player something
        // happened that a hearing player never heard
        bool Play(string folder, float volume)
        {
            AudioStream stream = Pick(folder);

            if (stream == null || _oneShot == null) return false;

            _oneShot.Stream = stream;
            _oneShot.VolumeDb = volume;
            _oneShot.Play();

            return true;
        }

        static AudioStream Pick(string folder) =>
            ImpactPool.Has(folder) ? ImpactPool.For(folder).Stream : null;

        double Next() =>
            BusyEverySeconds + (_rng.Roll(1000) / 1000.0 - 0.5) * 2.0 * BusyJitter;
    }
}
