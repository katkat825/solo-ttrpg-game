using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Game.Access
{
    // "WHAT WAS I DOING? WHERE ARE WE?" (AX5).
    //
    // Askable at any point, answered in character, and it costs nothing new: the place you are in and
    // the errand you are on are both already written down - the place by whatever is being walked, the
    // errand derived from the fact store - so this is the two of them read back to you and a pointer
    // at the book where the rest of it is.
    //
    // It is the re-orientation every returning player needs, and it doubles as the onboarding this
    // game otherwise has none of. It is also the thing a blind player needs most, which is why it is
    // built as SENTENCES rather than as a readout: the same Spoken goes to the narrator and to the
    // DM's note, so there is one answer to "where are we" rather than a spoken one and a printed one
    // that could disagree.
    //
    // SECOND PERSON, LIKE EVERYTHING ELSE ADDRESSED TO YOU. The player has no gender and is never
    // referred to in the third person, and check-voice holds these keys to it along with the rest.
    public static class Whereabouts
    {
        public const string Subject = "orient";

        // "You are at {0}." - the place's own name counted in
        public static string Here => KeyConventions.Key(KeyConventions.UiNs, Subject, "here");

        // nothing on the table yet: a cold room, before a book has been opened
        public static string Nowhere => KeyConventions.Key(KeyConventions.UiNs, Subject, "nowhere");

        // "The errand you are on is {0}." - the quest's own title counted in
        public static string Errand => KeyConventions.Key(KeyConventions.UiNs, Subject, "errand");

        public static string NoErrand =>
            KeyConventions.Key(KeyConventions.UiNs, Subject, "no_errand");

        // and where the rest of it is written down, which is the half that makes this orientation
        // rather than a status line
        public static string Log => KeyConventions.Key(KeyConventions.UiNs, Subject, "log");

        public static IEnumerable<string> Keys()
        {
            yield return Here;
            yield return Nowhere;
            yield return Errand;
            yield return NoErrand;
            yield return Log;
        }

        // the two that count a name in
        public static bool TakesAnArgument(string key) => key == Here || key == Errand;

        // WHERE AND WHAT, out of keys that have already been chosen by whoever knows the world. It
        // takes the keys rather than the words so that a place with no name in this locale is a
        // visible bug rather than a sentence with a hole in it.
        public static Spoken Of(ILocalizer text, string placeKey, string questKey)
        {
            if (text == null) return Spoken.Nothing;

            var lines = new List<string>
            {
                string.IsNullOrWhiteSpace(placeKey)
                    ? text.Get(Nowhere)
                    : text.Format(Here, text.Get(placeKey)),
            };

            // a cold room is not carrying an errand and has no log to be pointed at either: being
            // told twice that there is nothing is worse than being told once
            if (string.IsNullOrWhiteSpace(placeKey)) return Spoken.Urgently(lines.ToArray());

            lines.Add(string.IsNullOrWhiteSpace(questKey)
                ? text.Get(NoErrand)
                : text.Format(Errand, text.Get(questKey)));

            lines.Add(text.Get(Log));

            return Spoken.Urgently(lines.ToArray());
        }
    }
}
