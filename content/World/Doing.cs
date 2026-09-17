using System.Collections.Generic;
using Content.Entities;

namespace Content.World
{
    public sealed class Doing : Happened
    {
        Doing(Interaction verb, string entity)
        {
            Verb = verb;
            Entity = entity ?? "";
        }

        public Interaction Verb { get; }

        public string Entity { get; }

        // a localization key the DM reads out, or empty
        public string Line { get; private set; } = "";

        // a node in the campaign's dialogue/ folder to play, or empty
        public string Node { get; private set; } = "";

        // non-null when this began a fight; the caller runs it and reports back with Fought
        public Episode Fight { get; private set; }

        readonly List<string> _took = new List<string>();

        public IReadOnlyList<string> Took => _took;

        public bool Refused => Why.Length > 0;

        // developer diagnostic; a refusal here means a caller bug, since a disallowed interaction is never offered
        public string Why { get; private set; } = "";

        public static Doing No(Interaction verb, string entity, string why) =>
            new Doing(verb, entity) { Why = why };

        public static Doing Did(Interaction verb, string entity) => new Doing(verb, entity);

        public Doing Saying(string key)
        {
            Line = key ?? "";
            return this;
        }

        public Doing Playing(string node)
        {
            Node = node ?? "";
            return this;
        }

        public Doing Starting(Episode episode)
        {
            Fight = episode;
            return this;
        }

        public Doing Taking(string item)
        {
            if (!string.IsNullOrEmpty(item)) _took.Add(item);
            return this;
        }

        public Doing Writing(string fact)
        {
            Add(fact);
            return this;
        }

        public override string ToString() =>
            Refused
                ? $"{Verb.Word()} {Entity}: no - {Why}"
                : $"{Verb.Word()} {Entity}" +
                  (Node.Length > 0 ? $", talking through {Node}" : "") +
                  (Line.Length > 0 ? $", reading {Line}" : "") +
                  (Fight != null ? $", and {Fight}" : "") +
                  (Took.Count > 0 ? $", took {string.Join(", ", Took)}" : "") +
                  Said();
    }
}
