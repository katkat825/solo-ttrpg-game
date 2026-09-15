namespace Game.Dice
{
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

            // accept rather than loop forever when still wedged; DieBody logs it loudly
            return DieRecoveryStep.Accept;
        }

        public DieRecoveryStep Escaped(in EscapedDie die)
        {
            if (die.EscapesSoFar > _maxEscapes) return DieRecoveryStep.Accept;

            return DieRecoveryStep.Rethrow(1f - (float)die.EscapesSoFar / _maxEscapes);
        }
    }
}
