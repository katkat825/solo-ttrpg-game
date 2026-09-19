using System;
using System.Collections.Generic;
using Core.Localization;

namespace Game.Fight
{
    // THE WORDS THE TURN ITSELF NEEDS: the marker you nudge to end yours, the note the DM pushes
    // when you have run out of actions and still hold it, and the line on the sheet saying what a
    // Nerve would buy you this instant.
    //
    // Engine strings under ui.*, beside the ones printed on the initiative card and the character
    // sheet: every campaign is fought a turn at a time and the turn belongs to nobody's campaign.
    //
    // THE NOTE'S ROWS AND THE SHEET'S LINE ARE DIFFERENT KEYS ON PURPOSE, and the reason is the
    // one the story log already made: a row on the note is an instruction you tick - Push on - and
    // the line on the sheet is a statement of what is true - a Nerve would buy one more action.
    // Reusing one for the other reads as machine-written in every language including this one.
    //
    // Derived from the Spend enum rather than listed, so a sixth thing a Nerve can buy fails the
    // locale audit until somebody has written the sentence for it.
    public static class TurnKeys
    {
        public const string Subject = "turn";

        // what the initiative marker IS, which is what a screen reader reads and what the hand
        // lands on: the thing you nudge when you are done
        public static string EndTurn => KeyConventions.Key(KeyConventions.UiNs, Subject, "end_turn");

        // the two rows on the note. "Push on - one more action for a Nerve, {0} left"
        public static string Push => KeyConventions.Key(KeyConventions.UiNs, Subject, "push");

        public static string Stop => KeyConventions.Key(KeyConventions.UiNs, Subject, "stop");

        // ui.nerve.heart_die - what the sheet says a Nerve is for right now
        public const string NerveSubject = "nerve";

        public static string Of(Spend spend) =>
            KeyConventions.Key(KeyConventions.UiNs, NerveSubject, spend.Word());

        public static IEnumerable<string> All()
        {
            yield return EndTurn;
            yield return Push;
            yield return Stop;

            foreach (Spend spend in Enum.GetValues<Spend>()) yield return Of(spend);
        }

        // the push row counts the Nerve left into it; nothing else here takes a number
        public static bool TakesAnArgument(string key) => key == Push;
    }
}
