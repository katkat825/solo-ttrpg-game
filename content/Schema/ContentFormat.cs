namespace Content.Schema
{
    // Oldest is a promise: raising it abandons every campaign written below it, so move it only when a shape truly can't be read
    public static class ContentFormat
    {
        public const int Current = 1;

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
