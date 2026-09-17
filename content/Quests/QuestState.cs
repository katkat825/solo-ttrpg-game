namespace Content.Quests
{
    // derived from facts every time it is asked, never stored, so it cannot disagree
    public enum QuestState
    {
        Unknown,

        Offered,

        Active,

        Done,

        Failed,
    }

    public static class QuestStates
    {
        public static string Word(this QuestState state) => state.ToString().ToLowerInvariant();

        // what the log shows; the ones you have not been offered are not news
        public static bool InTheLog(this QuestState state) => state != QuestState.Unknown;

        public static bool Settled(this QuestState state) =>
            state is QuestState.Done or QuestState.Failed;
    }
}
