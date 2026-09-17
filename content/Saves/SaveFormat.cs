namespace Content.Saves
{
    public static class SaveFormat
    {
        // 2: the World layer. 'place' replaced 'encounter', and 'facts' arrived - the durable half a
        // place is rebuilt from. A format 1 save still reads: its encounter IS a place.
        public const int Current = 2;

        public const int Oldest = 1;

        public static bool CanRead(int format) => format >= Oldest && format <= Current;

        public static string Unfamiliar(int format) =>
            $"this save says it is format {format} and this build writes {Current} - it is read " +
            "as far as it can be, and anything this build does not recognise is listed below";
    }
}
