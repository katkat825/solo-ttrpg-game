namespace Core.Resolution
{
    // the ladder every check is measured against
    // numbers come from exact enumeration, not estimates
    // a starting hero (d8+d6+d6) caps at 14, so 15 and up are unreachable
    // that is a gate, not bad luck - use 15+ to wall content off
    public static class Difficulty
    {
        public const int Easy = 7;
        public const int Standard = 9;
        public const int Tricky = 11;
        public const int Hard = 13;
        public const int Formidable = 15;
        public const int Legendary = 18;
    }
}
