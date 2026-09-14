namespace Content.Schema
{
    // WHICH VERSION OF THE DATA FORMAT THIS BUILD SPEAKS (CONTENT_PIPELINE.md P4).
    //
    // Separate from `Core.EngineVersion`, and the two answer different questions:
    //
    //   the ENGINE version   what rules exist. A campaign that needs a Condition this build has
    //                        never heard of says so with `"engine"`, and is refused by name
    //   the FORMAT version   what the FILES look like. A campaign written when `inflicts` was a
    //                        single string rather than a list says so with `"format"`, and the
    //                        reader knows which shape to expect
    //
    // They move independently: a rule can arrive without changing a schema, and a schema can be
    // reshaped without any rule changing. Folding them into one number would mean every rule
    // addition invalidating every file, which is the thing the split is for.
    //
    // `Oldest` IS A PROMISE AND NOT A CONSTANT. Raising it abandons every campaign written against
    // the versions below it, so it moves only when a shape genuinely cannot be read any more - and
    // the honest way to avoid ever moving it is to keep the reader able to read the old shape,
    // which is the entire reason a version number is in the file at all.
    public static class ContentFormat
    {
        // 1 - the first published shape: monsters, items, maps, encounters and this manifest
        public const int Current = 1;

        public const int Oldest = 1;

        public static bool CanRead(int format) => format >= Oldest && format <= Current;

        // why not, as a sentence an author can act on. Two failures that look the same to a
        // version check are completely different things to the person reading the message: one
        // means update the game, the other means the campaign is older than anything this build
        // still reads
        public static string WhyNot(int format) =>
            format > Current
                ? $"this campaign is written for content format {format} and this build reads up " +
                  $"to {Current} - the game is older than the campaign, so update the game"
                : $"this campaign is written for content format {format} and this build no longer " +
                  $"reads anything below {Oldest}";
    }
}
