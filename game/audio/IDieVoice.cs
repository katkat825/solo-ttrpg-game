using Godot;
using Core.Dice;

// what a die is made of, as far as the ear is concerned
// everything about how a material sounds lives behind here - which samples, how loud,
// what pitch, whether it makes a noise at all
// DieAudio knows how to play a DieSound and how to notice a collision, and nothing else
// adding brass is a new file and one assignment, not an edit to anything that plays a sound

public interface IDieVoice
{
    // return DieSound.Silence to say nothing
    DieSound Struck(in DieHit hit);

    // one tap of the shake before a throw - dice knocking together in a closed hand
    // tap counts from zero, so a voice can build the burst toward the release
    DieSound Shaken(Die size, int tap, int taps);
}

// one noise, ready to play: what, how loud, how fast
// the stream is usually an AudioStreamRandomizer, which is where sample-to-sample
// variation comes from for free - volume and pitch here sit on top of that randomness

public readonly struct DieSound
{
    // null is silence, and silence is a legitimate answer
    public readonly AudioStream Stream;

    // relative to the sample as recorded. negative is quieter, 0 is full
    public readonly float VolumeDb;

    // 1.0 plays the sample as recorded. below 1 is bigger and duller
    public readonly float PitchScale;

    public DieSound(AudioStream stream, float volumeDb, float pitchScale)
    {
        Stream = stream;
        VolumeDb = volumeDb;
        PitchScale = pitchScale;
    }

    public static DieSound Silence => default;

    public bool IsSilent => Stream == null;
}

// one collision, as physics saw it
// facts only - no opinion about what it should sound like, that belongs to the material
// Impulse and Speed are the same event divided differently, and both are here
// impulse scales with mass, which is why it drives loudness well
// speed is that impulse over the die's own mass, which is what a threshold wants

public readonly struct DieHit
{
    public readonly Die Size;

    // total contact impulse this step, newton-seconds. scales with mass
    public readonly float Impulse;

    // impulse over the die's mass, metres per second
    public readonly float Speed;

    // contact normal against up, absolute - 1 is the felt floor, 0 is a wooden wall
    // sign is thrown away deliberately: which side the normal points is a physics-engine
    // convention, and this is asking about the surface
    public readonly float Flatness;

    public readonly bool AgainstDie;

    // speed after the bounce, measured at the fastest-moving corner rather than the centre
    // a die can be barely drifting while spinning hard, and that is a tumble, not a settle
    public readonly float Remaining;

    // first contact of this throw - everything after it is heard relative to it
    public readonly bool IsFirst;

    public DieHit(Die size, float impulse, float speed, float flatness,
                  bool againstDie, float remaining, bool isFirst)
    {
        Size = size;
        Impulse = impulse;
        Speed = speed;
        Flatness = flatness;
        AgainstDie = againstDie;
        Remaining = remaining;
        IsFirst = isFirst;
    }
}
