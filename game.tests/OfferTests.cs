using System.Collections.Generic;
using System.Linq;
using Content.Entities;
using Content.World;
using Core.Localization;
using Core.Space;
using Game.Explore;

namespace Game.Tests
{
    // THE VERB PICKER, WHICH IS NOT A PICKER.
    //
    // The rule under the whole response system is one sentence: a handful of things you could DO to
    // what is in front of you are cards laid out beside it, and a set of things somebody WROTE is a
    // note the DM pushes across. This file is that sentence, held to.
    //
    // The guardrail matters as much as the rule. Nothing here is a standing surface - an offer is
    // made, answered and gone - and the way that stays true is that an offer with nothing in it is
    // an offer that lays nothing out at all.
    public class OfferTests
    {
        static Present Somebody(params Interaction[] verbs) =>
            new Present(1, new Cell(1, 1), "norrel",
                        new Entity("quay.norrel", "", "", Can(verbs), null),
                        verbs);

        static Dictionary<Interaction, string> Can(IEnumerable<Interaction> verbs) =>
            verbs.ToDictionary(v => v, v => "");

        // a statblock on a square, which is what a foe on a pre-armed map is
        static Present AFoe() => new Present(2, new Cell(2, 2), "marsh_jack", null, null);


        [Fact]
        public void NothingOfferedLaysNothingOut()
        {
            Assert.Equal(Laid.Nothing, Offer.Nothing.As);
            Assert.False(Offer.Nothing.Any);
            Assert.Empty(Offer.Nothing.Answers);

            Assert.Equal(Laid.Nothing, Offer.From(null).As);
            Assert.Equal(Laid.Nothing, Offer.Offering("").As);
            Assert.Equal(Laid.Nothing, Offer.Written("x", null).As);
        }

        // a foe is a statblock standing on a map, and what you do about one is a fight
        [Fact]
        public void AFoeOffersNothing()
        {
            Assert.Equal(Laid.Nothing, Offer.From(AFoe()).As);
        }

        [Fact]
        public void SomebodysVerbsAreCards()
        {
            Offer offer = Offer.From(Somebody(Interaction.Talk, Interaction.Examine));

            Assert.Equal(Laid.Cards, offer.As);
            Assert.Equal(2, offer.Answers.Count);
            Assert.All(offer.Answers, a => Assert.Equal(Answering.Verb, a.Is));
            Assert.Equal("norrel", offer.About);
        }

        [Fact]
        public void TheCardsAreInTheOrderTheyWereOffered()
        {
            Offer offer = Offer.From(Somebody(Interaction.Search, Interaction.Talk));

            Assert.Equal(new[] { Interaction.Search, Interaction.Talk },
                         offer.Answers.Select(a => a.Verb).ToArray());
        }

        // the whole vocabulary fits on cards, which is the point of capping it rather than the cap
        // being a number somebody will have to argue with later
        [Fact]
        public void EveryVerbAtOnceStillFitsOnCards()
        {
            Offer offer = Offer.From(Somebody(System.Enum.GetValues<Interaction>()));

            Assert.Equal(Laid.Cards, offer.As);
            Assert.Equal(5, offer.Answers.Count);
            Assert.True(offer.Answers.Count <= Offer.MostCards);
        }

        [Fact]
        public void AQuestIsAYesAndANo_AndBothAreCards()
        {
            Offer offer = Offer.Offering("the_lockbox");

            Assert.Equal(Laid.Cards, offer.As);
            Assert.Equal(new[] { Answering.Accept, Answering.Decline },
                         offer.Answers.Select(a => a.Is).ToArray());
            Assert.All(offer.Answers, a => Assert.Equal("the_lockbox", a.About));
        }

        // WORDS SOMEBODY WROTE GO ON THE DM'S NOTE, because the DM is the one who wrote them
        [Fact]
        public void WrittenOptionsGoOnTheNote()
        {
            Offer offer = Offer.Written("norrel_hello", new[]
            {
                Answer.Written("dialogue.norrel.line.quay.001", "norrel_hello"),
                Answer.Written("dialogue.norrel.line.quay.002", "norrel_hello"),
            });

            Assert.Equal(Laid.Note, offer.As);
            Assert.Equal(2, offer.Answers.Count);
        }

        // A FAN OF CARDS PAST A HANDFUL IS A ROW OF BUTTONS. Nothing in the closed vocabulary can
        // reach this, which is why the cap is a guard rather than a design choice
        [Fact]
        public void TooManyForAHandfulGoOnTheNoteInstead()
        {
            var many = new List<Answer>();

            for (int at = 0; at <= Offer.MostCards; at++) many.Add(Answer.Accepting("q" + at));

            Assert.Equal(Laid.Note, Offer.Written("too many", many).As);
        }

        [Fact]
        public void OneWrittenLineAmongCardsPutsTheWholeThingOnTheNote()
        {
            Offer offer = Offer.Written("mixed", new[]
            {
                Answer.To(Interaction.Talk, "norrel"),
                Answer.Written("quest.quay.the_lockbox.title", "the_lockbox"),
            });

            Assert.Equal(Laid.Note, offer.As);
        }


        // ---- the answers themselves -----------------------------------------------------------

        [Fact]
        public void AnAnswerCarriesAKeyAndNeverWords()
        {
            Answer answer = Answer.To(Interaction.Examine, "norrel");

            Assert.Equal("ui.verb_card.examine", answer.Key);
            Assert.True(KeyConventions.IsWellFormed(answer.Key));
        }

        [Fact]
        public void EveryVerbsWordObeysTheGrammar()
        {
            foreach (string key in VerbKeys.All())
                Assert.True(KeyConventions.IsWellFormed(key), key + " is not a well-formed key");
        }

        [Fact]
        public void TheVerbWordsAreDerivedFromTheEnum_NeverListed()
        {
            Assert.Equal(System.Enum.GetValues<Interaction>().Length + 2, VerbKeys.All().Count());

            foreach (Interaction verb in System.Enum.GetValues<Interaction>())
                Assert.Contains(verb.Word(), VerbKeys.Of(verb));
        }

        // AN ANSWER YOU CANNOT TAKE IS STILL AN ANSWER. Knowing what you cannot do is information,
        // which is the call the dialogue cards already made
        [Fact]
        public void AClosedAnswerIsOfferedRatherThanHidden()
        {
            Answer shut = Answer.To(Interaction.Open, "shed", open: false);

            Offer offer = Offer.Written("shed", new[] { Answer.To(Interaction.Talk, "norrel"), shut });

            Assert.Equal(2, offer.Answers.Count);
            Assert.False(offer.Answers[1].Open);
        }

        // V2. The turn that has run out of actions asks two written questions and one of them has
        // to say how much Nerve is in hand. A count glued onto the end of a sentence is a sentence
        // that cannot be translated, so the number travels with the answer and whatever lays it
        // out formats it in.
        [Fact]
        public void AnAnswerCarriesItsNumberRatherThanHavingOneGluedOnTheEnd()
        {
            Answer pushing = Answer.Written("ui.turn.push", "out_of_actions", open: true, 2);

            Assert.Equal(new object[] { 2 }, pushing.Counting);

            Answer stopping = Answer.Written("ui.turn.stop", "out_of_actions");

            Assert.Empty(stopping.Counting);
        }

        // two things somebody wrote down go on the DM's note, and the note is the one object at
        // this table that is not a menu wearing a costume
        [Fact]
        public void ThePushOrStopIsANoteAndNotAFanOfCards()
        {
            Offer offer = Offer.Written("out_of_actions", new[]
            {
                Answer.Written("ui.turn.push", "out_of_actions", open: false, 0),
                Answer.Written("ui.turn.stop", "out_of_actions"),
            });

            Assert.Equal(Laid.Note, offer.As);
            Assert.False(offer.At(0).Open);
            Assert.True(offer.At(1).Open);
        }

        [Fact]
        public void AnIndexOutsideTheOfferIsNothing()
        {
            Offer offer = Offer.Offering("the_lockbox");

            Assert.Null(offer.At(-1));
            Assert.Null(offer.At(2));
            Assert.NotNull(offer.At(0));
        }
    }
}
