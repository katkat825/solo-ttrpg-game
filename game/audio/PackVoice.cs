using System.Collections.Generic;
using Godot;
using Content.Audio;
using Content.Minis;

namespace Game.Audio
{
    // WHAT A SUPPLIED MINI SOUNDS LIKE (MINIS_AND_ART.md A3).
    //
    // `THE_TABLE.md` section 7 ranks audio as half the game, and A3 puts it plainly: "a mini that
    // lands silently is half a mini." This is the pack's own foley, keyed by what the piece is
    // doing, dropped into the playback that already exists.
    //
    // THE SAME SHAPE AS `MiniVoice` AND `SurfaceVoice`: everything about how a thing sounds lives
    // in one class, and what plays it knows only how to play a stream at a volume and a pitch. A
    // pack supplying its own set is a second voice here, not an edit to `Mini`.
    //
    // AND THE SAME PLAYBACK. An `AudioStreamRandomizer` over the folder, `RandomNoRepeats`, the
    // same 1.08 pitch and 2 dB of variation `ImpactPool` uses - because a supplied set has to sit
    // beside the shipped pools in one mix rather than beside them in two. "The engine picks from
    // the set the same way the wood pool is sampled, so a supplied set drops into the existing
    // playback with no new mixing path."
    //
    // BUILT FROM `WaveFacts` RATHER THAN FROM A PATH, and that is the one real difference from
    // `ImpactPool`. A pack lives OUTSIDE the `.pck` - it is an ordinary folder on an ordinary
    // disk, which `res://` cannot reach and `GD.Load` will not open - so the bytes come from
    // `Content.Audio.WaveReader`, which has already parsed and metered them headlessly. One parse,
    // in the place where it is testable, and a stream built out of the result.
    public sealed class PackVoice
    {
        readonly Dictionary<Motion, AudioStream> _sets = new Dictionary<Motion, AudioStream>();

        // matched to `MiniVoice.SetDownDb`, so a pack's set-down and the shipped one sit at the
        // same level in the same mix and a pack cannot make itself louder by supplying foley
        const float Db = -2f;

        public bool IsEmpty => _sets.Count == 0;

        public int Count => _sets.Count;

        // null when a mini supplied nothing, which is most of them - and then `Mini` plays the
        // shared pool, exactly as it did before this milestone
        public static PackVoice Over(Mounted mounted)
        {
            if (mounted == null || mounted.Foley.Count == 0) return null;

            var voice = new PackVoice();

            foreach (KeyValuePair<Motion, string> set in mounted.Foley)
            {
                FoleySet samples = FoleySet.Read(set.Value, set.Value);

                // ALREADY REPORTED, NOT REPORTED AGAIN. `Package.CrossCheckArt` ran this same read
                // at load and named every problem in it; saying it twice would double every line
                // of a validator report. What matters here is only whether anything is playable
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

        // THE PARSED SAMPLE, HANDED TO GODOT AS-IS. Every field below came out of the file's own
        // format chunk, and `WaveReader` has already refused anything `AudioStreamWav` cannot play
        // - which is exactly why `FoleyCaps.Depths` is 8 and 16 and not a taste about quality
        static AudioStreamWav Wave(WaveFacts sample) => new AudioStreamWav
        {
            Data = sample.Pcm,
            Format = sample.Depth == 8 ? AudioStreamWav.FormatEnum.Format8Bits
                                       : AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sample.Rate,
            Stereo = sample.IsStereo,
        };

        // TRUE WHEN SOMETHING WAS PLAYED, so the caller's fallback is one line - the same shape
        // `MiniClips.Play` uses, because they are the same decision about the same moment
        public bool Play(AudioStreamPlayer3D player, Motion motion)
        {
            if (player == null || !_sets.TryGetValue(motion, out AudioStream stream)) return false;

            player.Stream = stream;
            player.VolumeDb = Db;
            player.PitchScale = 1f;
            player.Play();

            return true;
        }

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() => $"{_sets.Count} supplied foley sets";
    }
}
