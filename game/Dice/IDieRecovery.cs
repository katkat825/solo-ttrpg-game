// what to do about a throw that produced no readable number
// a die lands cocked on an edge, or a die leaves the tray
// both happen with real dice and neither is a physics bug
// the answer is a feel question, so it lives here and not in DieBody

public interface IDieRecovery
{
    // the die stopped, but not flat enough to trust the face
    DieRecoveryStep Cocked(in CockedDie die);

    // the die is outside the tray and will never come to rest on its own
    DieRecoveryStep Escaped(in EscapedDie die);
}

public enum DieRecoveryAction
{
    // take the number as it stands - the only ending that always terminates
    Accept,

    // tap it - cheap, and doesn't disturb dice that have already settled
    Nudge,

    Rethrow,
}

public readonly struct DieRecoveryStep
{
    public readonly DieRecoveryAction Action;

    // for Rethrow: how hard, 1 being a normal throw
    // at zero the die just drops from its spawn point, which is inside the tray
    // so a policy that walks the energy down to nothing always reaches a settle
    public readonly float Energy;

    DieRecoveryStep(DieRecoveryAction action, float energy)
    {
        Action = action;
        Energy = energy;
    }

    public static readonly DieRecoveryStep Accept = new(DieRecoveryAction.Accept, 0f);
    public static readonly DieRecoveryStep Nudge = new(DieRecoveryAction.Nudge, 0f);

    public static DieRecoveryStep Rethrow(float energy = 1f) => new(DieRecoveryAction.Rethrow, energy);
}

// a die at rest on something other than a face
public readonly struct CockedDie
{
    // the number nearest to showing - what Accept would take
    public readonly int Value;

    // how squarely that face points at the felt, as a dot product - 1.0 is flat
    public readonly float Alignment;

    // what this shape needs to score to count as flat - DieSolid.MinFlatAlignment
    public readonly float Required;

    public readonly int NudgesSoFar;
    public readonly int RethrowsSoFar;

    public CockedDie(int value, float alignment, float required, int nudgesSoFar, int rethrowsSoFar)
    {
        Value = value;
        Alignment = alignment;
        Required = required;
        NudgesSoFar = nudgesSoFar;
        RethrowsSoFar = rethrowsSoFar;
    }
}

// a die that has left the tray
public readonly struct EscapedDie
{
    // including this one - first escape of a throw is 1
    public readonly int EscapesSoFar;

    // diagnostic - a fast escape is a wall problem
    public readonly double FlightSeconds;

    public EscapedDie(int escapesSoFar, double flightSeconds)
    {
        EscapesSoFar = escapesSoFar;
        FlightSeconds = flightSeconds;
    }
}
