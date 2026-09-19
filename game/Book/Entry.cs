using System;
using Content.Entities;
using Core.Localization;

namespace Game.Book
{
    // WHAT A LINE OF THE STORY SO FAR IS.
    //
    // A key and who it belongs to, never a sentence. The log is re-read in whatever language the
    // game is in - including one switched to after the thing happened - so an entry that had kept
    // the words would be a paragraph of frozen English in the middle of a translated book.
    public enum Recorded
    {
        // somebody spoke: a conversation line, a bark, a beat
        Said,

        // you did something to what was in front of you: one of the five verbs
        Did,

        // the quest ledger. Derived from facts rather than appended, so it survives a reload
        Accepted,

        Completed,

        Failed,
    }

    public static class Recordings
    {
        public static string Word(this Recorded what) => what.ToString().ToLowerInvariant();

        public const string Subject = "story";

        // how the line reads, as ONE KEY PER WHOLE SENTENCE with the part that varies going in as a
        // placeholder. "You searched {0}." is one key; it is never "You " plus a verb plus a name,
        // because word order differs by language and a sentence assembled from three translated
        // fragments cannot be translated at all.
        public static string Reading(this Recorded what) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, Word(what));

        public const string VerbSubject = "story_verb";

        // AND THAT IS WHY A VERB HAS ITS OWN SENTENCE. The card in front of you says "Search it",
        // which is an instruction; the log says "You searched the lockbox", which is a record. The
        // same word cannot be both, and reusing the card's would have printed "You Search it."
        public static string Reading(this Interaction verb) =>
            KeyConventions.Key(KeyConventions.UiNs, VerbSubject, Interactions.Word(verb));

        // a log with nothing in it yet, which is what a book looks like on the first evening
        public static string Empty =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, "empty");

        public static System.Collections.Generic.IEnumerable<string> Keys()
        {
            foreach (Recorded what in Enum.GetValues<Recorded>())
            {
                // an action is read back in the verb's own words, so there is no ui.story.did
                if (what == Recorded.Did) continue;

                yield return Reading(what);
            }

            foreach (Interaction verb in Enum.GetValues<Interaction>())
                yield return Reading(verb);

            yield return Empty;
        }
    }

    public sealed class Entry
    {
        public Entry(Recorded what, string subject, string key, string sentence = null)
        {
            What = what;
            Subject = subject ?? "";
            Key = key ?? "";
            Sentence = string.IsNullOrEmpty(sentence) ? what.Reading() : sentence;
        }

        public Recorded What { get; }

        // who spoke, or which quest, or what was acted on. A key's own campaign scoping is already
        // in Key; this is the id, for grouping and for a bookcase to sort by
        public string Subject { get; }

        // the key of whatever goes in the sentence's {0}: the line that was spoken, the
        // quest's title, the name of the thing you searched
        public string Key { get; }

        // the key of the sentence the line is read back as. Usually the kind's own; for an action
        // it is the verb's, because "searched" and "opened" are different sentences
        public string Sentence { get; }

        public string Reads => Sentence;

        public bool IsAQuest => What is Recorded.Accepted or Recorded.Completed or Recorded.Failed;

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"{What.Word()} {Subject}: {Key}";
    }
}
