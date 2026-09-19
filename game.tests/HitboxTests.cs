using Godot;
using Game.Access;

namespace Game.Tests
{
    // HOW BIG THE THING YOU REACH FOR HAS TO BE (AX1).
    //
    // The rule is a floor rather than a formula, and the two things worth holding it to are the two
    // that would break the room if it were a formula instead: that it never grows something already
    // big enough, and that it never pads a thickness.
    public class HitboxTests
    {
        [Fact]
        public void A_thing_too_small_to_hit_is_grown_to_a_fingertip()
        {
            // a nerve chip: 8mm across and 1.6mm thick
            Vector3 span = Hitbox.Around(new Vector3(0.0084f, 0.0016f, 0.0084f));

            Assert.Equal(Hitbox.Least, span.X, 5);
            Assert.Equal(Hitbox.Least, span.Z, 5);
            Assert.True(Hitbox.Generous(span));
        }

        [Fact]
        public void A_thing_already_big_enough_is_left_exactly_as_it_was()
        {
            // the door: 2 metres of it, and a body grown past that would go through the ceiling and
            // shadow everything behind it, because a raycast returns the nearest thing it hits
            var door = new Vector3(0.06f, 2.0f, 0.85f);

            Assert.Equal(door, Hitbox.Around(door));
        }

        [Fact]
        public void The_thickness_is_never_padded()
        {
            // a line on a page: as wide as the page, 26mm tall, and 4mm of nothing
            Vector3 span = Hitbox.Around(new Vector3(0.142f, 0.026f, 0.004f));

            Assert.Equal(0.004f, span.Z, 5);
            Assert.Equal(0.142f, span.X, 5);
            Assert.Equal(0.026f, span.Y, 5);
        }

        [Fact]
        public void A_plane_with_no_thickness_at_all_is_still_hittable()
        {
            Vector3 span = Hitbox.Around(new Vector3(0.1f, 0f, 0.1f));

            Assert.Equal(Hitbox.Thinnest, span.Y, 5);
        }

        [Fact]
        public void A_long_thin_sliver_is_not_generous_however_much_of_it_there_is()
        {
            // 300mm by 3mm: plenty of area, and nothing you could land on
            Assert.False(Hitbox.Generous(new Vector3(0.3f, 0.001f, 0.003f)));
        }

        [Fact]
        public void How_short_it_is_says_by_how_much()
        {
            Assert.Equal(0f, Hitbox.Short(new Vector3(0.05f, 0.004f, 0.05f)), 5);

            Assert.Equal(Hitbox.Least - 0.01f,
                         Hitbox.Short(new Vector3(0.01f, 0.004f, 0.05f)), 5);
        }

        [Fact]
        public void What_comes_out_is_always_generous_whatever_went_in()
        {
            var sizes = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.001f, 0.001f, 0.001f),
                new Vector3(0.4f, 0.6f, 0.2f),
                new Vector3(-0.03f, 0.004f, -0.03f),
            };

            foreach (Vector3 size in sizes) Assert.True(Hitbox.Generous(Hitbox.Around(size)));
        }
    }
}
