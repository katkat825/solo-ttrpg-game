using System;
using Game.Room;

namespace Game.Tests
{
    // WHAT TIME IT IS WHERE THE PLAYER IS SITTING (AX6).
    //
    // The date is in it for one reason and the reason is testable: an hour-only model makes a June
    // evening as dark as a December one, which is the thing anybody who has looked out of a window in
    // summer would notice immediately.
    public class DaylightTests
    {
        static DateTime Midsummer(int hour) => new DateTime(2026, 6, 21, hour, 0, 0);

        static DateTime Midwinter(int hour) => new DateTime(2026, 12, 21, hour, 0, 0);

        [Fact]
        public void Nine_in_the_evening_is_daylight_in_June_and_night_in_December()
        {
            Assert.False(Daylight.At(Midsummer(21)).IsDark);
            Assert.True(Daylight.At(Midwinter(21)).IsDark);
        }

        [Fact]
        public void The_small_hours_are_dark_whatever_the_month()
        {
            // ONE IN THE MORNING, NOT THREE. At this latitude the sun is up before four in late
            // June and it is light before that, so three o'clock reading as dawn in midsummer is
            // the model being right rather than the model being wrong
            Assert.Equal(Hour.Night, Daylight.At(Midsummer(1)).Is);
            Assert.Equal(Hour.Night, Daylight.At(Midwinter(1)).Is);
        }

        [Fact]
        public void The_middle_of_the_day_is_daylight_whatever_the_month()
        {
            Assert.False(Daylight.At(Midsummer(12)).IsDark);
            Assert.False(Daylight.At(Midwinter(12)).IsDark);
        }

        [Fact]
        public void A_dark_room_is_lit_by_its_lamp_and_a_bright_one_is_not()
        {
            Daylight night = Daylight.At(Midwinter(23));
            Daylight noon = Daylight.At(Midsummer(13));

            Assert.True(night.Lamp > noon.Lamp);
            Assert.True(night.Ambient < noon.Ambient);

            // and no daylight is mixed into the lamp at midnight
            Assert.Equal(0f, night.Cool);
            Assert.True(noon.Cool > 0.5f);
        }

        [Fact]
        public void A_summer_day_is_longer_than_a_winter_one()
        {
            float june = Daylight.HalfDay(new DateTime(2026, 6, 21).DayOfYear, Daylight.Latitude);
            float december = Daylight.HalfDay(new DateTime(2026, 12, 21).DayOfYear,
                                              Daylight.Latitude);

            Assert.True(june > december + 2f);

            // and at this latitude the sun does rise and set, both times
            Assert.InRange(june, 6f, 12f);
            Assert.InRange(december, 0f, 6f);
        }

        [Fact]
        public void The_equinoxes_are_about_twelve_hours_of_daylight()
        {
            float march = Daylight.HalfDay(new DateTime(2026, 3, 21).DayOfYear, Daylight.Latitude);

            Assert.InRange(march * 2f, 11.5f, 12.5f);
        }

        [Fact]
        public void Above_the_arctic_circle_it_answers_all_day_or_not_at_all()
        {
            Assert.Equal(12f, Daylight.HalfDay(new DateTime(2026, 6, 21).DayOfYear, 80f));
            Assert.Equal(0f, Daylight.HalfDay(new DateTime(2026, 12, 21).DayOfYear, 80f));
        }

        [Fact]
        public void A_machine_with_its_clock_somewhere_impossible_still_gets_a_room()
        {
            // clamped into the year rather than thrown: day -40 is read as the first of January,
            // which above the arctic circle is a day the sun does not come up at all
            Assert.Equal(0f, Daylight.HalfDay(-40, 80f));

            Assert.InRange(Daylight.HalfDay(4000, Daylight.Latitude), 0f, 12f);

            Assert.InRange(Daylight.At(new DateTime(1, 1, 1, 0, 0, 0)).Lamp, 0.2f, 1.2f);
        }

        [Fact]
        public void Every_hour_of_every_month_gives_a_lamp_and_an_ambient_worth_having()
        {
            for (int month = 1; month <= 12; month++)
                for (int hour = 0; hour < 24; hour++)
                {
                    Daylight was = Daylight.At(new DateTime(2026, month, 15, hour, 30, 0));

                    Assert.InRange(was.Lamp, 0.2f, 1.2f);
                    Assert.InRange(was.Ambient, 0.2f, 3f);
                    Assert.InRange(was.Cool, 0f, 1f);
                }
        }
    }
}
