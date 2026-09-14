using System;

namespace Content.Audio
{
    // ONE SAMPLE, MEASURED (MINIS_AND_ART.md A3).
    //
    // What came out of the header, plus the two numbers a header cannot carry: how loud the sample
    // actually is, peak and average. Those two are the reason this exists rather than a tuple -
    // the loudness cap is the one a pack author cannot self-check without a meter, so the
    // validator has to be the meter.
    //
    // THE PCM ITSELF IS CARRIED, and that is deliberate rather than wasteful. Godot's
    // `AudioStreamWav` wants exactly this - a byte array, a format, a mix rate and whether it is
    // stereo - so the game side builds a playable stream out of a `WaveFacts` and never opens the
    // file a second time or reaches for a loader that would have its own opinion about the
    // container. One parse, in `content/`, where it is testable headlessly.
    public sealed class WaveFacts
    {
        public WaveFacts(string file, int channels, int rate, int depth, int frames,
                         float peak, float rms, byte[] pcm)
        {
            File = file ?? "";
            Channels = channels;
            Rate = rate;
            Depth = depth;
            Frames = frames;
            Peak = peak;
            Rms = rms;
            Pcm = pcm ?? Array.Empty<byte>();
        }

        // as the author would name it
        public string File { get; }

        public int Channels { get; }

        public bool IsStereo => Channels == 2;

        // samples per second
        public int Rate { get; }

        // bits per sample - 8 or 16 (`FoleyCaps.Depths`)
        public int Depth { get; }

        // per channel, which is what "how long is it" is actually counting
        public int Frames { get; }

        public float Seconds => Rate > 0 ? (float)Frames / Rate : 0f;

        // dBFS. `Silent` rather than negative infinity for an all-zero file, so nothing downstream
        // has to defend itself against an infinity in a comparison
        public float Peak { get; }

        public float Rms { get; }

        // the loudest a digital sample can be is 0 dBFS, so a floor rather than -inf
        public const float Silent = -120f;

        // the sample data itself, exactly as the file carried it
        public byte[] Pcm { get; }

        // DEVELOPER AND AUTHOR FACING, not localized - this is the line the validator prints
        public override string ToString() =>
            $"{File}: {Seconds:0.00}s, {Rate} Hz {Depth}-bit " +
            (IsStereo ? "stereo" : "mono") +
            $", peak {Peak:0.0} dB, rms {Rms:0.0} dB";
    }
}
