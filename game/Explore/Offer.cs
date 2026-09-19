using System.Collections.Generic;
using System.Linq;
using Content.Entities;
using Content.World;

namespace Game.Explore
{
    // HOW THE MOMENT IS PUT IN FRONT OF YOU. Not what a list looks like - there is no list.
    public enum Laid
    {
        Nothing,

        // things you do to a thing on the table, dealt out beside it
        Cards,

        // the DM slides a note across with the options on it, and takes it away again
        Note,
    }

    // WHAT THE MOMENT OFFERS, AND WHICH OBJECT CARRIES IT.
    //
    // This is the whole verb picker, and it is a decision rather than a widget: a handful of
    // things you could DO to what is in front of you are cards laid out beside it, and a set of
    // things somebody WROTE is a note the DM pushes across and takes back. Nothing here is a
    // standing surface - an offer is made, answered, and gone - which is the one guardrail that
    // keeps the note from being a menu wearing a costume.
    //
    // It computes the decision and holds no objects, so the rule can be read and tested without an
    // engine, and the objects that carry it are free to be redrawn without touching the rule.
    public sealed class Offer
    {
        // BEYOND THIS MANY, EVEN VERBS GO ON THE NOTE. A fan of cards past a handful stops being
        // things lying on a table and starts being a row of buttons. The five verbs and a
        // yes-or-no both fit under it; nothing in the vocabulary can exceed it by accident.
        public const int MostCards = 5;

        Offer(IReadOnlyList<Answer> answers, string about)
        {
            Answers = answers ?? new Answer[0];
            About = about ?? "";

            As = Answers.Count == 0 ? Laid.Nothing
               : Answers.All(a => a.IsACard) && Answers.Count <= MostCards ? Laid.Cards
               : Laid.Note;
        }

        public static readonly Offer Nothing = new Offer(null, "");

        public Laid As { get; }

        public IReadOnlyList<Answer> Answers { get; }

        // the entity, quest or conversation the offer is about
        public string About { get; }

        public bool Any => Answers.Count > 0;

        public Answer At(int index) =>
            index >= 0 && index < Answers.Count ? Answers[index] : null;


        // what somebody standing in front of you is offering. A foe on a pre-armed map offers
        // nothing: it is a statblock on a square, and what you do about it is a fight
        public static Offer From(Present it)
        {
            if (it == null || it.IsAFoe || it.Offers.Count == 0) return Nothing;

            var answers = new List<Answer>();

            foreach (Interaction verb in it.Offers) answers.Add(Answer.To(verb, it.Id));

            return new Offer(answers, it.Id);
        }

        // a quest on the table is a yes or a no, and both are cards
        public static Offer Offering(string quest)
        {
            if (string.IsNullOrEmpty(quest)) return Nothing;

            return new Offer(new[] { Answer.Accepting(quest), Answer.Declining(quest) }, quest);
        }

        // whatever the author wrote, in the author's own order
        public static Offer Written(string about, IEnumerable<Answer> options)
        {
            var answers = (options ?? new Answer[0]).ToArray();

            return answers.Length == 0 ? Nothing : new Offer(answers, about);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            As == Laid.Nothing
                ? "nothing offered"
                : $"{Answers.Count} on {As.ToString().ToLowerInvariant()}" +
                  (About.Length > 0 ? $" about '{About}'" : "") +
                  ": " + string.Join(", ", Answers);
    }
}
