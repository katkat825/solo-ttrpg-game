using System.Collections.Generic;
using Godot;
using Content.Audio;
using Content.Minis;

namespace Game.Audio
{
    // built from WaveFacts, not a path: a pack lives outside the .pck where res:// and GD.Load can't reach, so WaveReader parses the bytes headlessly
    public sealed class PackVoice
    {
        readonly Dictionary<Motion, AudioStream> _sets = new Dictionary<Motion, AudioStream>();

        // matched to MiniVoice.SetDownDb, so a pack can't make itself louder by supplying its own foley
        const float Db = -2f;

        public bool IsEmpty => _sets.Count == 0;

        public int Count => _sets.Count;

        // null when a mini supplied no foley (most of them); Mini then plays the shared pool
        public static PackVoice Over(Mounted mounted)
        {
            if (mounted == null || mounted.Foley.Count == 0) return null;

            var voice = new PackVoice();

            foreach (KeyValuePair<Motion, string> set in mounted.Foley)
            {
                FoleySet samples = FoleySet.Read(set.Value, set.Value);

                // problems here were already reported by CrossCheckArt at load; skip silently, only whether anything is playable matters
                if (samples.IsEmpty) continue;

                voice._sets[set.Key] = Randomized(samples);
            }

            return voice.IsEmpty ? null : voice;
        }

        static AudioStream Randomized(FoleySet samples)
        {
            var randomizer = new AudioStreamRandomizer
            {
                PlaybackMode = AudioStreamRandomizer.PlaybackModeEnum.RandomNoRepeats,
                RandomPitch = 1.08f,
                RandomVolumeOffsetDb = 2f,
            };

            foreach (WaveFacts sample in samples.Samples)
                randomizer.AddStream(-1, Wave(sample));

            return randomizer;
        }

        // handed to Godot as-is: WaveReader already refused anything AudioStreamWav can't play, which is why the depths are 8 and 16
        static AudioStreamWav Wave(WaveFacts sample) => new AudioStreamWav
        {
            Data = sample.Pcm,
            Format = sample.Depth == 8 ? AudioStreamWav.FormatEnum.Format8Bits
                                       : AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sample.Rate,
            Stereo = sample.IsStereo,
        };

        // returns whether it played, so the caller's fallback is one line
        public bool Play(AudioStreamPlayer3D player, Motion motion)
        {
            if (player == null || !_sets.TryGetValue(motion, out AudioStream stream)) return false;

            player.Stream = stream;
            player.VolumeDb = Db;
            player.PitchScale = 1f;
            player.Play();

            return true;
        }

        // developer only, not localized, must never reach the screen
        public override string ToString() => $"{_sets.Count} supplied foley sets";
    }
}
