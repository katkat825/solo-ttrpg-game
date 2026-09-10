using Godot;

namespace Game.Audio
{
    // what a weighted figure sounds like set down on the board - THE_TABLE.md 7 lists it as a
    // first-order sound and B1 is where it is needed, because the click is half of what makes a
    // move feel like a move. Close your eyes and move the piece: it should sound like a piece on
    // a mat, which is B1's verify line and is not something a test can answer.
    //
    // Same shape as SurfaceVoice and for the same reason: everything about how a thing sounds
    // lives in one class, and what plays it knows only how to play a stream at a volume and a
    // pitch. A pewter mini and a resin one are a second voice here, not an edit to Mini.
    //
    // BORROWED, NOT A MINI. There is no recording of a figure being set down yet, so this points
    // at the wood impact pool and leans on volume and pitch to place it: lower, quieter and duller
    // than a die, because a mini is heavier, softer-cornered and lands on one flat base rather
    // than tumbling. Close enough to be convincing, and NOT THE SAME AS BEING RIGHT - exactly the
    // arrangement Tray/skins/surfaces/felt.tres already runs on, and labelled the same way so it
    // is findable. The fix is a real recording in Samples below; the folder is the list, so the
    // day one lands there this switches to it with nothing to edit.
    public sealed class MiniVoice
    {
        // where the real recordings go. same folder-is-the-list rule as impacts: nothing names a
        // file, so adding samples is copying them in
        public const string Samples = "res://audio/samples/minis/";

        // and what stands in until then
        public const string Borrowed = ImpactPool.Default;

        // one voice for every piece on the board - it holds nothing per-mini, and ImpactPool
        // caches by folder, so a board full of minis is a board full of references
        public static readonly MiniVoice Shared = new MiniVoice();

        // a set-down is quiet. it is a cue that the move finished, not an event
        const float SetDownDb = -9f;

        // pitched well down: 50 mm of onyx ringing on a plank is not 40 g of figure being placed
        const float BorrowedPitch = 0.72f;

        // and a shade quieter still, because a die's first bounce is the loudest thing in the
        // wood pool and a mini never bounces at all
        const float BorrowedDb = -4f;

        readonly ImpactPool _pool;

        // true once minis/ has recordings of its own, and every allowance below stops applying
        public bool Borrowing { get; }

        public MiniVoice()
        {
            Borrowing = !ImpactPool.Has(Samples);

            _pool = ImpactPool.For(Borrowing ? Borrowed : Samples);
        }

        public AudioStream Stream => _pool.Stream;

        public float VolumeDb => SetDownDb + (Borrowing ? BorrowedDb : 0f);

        public float PitchScale => Borrowing ? BorrowedPitch : 1f;

        // the pool is an AudioStreamRandomizer, so no two set-downs are the same sample at the
        // same pitch without anything here asking for variation
        public void SetDown(AudioStreamPlayer3D player)
        {
            if (player == null) return;

            player.Stream = Stream;
            player.VolumeDb = VolumeDb;
            player.PitchScale = PitchScale;
            player.Play();
        }

        // DEVELOPER ONLY - not localized, must never reach the screen
        public override string ToString() =>
            Borrowing
                ? $"borrowing {Borrowed} at {PitchScale:0.00}x - no recordings in {Samples} yet"
                : $"{Samples}, {_pool.Count} samples";
    }
}
