namespace Content.Minis
{
    // THE MINI'S MOTION CONTRACT, AS AN ENUM (MINIS_AND_ART.md A1, ART_DIRECTION.md section 5).
    //
    // `THE_TABLE.md` section 7 lists the small set a piece on a table actually does, and the
    // engine already calls every one of them: `Mini.PlaceAt`, `Mini.Follow`, `Mini.Strike`, the
    // take-a-Condition wobble, `Mini.Topple`. What this phase adds is how a SUPPLIED model names
    // the clip that plays each one - so the set has to exist somewhere both the schema and the
    // loader can point at, and it is here.
    //
    // AN ENUM AND NOT A LIST OF STRINGS, for the reason `Vocabulary` gives about every other word
    // a campaign is allowed to use: the words a manifest may write are DERIVED from this, so
    // adding a sixth motion makes it authorable the same day and a schema listing the five by hand
    // would be a second description of this file, free to drift from it.
    //
    // IT IS DELIBERATELY SHORT AND IT IS DELIBERATELY NOT AN ANIMATION SYSTEM. A mini is not a
    // character in an action game - it is a painted figure a hand picks up, slides, leans with,
    // and lays on its side. Five is what a table does. Anything a pack author wants beyond these
    // is polish on top of one of them, not a sixth entry here (CONVENTIONS.md 5).
    public enum Motion
    {
        // stood on a square, by a hand, for the first time
        Placed,

        // carried to the next square - `Mini.Follow`
        Move,

        // reaching at something and coming back - `Mini.Strike`, and the same gesture as a refusal
        Strike,

        // took a Condition: a wobble where it stands, and nothing else moves
        Wobble,

        // down, and laid on its side where it fell - `Mini.Topple`
        Topple,
    }
}
