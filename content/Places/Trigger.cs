namespace Content.Places
{
    // closed vocabulary; an open string here would be a scripting hook, and content is data, not code
    public sealed class Trigger
    {
        public Trigger(When when, Then then, string place = null, string fact = null,
                       string sets = null)
        {
            WhenIt = when;
            ThenDo = then;
            Place = place ?? "";
            Fact = fact ?? "";
            Sets = sets ?? "";
        }

        public When WhenIt { get; }

        public Then ThenDo { get; }

        // empty for every verb but Goto
        public string Place { get; }

        // the fact watched; empty unless WhenIt is When.Fact
        public string Fact { get; }

        // the fact written; empty unless ThenDo is Set or Clear
        public string Sets { get; }

        public override string ToString()
        {
            string when = WhenIt.Word() + (Fact.Length > 0 ? " " + Fact : "");
            string then = ThenDo.Word() +
                          (Place.Length > 0 ? " " + Place : "") +
                          (Sets.Length > 0 ? " " + Sets : "");

            return $"{when} -> {then}";
        }
    }

    // one word each; Vocabulary lowercases the member name, so EndChapter becomes "endchapter"
    public enum Then
    {
        Next,

        Goto,

        Ends,

        Set,

        Clear,
    }

    public static class Thens
    {
        public static string Word(this Then then) => then.ToString().ToLowerInvariant();

        public static bool NeedsAPlace(this Then then) => then == Then.Goto;

        public static bool NeedsAFact(this Then then) => then is Then.Set or Then.Clear;
    }
}
