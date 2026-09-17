using System;
using System.Linq;

namespace Content.Places
{
    public enum Gesture
    {
        // hesitation is a flag on the cue, not a gesture of its own
        Place,

        Slide,

        Push,

        Tap,

        ReachBehind,

        Rest,

        Withdraw,


        Tack,

        TurnPage,

        Write,


        // named rather than left as the absence of a gesture
        Idle,
    }

    public static class Gestures
    {
        // derived here, not a field an author could set to disagree
        public static bool Tells(this Gesture gesture) =>
            gesture is Gesture.Push or Gesture.Tack or Gesture.TurnPage or Gesture.Write;

        // only a placement; the reader refuses hesitant on anything else rather than ignoring it
        public static bool CanHesitate(this Gesture gesture) => gesture == Gesture.Place;

        public static bool NeedsASquare(this Gesture gesture) =>
            gesture is Gesture.Place or Gesture.Tap or Gesture.Slide;

        public static string Word(this Gesture gesture) =>
            gesture.ToString().ToLowerInvariant();

        public static bool Has(string word) =>
            word != null && Enum.GetNames<Gesture>()
                                .Any(n => string.Equals(n, word, StringComparison.OrdinalIgnoreCase));
    }
}
