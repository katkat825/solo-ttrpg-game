using System;
using Godot;

namespace Game.Room
{
    // WHAT TIME IT IS WHERE THE PLAYER IS SITTING (AX6).
    //
    // The room's lighting follows the player's own clock and calendar. It is a small thing that does
    // something no amount of art does: open the game after dinner in November and the room is dark
    // and quiet, open it on a June afternoon and there is daylight in it. The table you are sitting at
    // is the table you are sitting at.
    //
    // DATE-BASED AND OFFLINE, both deliberate. The hour alone would make a summer evening as dark as
    // a winter one, which is exactly the thing that would read as wrong to anybody who has ever looked
    // out of a window in July; the date fixes it for the cost of one sine. And nothing is asked of the
    // network, ever: the machine's own clock is the whole input, so this works on a train and tells
    // nobody where you are.
    //
    // ONE LATITUDE, ASSUMED. Where the player actually is would need either a network call or a
    // permission prompt, and the difference between 45 and 55 degrees is a few minutes of dusk - so it
    // assumes a mid-northern latitude and says so. A player who wants a different room at a different
    // hour has the lamp; this is atmosphere, and it is never information.
    //
    // Pure and clamped, so a machine with its clock set to 3 a.m. on the 400th day of the year gets a
    // dark room rather than an exception.
    public enum Hour
    {
        Night,

        Dawn,

        Morning,

        Afternoon,

        Dusk,

        Evening,
    }

    public readonly struct Daylight
    {
        Daylight(Hour hour, float lamp, float ambient, float cool)
        {
            Is = hour;
            Lamp = lamp;
            Ambient = ambient;
            Cool = cool;
        }

        public Hour Is { get; }

        // WHAT THE SCENE'S OWN LAMP ENERGY IS MULTIPLIED BY, rather than an energy of its own: where
        // the lamp is and how bright it was set are the scene's, and were tuned by eye. This only
        // says how much of that is on.
        public float Lamp { get; }

        public float Ambient { get; }

        // how much daylight is mixed into the lamp's warmth. Nothing at night, most of it at noon
        public float Cool { get; }

        // through the window: cold north light, never the sun itself
        public static readonly Color Sky = new Color("#b7c9e0");

        // the assumed latitude, in degrees north. Not a setting: a setting for this would be asking
        // the player a question about astronomy to light a room
        public const float Latitude = 50f;

        // how long dawn and dusk last, either side of the sun's own crossing
        public const float Twilight = 1f;

        public static Daylight At(DateTime when) => At(when, Latitude);

        public static Daylight At(DateTime when, float latitude)
        {
            float hour = when.Hour + when.Minute / 60f;

            float half = HalfDay(when.DayOfYear, latitude);

            float rise = 12f - half;
            float set = 12f + half;

            if (hour < rise - Twilight || hour > set + Twilight * 2f)
                return new Daylight(Hour.Night, 1f, 0.55f, 0f);

            if (hour < rise + Twilight)
                return new Daylight(Hour.Dawn, 0.85f, 0.9f, 0.45f);

            if (hour < 12f)
                return new Daylight(Hour.Morning, 0.45f, 1.9f, 0.85f);

            if (hour < set - Twilight)
                return new Daylight(Hour.Afternoon, 0.5f, 1.7f, 0.75f);

            if (hour < set + Twilight)
                return new Daylight(Hour.Dusk, 0.9f, 0.85f, 0.3f);

            return new Daylight(Hour.Evening, 1f, 0.7f, 0f);
        }

        // half the length of the day, in hours. The standard sunrise equation, with the declination
        // from the day of the year - and clamped, because above the arctic circle the acos has no
        // answer and the honest one there is "all day" or "not at all"
        public static float HalfDay(int dayOfYear, float latitude)
        {
            int day = Math.Clamp(dayOfYear, 1, 366);

            float declination = Mathf.DegToRad(23.44f) *
                               Mathf.Sin(Mathf.Tau / 365f * (day - 81));

            float cosine = -Mathf.Tan(Mathf.DegToRad(latitude)) * Mathf.Tan(declination);

            if (cosine <= -1f) return 12f;

            if (cosine >= 1f) return 0f;

            return Mathf.RadToDeg(Mathf.Acos(cosine)) / 15f;
        }

        public bool IsDark => Is is Hour.Night or Hour.Evening;

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"{Is.ToString().ToLowerInvariant()}: lamp x{Lamp:0.00}, ambient x{Ambient:0.00}, " +
            $"{Cool * 100f:0}% daylight";
    }
}
