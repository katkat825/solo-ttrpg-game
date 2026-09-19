using System.Collections.Generic;
using Content.Entities;

namespace Game.Explore
{
    // What one answer to a moment IS, which decides how it is put in front of you.
    public enum Answering
    {
        // one of the five the engine knows how to carry out
        Verb,

        Accept,

        Decline,

        // a line the author wrote. A choice in a conversation, a way the scene may be answered -
        // anything that is words rather than a thing done to a thing on the table
        Option,
    }

    // ONE THING YOU COULD DO ABOUT THE MOMENT IN FRONT OF YOU.
    //
    // It carries a KEY and never words: whatever lays it out resolves that through the localizer,
    // the same as a bubble and a card already do.
    //
    // An answer you cannot take is still an answer. It is offered closed rather than hidden,
    // because knowing what you cannot do is information - which is the same call the dialogue
    // choice cards already made, and for the same reason.
    public sealed class Answer
    {
        Answer(Answering answering, string key, string about, bool open,
               IReadOnlyList<object> counting = null)
        {
            Is = answering;
            Key = key ?? "";
            About = about ?? "";
            Open = open;
            Counting = counting ?? System.Array.Empty<object>();
        }

        public Answering Is { get; }

        public string Key { get; }

        // what it is an answer about: an entity id, a quest id, or a node in the dialogue book
        public string About { get; }

        public bool Open { get; }

        // WHAT GOES IN THE {0}. A line saying how much of something you have left is one key with
        // a number in it, never a sentence with a number glued on the end - so the number travels
        // with the answer and whatever lays it out formats rather than concatenates.
        public IReadOnlyList<object> Counting { get; }

        // set for Answering.Verb and meaningless otherwise
        public Interaction Verb { get; private set; }

        public static Answer To(Interaction verb, string entity, bool open = true) =>
            new Answer(Answering.Verb, VerbKeys.Of(verb), entity, open) { Verb = verb };

        public static Answer Accepting(string quest) =>
            new Answer(Answering.Accept, VerbKeys.Accept, quest, true);

        public static Answer Declining(string quest) =>
            new Answer(Answering.Decline, VerbKeys.Decline, quest, true);

        // the author's own key, for the author's own line
        public static Answer Written(string key, string about, bool open = true,
                                     params object[] counting) =>
            new Answer(Answering.Option, key, about, open, counting);

        // a verb and a yes-or-no are things you do to what is on the table, and go on cards; words
        // somebody wrote go on the DM's note, because the DM is the one who wrote them
        public bool IsACard => Is != Answering.Option;

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{Is.ToString().ToLowerInvariant()} {Key}" +
            (About.Length > 0 ? $" ({About})" : "") +
            (Open ? "" : " - closed");
    }
}
