using Godot;
using Core.Dice;
using Game.Tray;

namespace Game.Audio
{
    // the surface decides the sound: what gets struck dominates, and the die only modulates by force and size
    public sealed class SurfaceVoice : IDieVoice
    {
        // fallback when a die hasn't been told where it is: the fairness sweep, and any die in a scene with no tray
        public static readonly SurfaceVoice Shared = new(null);

        // the speed at which a hit is as loud as the sample gets; past it force adds brightness, not volume, so a hard throw doesn't clip
        const float FullForce = 2.5f;

        // exponent lifting soft hits toward hard: a linear map buries the whole tumble under the first bounce, which carries fifty times the momentum
        const float ForceCurve = 0.75f;

        const float SettlingSpeed = 0.8f;   // below this the die is sitting down, not bouncing

        // above this Flatness the die hit the floor, below it a wall; a hard split because there's no halfway between two sample folders
        const float FloorFlatness = 0.5f;

        const float DieOnDieDb = -2.5f;

        const float DieOnDiePitch = 1.09f;

        const float RattleDb = -17f;   // quiet on purpose - a cue, not an event

        readonly TraySurface _floor;
        readonly TraySurface _walls;

        // a null tray - or a null surface on one - falls back to the default pool, untrimmed
        public SurfaceVoice(TraySkin tray)
        {
            _floor = tray?.Floor;
            _walls = tray?.Walls;
        }

        // larger solids ring lower; the size is the only thing about the die itself that reaches the sound
        static float SizePitch(Die size) => size switch
        {
            Die.D4 => 1.20f,
            Die.D6 => 1.11f,
            Die.D8 => 1.04f,
            Die.D10 => 1.00f,   // the die the recordings were made from
            Die.D12 => 0.92f,
            _ => 1f,
        };

        static AudioStream Samples(TraySurface surface) =>
            ImpactPool.For(surface?.AudioPool).Stream;

        public DieSound Struck(in DieHit hit)
        {
            // amplitude, not energy: it's what a fader measures and the ear reports; squaring would make soft taps vanish before they got quiet
            float force = Mathf.Clamp(hit.Speed / FullForce, 0f, 1f);

            float db = Mathf.LinearToDb(Mathf.Pow(Mathf.Max(force, 0.02f), ForceCurve));

            // harder is brighter too: without the pitch lift a heavy throw is just the same tap with the fader raised
            float pitch = SizePitch(hit.Size) * Mathf.Lerp(0.94f, 1.06f, force);

            AudioStream stream;

            if (hit.AgainstDie)
            {
                // neither surface was involved, so neither surface's character applies
                stream = Samples(_floor);
                db += DieOnDieDb;
                pitch *= DieOnDiePitch;
            }
            else
            {
                TraySurface surface = hit.Flatness >= FloorFlatness ? _floor : _walls;

                stream = Samples(surface);
                db += surface?.ImpactDb ?? 0f;
                pitch *= surface?.ImpactPitch ?? 1f;
            }

            // the throw is heard through its first contact, so it leads slightly
            if (hit.IsFirst) db += 1.5f;

            // the settle layer, the last taps as it drops onto its face, driven by physics not a timer
            if (hit.Remaining < SettlingSpeed)
            {
                db -= 2.5f;
                pitch *= 0.93f;
            }

            return new DieSound(stream, db, pitch);
        }

        public DieSound Shaken(Die size, int tap, int taps)
        {
            // reuses the impact samples played small, fast and high (what a rattle is), so it can't drift out of character with them
            float through = taps <= 1 ? 1f : (float)tap / (taps - 1);

            return new DieSound(
                Samples(_floor),
                RattleDb + Mathf.Lerp(-3f, 2f, through),          // builds toward the release
                SizePitch(size) * 1.15f * Mathf.Lerp(0.96f, 1.05f, through));
        }
    }
}
