using Godot;

namespace Game.Audio
{
    public sealed class MiniVoice
    {
        // where the real recordings go; the folder is the list, so adding samples is copying them in
        public const string Samples = "res://audio/samples/minis/";

        // and what stands in if minis/ is ever empty
        public const string Borrowed = ImpactPool.Default;

        // one voice for every piece: it holds nothing per-mini, so a board of minis shares references
        public static readonly MiniVoice Shared = new MiniVoice();

        // held a touch below unity so the randomizer's +2 dB can't push a -1 dBFS sample past the ceiling and clip
        const float SetDownDb = -2f;

        // pitched well down: 50 mm of onyx ringing on a plank is not 40 g of figure being placed
        const float BorrowedPitch = 0.72f;

        // quieter still: a die's first bounce is the loudest thing in the wood pool, and a mini never bounces
        const float BorrowedDb = -4f;

        readonly ImpactPool _pool;

        // true only if minis/ has no recordings; every allowance above stops applying once it does
        public bool Borrowing { get; }

        public MiniVoice()
        {
            Borrowing = !ImpactPool.Has(Samples);

            _pool = ImpactPool.For(Borrowing ? Borrowed : Samples);
        }

        public AudioStream Stream => _pool.Stream;

        public float VolumeDb => SetDownDb + (Borrowing ? BorrowedDb : 0f);

        public float PitchScale => Borrowing ? BorrowedPitch : 1f;

        // the pool randomizes, so no two set-downs are the same sample or pitch without this asking
        public void SetDown(AudioStreamPlayer3D player)
        {
            if (player == null) return;

            player.Stream = Stream;
            player.VolumeDb = VolumeDb;
            player.PitchScale = PitchScale;
            player.Play();
        }

        // developer only, not localized, must never reach the screen
        public override string ToString() =>
            Borrowing
                ? $"borrowing {Borrowed} at {PitchScale:0.00}x - no recordings in {Samples} yet"
                : $"{Samples}, {_pool.Count} samples";
    }
}
