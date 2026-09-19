using System;
using System.Collections.Generic;
using Content.Entities;
using Core.Localization;

namespace Game.Explore
{
    // THE WORDS ON A CARD THE MOMENT LAYS OUT.
    //
    // The verbs are the engine's and closed, so their words are the engine's too and live beside
    // the ones printed on the character sheet - every campaign is played with the same five, and a
    // campaign that wanted a sixth would be asking for engine work rather than for a string.
    //
    // Derived from the enum rather than listed, so a sixth verb fails the locale audit until
    // somebody has written the word for it.
    public static class VerbKeys
    {
        public const string Subject = "verb_card";

        // the two that are not verbs but are laid out the same way: a quest offered is answered
        // with a card, not with a list
        public const string AcceptWord = "accept";

        public const string DeclineWord = "decline";

        public static string Of(Interaction verb) =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, verb.Word());

        public static string Accept => KeyConventions.Key(KeyConventions.UiNs, Subject, AcceptWord);

        public static string Decline => KeyConventions.Key(KeyConventions.UiNs, Subject, DeclineWord);

        public static IEnumerable<string> All()
        {
            foreach (Interaction verb in Enum.GetValues<Interaction>()) yield return Of(verb);

            yield return Accept;
            yield return Decline;
        }
    }
}
