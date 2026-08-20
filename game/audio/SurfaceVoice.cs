using Godot;
using Core.Dice;

// how a die sounds when the SURFACE decides, which is the honest model of an impact:
// what gets struck dominates, and the thing striking it only modulates
//
// NOT named for a die material, and deliberately so. This class never asks what the die
// is made of - it reads the tray's floor and walls, picks between them on DieHit.Flatness,
// and shapes the result by force and by die SIZE. An earlier name of WoodenDice implied a
// wooden die; the wood was always the tray, and the dice have been onyx the whole time.
//
// IDieVoice stays the seam for die material, because that is a real thing an ear can hear -
// a brass die on felt is not a stone one. It just needs its own sample sets to say so, and
// until those exist there is nothing honest for a per-material voice to do. When they
// arrive, BrassVoice sits beside this one and DiceTray hands it over instead.
//
// samples are single impacts sliced out of CC0 recordings by tools/slice_impacts.py,
// see THIRD_PARTY.md for sources and licences

public sealed class SurfaceVoice : IDieVoice
{
    // what a die falls back to when nobody has told it where it is
    // every die in the fairness sweep, and any die dropped into a scene with no tray
    public static readonly SurfaceVoice Shared = new(null);

    // the speed at which a hit is as loud as the sample gets
    // above it extra force stops adding volume and only adds brightness,
    // which is what keeps a hard throw from clipping
    const float FullForce = 2.5f;

    // exponent on the force, lifting soft hits toward hard ones
    // 1 is the honest linear map and it is wrong - a die's last taps carry a fiftieth of the
    // momentum of its first bounce, so the whole tumble vanishes under the impact that started it
    const float ForceCurve = 0.75f;

    const float SettlingSpeed = 0.8f;   // below this the die is sitting down, not bouncing

    // above this Flatness the die hit the floor, below it a wall
    // a hard split rather than a blend, because what is being chosen is a folder of samples
    // and there is no halfway between two recordings
    const float FloorFlatness = 0.5f;

    // two dice meeting: sharper and smaller than either of them hitting the tray
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

    // larger solids are heavier, land on more surface and ring lower
    // this is the ONLY thing about the die itself that reaches the sound
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
        // amplitude rather than energy - amplitude is what a fader is measured in and what
        // the ear reports; squaring it makes soft taps vanish before they get quiet
        float force = Mathf.Clamp(hit.Speed / FullForce, 0f, 1f);

        float db = Mathf.LinearToDb(Mathf.Pow(Mathf.Max(force, 0.02f), ForceCurve));

        // harder is also brighter - a real die struck hard rings up as well as louder
        // without this a heavy throw is the same tap with the fader raised
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

        // the last few taps as it drops onto its face
        // the settle layer, arrived at from the physics rather than from a timer
        if (hit.Remaining < SettlingSpeed)
        {
            db -= 2.5f;
            pitch *= 0.93f;
        }

        return new DieSound(stream, db, pitch);
    }

    public DieSound Shaken(Die size, int tap, int taps)
    {
        // not its own recording - the impact samples played small, fast and high, which is
        // what a rattle physically is, and so it can never drift out of character with them
        float through = taps <= 1 ? 1f : (float)tap / (taps - 1);

        return new DieSound(
            Samples(_floor),
            RattleDb + Mathf.Lerp(-3f, 2f, through),          // builds toward the release
            SizePitch(size) * 1.15f * Mathf.Lerp(0.96f, 1.05f, through));
    }
}
