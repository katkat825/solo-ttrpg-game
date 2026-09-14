using Godot;

namespace Game.Campaigns
{
    // WHICH CAMPAIGN AND WHICH ENCOUNTER, ASKED FOR FROM OUTSIDE (CONTENT_PIPELINE.md P7).
    //
    //     --campaign=greyhollow --encounter=the_cistern
    //
    // THIS IS A P7 FINDING AND IT IS WRITTEN DOWN AS ONE. The second-campaign test is "author a
    // campaign entirely in data, and the diff of the codebase across authoring it is empty".
    // Authoring `greyhollow` needed no code at all - the schemas, the loader, the maps and the
    // validator took it whole on the first run. PLAYING it did: the campaign and the encounter
    // were named in `table.tscn` and nowhere else, so a second campaign could only be reached by
    // editing a scene. That is exactly the kind of boundary violation P7 exists to surface, and
    // this is the fix.
    //
    // THE SHELF IS STILL PHASE R, and this is not a substitute for it. A player will pick a
    // campaign off a shelf with box art on it; what this does is make "which campaign" a thing the
    // game is TOLD rather than a thing it is BUILT with, so the shelf, a check script and a
    // developer trying something all have one way in. When the shelf arrives it sets the same two
    // fields and this stays as the way a check drives it.
    //
    // NOTHING HERE VALIDATES. It reads two strings off the command line and hands them over; the
    // loader is what says whether that campaign is installed and whether it has such an encounter,
    // and it already says both, by name (Board.Named).
    public static class Requested
    {
        public const string CampaignArg = "--campaign=";

        public const string EncounterArg = "--encounter=";

        // empty when nothing was asked for, which is the ordinary case: the scene's own choice
        // stands, and a game launched by double-clicking it plays what it shipped with
        public static string Campaign => Value(CampaignArg);

        public static string Encounter => Value(EncounterArg);

        static string Value(string flag)
        {
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg.StartsWith(flag, System.StringComparison.Ordinal))
                    return arg.Substring(flag.Length).Trim();

            return "";
        }
    }
}
