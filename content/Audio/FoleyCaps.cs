namespace Content.Audio
{
    // WHAT A SUPPLIED SAMPLE IS HELD TO (MINIS_AND_ART.md A3).
    //
    // "Validation: accepted formats, a length/size ceiling, loudness sanity (the maximiser
    // discipline from the mini-audio work, so a pack can't ship a clipping wall of noise), all
    // named on failure."
    //
    // THE FORMAT IS ONE FORMAT AND THAT IS NOT LAZINESS. `Game.Audio.ImpactPool` has read wavs
    // since the tray, `tools/slice_impacts.py` writes them, the 77-sample wood pool is them, and
    // Godot's own `AudioStreamWav` carries 8- and 16-bit PCM. One format the whole chain already
    // speaks is one parser, one loader and no conversion step on a player's machine - and "export
    // a wav" has an answer in every audio tool there is.
    //
    // LOUDNESS IS THE CAP NOBODY EXPECTS AND THE ONE THAT MATTERS MOST. The game mixes a supplied
    // set into the SAME pool the dice and the shipped minis play out of, at the gains
    // `MiniVoice` chose against samples maximised to about -18 dB RMS. A pack that ships a
    // brickwalled sample is not "a bit loud" - it is the loudest thing in the room, every time a
    // piece is set down, and the player's only recourse is to turn the whole game down. So the
    // ceiling is checked and said out loud, in dB, with the number the sample actually measured.
    public static class FoleyCaps
    {
        // the one accepted extension - see above
        public const string Extension = ".wav";

        // WHAT `AudioStreamWav` CAN ACTUALLY PLAY, which is the honest reason this is 8 or 16
        // rather than a taste about quality. A 24-bit or float wav would have to be converted on
        // load, and a conversion nobody asked for is a difference between what an author heard and
        // what a player hears
        public static readonly int[] Depths = { 8, 16 };

        // FOLEY IS SHORT. A set-down is a tenth of a second and the longest thing in this phase is
        // a topple; ten seconds is an ambience loop, which is a different feature and not this one
        public const float Seconds = 10f;

        // per sample. A ten-second 16-bit stereo 48k wav is under 2 MB, so this is headroom rather
        // than a squeeze
        public const long Bytes = 4L * 1024L * 1024L;

        // per set. The folder is the list and variation is the point, but a hundred set-downs is
        // a pack that has misunderstood the feature - and every one of them is resident memory
        public const int Samples = 64;

        // ---- loudness, in dBFS ----

        // THE CEILING. Anything above this is either clipped or was mastered to be the loudest
        // thing in somebody else's game. -1 dBFS is where the mini samples were maximised to, and
        // the randomizer's own +2 dB of variation sits under it
        public const float Peak = -0.5f;

        // AND THE BODY. Peak alone does not catch a brickwalled sample - that is exactly what a
        // maximiser produces, a wall at the ceiling with no dynamics under it - so the average
        // matters more than the maximum. The shipped pools measure about -18; -12 is comfortably
        // above anything honest and well below a wall
        public const float Rms = -12f;

        // A SAMPLE THAT IS TOO QUIET IS DELIBERATELY NOT REFUSED. A mini that lands almost
        // silently may be exactly what its author meant - a cloth figure, a ghost - and the player
        // can turn it up. The asymmetry is on purpose: quiet is a choice, and loud is an
        // imposition on everybody else's mix
    }
}
