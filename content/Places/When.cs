namespace Content.Places
{
    // shared by triggers and cues, two things an author hangs on the same instant
    public enum When
    {
        Entered,

        Cleared,

        Fact,
    }

    public static class Moments
    {
        public static string Word(this When when) => when.ToString().ToLowerInvariant();

        // only one of them needs a fact named beside it, and the reader insists on exactly that
        public static bool NeedsAFact(this When when) => when == When.Fact;
    }
}
