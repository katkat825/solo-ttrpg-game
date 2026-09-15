namespace Content.Audio
{
    public static class FoleyCaps
    {
        public const string Extension = ".wav";

        // 8 or 16 only; a wider wav would need a convert-on-load step
        public static readonly int[] Depths = { 8, 16 };

        public const float Seconds = 10f;

        public const long Bytes = 4L * 1024L * 1024L;

        public const int Samples = 64;


        public const float Peak = -0.5f;

        // peak alone misses a brickwalled sample, so cap the average too
        public const float Rms = -12f;

        // quiet is deliberately allowed; only loud is an imposition
    }
}
