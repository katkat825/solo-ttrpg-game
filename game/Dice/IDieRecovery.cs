namespace Game.Dice
{
    public interface IDieRecovery
    {
        DieRecoveryStep Cocked(in CockedDie die);

        DieRecoveryStep Escaped(in EscapedDie die);
    }

    public enum DieRecoveryAction
    {
        // take the number as it stands - the only ending that always terminates
        Accept,

        Nudge,

        Rethrow,
    }

    public readonly struct DieRecoveryStep
    {
        public readonly DieRecoveryAction Action;

        // rethrow strength, 1 is a normal throw; at 0 the die drops inside the tray, so walking energy down always settles
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

    public readonly struct CockedDie
    {
        // the number nearest to showing, which Accept would take
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

    public readonly struct EscapedDie
    {
        // including this one - first escape of a throw is 1
        public readonly int EscapesSoFar;

        public readonly double FlightSeconds;

        public EscapedDie(int escapesSoFar, double flightSeconds)
        {
            EscapesSoFar = escapesSoFar;
            FlightSeconds = flightSeconds;
        }
    }
}
