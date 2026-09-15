using System;

namespace Content.Audio
{
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

        public string File { get; }

        public int Channels { get; }

        public bool IsStereo => Channels == 2;

        public int Rate { get; }

        public int Depth { get; }

        public int Frames { get; }

        public float Seconds => Rate > 0 ? (float)Frames / Rate : 0f;

        public float Peak { get; }

        public float Rms { get; }

        // floor rather than -inf so nothing downstream compares against infinity
        public const float Silent = -120f;

        public byte[] Pcm { get; }

        public override string ToString() =>
            $"{File}: {Seconds:0.00}s, {Rate} Hz {Depth}-bit " +
            (IsStereo ? "stereo" : "mono") +
            $", peak {Peak:0.0} dB, rms {Rms:0.0} dB";
    }
}
