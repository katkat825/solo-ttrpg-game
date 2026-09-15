using Godot;
using Core.Dice;

namespace Game.Audio
{
    public interface IDieVoice
    {
        // return DieSound.Silence to say nothing
        DieSound Struck(in DieHit hit);

        // one tap of the pre-throw shake; tap counts from zero so a voice can build toward the release
        DieSound Shaken(Die size, int tap, int taps);
    }

    // one noise ready to play; the stream is usually an AudioStreamRandomizer, so sample variation is free and volume/pitch sit on top

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

    // one collision as physics saw it, facts only
    // Impulse and Speed are the same event divided differently: impulse scales with mass (drives loudness), speed is impulse over mass (what a threshold wants)

    public readonly struct DieHit
    {
        public readonly Die Size;

        // total contact impulse this step, newton-seconds. scales with mass
        public readonly float Impulse;

        // impulse over the die's mass, metres per second
        public readonly float Speed;

        // contact normal against up, absolute - 1 is the felt floor, 0 a wall; the sign is dropped because it's a physics-engine convention, not about the surface
        public readonly float Flatness;

        public readonly bool AgainstDie;

        // speed after the bounce, at the fastest-moving corner: a die can drift slowly while spinning hard, which is a tumble not a settle
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
}
