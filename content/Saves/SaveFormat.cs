namespace Content.Saves
{
    // WHAT SHAPE A SAVE FILE IS (CONTENT_PIPELINE.md P6).
    //
    // A third version number, and the third one is not one too many. `Core.EngineVersion` is what
    // rules exist, `Content.Schema.ContentFormat` is what a CAMPAIGN'S files look like, and this is
    // what a SAVE looks like. They move for different reasons and at different times: adding a
    // field to a save format does nothing to any published campaign, and reshaping a statblock
    // does nothing to anybody's saves.
    //
    // A SAVE IS NEVER REFUSED FOR ITS VERSION, which is the one way this differs from the campaign
    // format. A campaign written for a newer build is refused, because half-loading somebody
    // else's content produces a game that plays wrong. A save is the player's own afternoon, and
    // the worst thing that can be done with it is to decline to open it - so a save from a later
    // build is read as far as it can be and says what it did not understand.
    public static class SaveFormat
    {
        // 1 - the first shape: campaign and chapter, the hero, the room, and the felt
        public const int Current = 1;

        // WHY THERE IS NO `Oldest`. There is nothing to be oldest than yet, and adding the field
        // before there is a second version would be guessing at how the first migration will go.
        // The field a reader actually needs is the one in the FILE, and that is there from day one
        // - which is the whole of "version the format from day one"
        public static string Unfamiliar(int format) =>
            $"this save says it is format {format} and this build writes {Current} - it is read " +
            "as far as it can be, and anything this build does not recognise is listed below";
    }
}
