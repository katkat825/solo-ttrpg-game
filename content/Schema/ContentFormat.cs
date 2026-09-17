namespace Content.Schema
{
    // Oldest is a promise: raising it abandons every campaign written below it, so move it only when a shape truly can't be read
    public static class ContentFormat
    {
        // 2 is the World layer: places/ instead of encounters/, chapters[].places instead of
        // chapters[].encounters, and entities/, quests/ and roads/ beside them. A format 1
        // campaign still loads, because its encounters ARE places whose standings are all foes -
        // which is the compatibility test the whole reframe had to pass
        // (PLACES_AND_PERSISTENCE.md section 2).
        public const int Current = 2;

        public const int Oldest = 1;

        public static bool CanRead(int format) => format >= Oldest && format <= Current;

        // two directions that look the same to a version check but need different sentences
        public static string WhyNot(int format) =>
            format > Current
                ? $"this campaign is written for content format {format} and this build reads up " +
                  $"to {Current} - the game is older than the campaign, so update the game"
                : $"this campaign is written for content format {format} and this build no longer " +
                  $"reads anything below {Oldest}";
    }
}
