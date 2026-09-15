namespace Content.Saves
{
    public static class SaveFormat
    {
        public const int Current = 1;

        // no Oldest yet: nothing to be older than, and the version a reader needs is in the file
        public static string Unfamiliar(int format) =>
            $"this save says it is format {format} and this build writes {Current} - it is read " +
            "as far as it can be, and anything this build does not recognise is listed below";
    }
}
