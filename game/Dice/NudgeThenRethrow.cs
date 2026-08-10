// the default recovery policy, and the one M0-M3 were measured with
// tap a cocked die, throw it again if tapping fails, take what's showing if it is still wedged
// nudge first because a re-throw reads as the game correcting itself, a tap reads as a die toppling
// escapes walk the throw down to nothing, so recovery always terminates

public sealed class NudgeThenRethrow : IDieRecovery
{
    readonly int _maxNudges;
    readonly int _maxRethrows;
    readonly int _maxEscapes;

    public NudgeThenRethrow(int maxNudges, int maxRethrows, int maxEscapes)
    {
        _maxNudges = maxNudges;
        _maxRethrows = maxRethrows;
        _maxEscapes = maxEscapes;
    }

    public DieRecoveryStep Cocked(in CockedDie die)
    {
        if (die.NudgesSoFar < _maxNudges) return DieRecoveryStep.Nudge;
        if (die.RethrowsSoFar < _maxRethrows) return DieRecoveryStep.Rethrow();

        // taking the nearest face is wrong, looping forever is worse
        // DieBody logs loudly when this happens, so giving up too easily
        // shows in the log rather than quietly in the tally
        return DieRecoveryStep.Accept;
    }

    public DieRecoveryStep Escaped(in EscapedDie die)
    {
        if (die.EscapesSoFar > _maxEscapes) return DieRecoveryStep.Accept;

        return DieRecoveryStep.Rethrow(1f - (float)die.EscapesSoFar / _maxEscapes);
    }
}
