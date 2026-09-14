using System;

namespace Core
{
    // WHAT BUILD OF THE RULES THIS IS, and the only number in the project a campaign is allowed to
    // have an opinion about.
    //
    // `CONTENT_PIPELINE.md` P4 puts a MINIMUM ENGINE VERSION in every `campaign.json`, for one
    // reason: "a 2026 campaign still loads in a 2028 build, and the game refuses a too-new one
    // legibly rather than half-loading it". A campaign written against a rule that did not exist
    // yet has to be able to say so, or the failure is a monster with a behaviour nothing
    // recognises and a shelf entry that plays wrong instead of not at all.
    //
    // IN `core/` BECAUSE core IS THE ENGINE. The version of the rules is a property of the rules,
    // not of the loader that reads a manifest or of the Godot project that draws them - and it has
    // to be readable from a headless test and a simulation, which is the same argument every other
    // line in this folder makes.
    //
    // MOVE IT WHEN A RULE CHANGES, not when a file does. The minor number goes up when something a
    // campaign could depend on arrives - a new `Tile`, a new `Condition`, a behaviour, a field in a
    // schema that does something. The major goes up when one is taken away or changed underneath a
    // campaign that was using it, which is the event this number exists to survive.
    public static class EngineVersion
    {
        // 0.4 - the phases that are built: B the board, E edge walls, C the combat loop, and P the
        // content pipeline in progress. Pre-1.0 on purpose: nothing has shipped, and a campaign
        // written against 0.x is written against a thing that is still moving
        public static readonly Version Current = new Version(0, 4);

        // A CAMPAIGN ASKING FOR MORE THAN THIS IS REFUSED, and one asking for less is played. That
        // is the whole rule, and it is stated as a method so the comparison is written once rather
        // than in the loader, the validator and whatever asks next
        public static bool Satisfies(Version wanted) => wanted == null || wanted <= Current;
    }
}
