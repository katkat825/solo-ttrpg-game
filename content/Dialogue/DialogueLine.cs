namespace Content.Dialogue
{
    // one line of a conversation, as the compiler found it and as the locale will be asked for it.
    // The words are NOT here: the .yarn source holds the author's working text and the campaign's
    // locale/ holds what a player reads, in every language including English (CONVENTIONS section 7).
    public sealed class DialogueLine
    {
        public DialogueLine(string yarnId, string key, string speaker, string node, string file,
                            int line, string source)
        {
            YarnId = yarnId ?? "";
            Key = key ?? "";
            Speaker = speaker ?? "";
            Node = node ?? "";
            File = file ?? "";
            Line = line;
            Source = source ?? "";
        }

        // what the runtime calls it: "line:the_hinges_are_new"
        public string YarnId { get; }

        // what the localizer calls it: "dialogue.wolf.line.greyhollow.the_hinges_are_new"
        public string Key { get; }

        // the creature saying it - the node's speaker, or this line's own #speaker: override
        public string Speaker { get; }

        public string Node { get; }

        // for a problem to point at, campaign-relative
        public string File { get; }

        public int Line { get; }

        // the author's working text. Never shown: a line that reached the screen from here would be
        // English hardcoded in a data file, which is the one thing the pseudolocale cannot find.
        public string Source { get; }

        public override string ToString() => $"{Key} ({File}:{Line})";
    }
}
