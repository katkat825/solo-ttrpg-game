using Godot;

namespace Game.Access
{
    // HOW BIG THE THING YOU REACH FOR HAS TO BE (AX1).
    //
    // Generous, forgiving hitboxes everywhere they can be - which was already being obeyed one object
    // at a time and for one reason at a time. The "?" is half again as wide as its brass because a
    // lost player should not have to aim; a line in the open book is the whole line rather than the
    // letters; a blank on the bookcase is a hand's width whatever the thing is. All three were right,
    // all three were written down beside the object, and none of them could be checked.
    //
    // SO THE RULE IS A FLOOR, NOT A FORMULA. Nothing smaller than a fingertip, and anything that wants
    // to be more forgiving than that still may - which is the only version of this rule that is safe
    // to apply everywhere: a rule that made everything forty percent bigger would have grown the
    // door's body up through the ceiling, and a raycast returns the nearest body it hits.
    //
    // THE THIN AXIS IS LEFT ALONE, and that is the whole subtlety. Most of what you touch here is a
    // piece of paper or a card lying on a table: padding its thickness to a fingertip would push the
    // body up through the thing standing on it and down through the table. It is the two broad faces
    // you aim at, so it is the two broad axes the rule is about.
    public static class Hitbox
    {
        // a fingertip on a table this size. The board's own squares are 60 mm, a nerve chip is 8, and
        // the smallest thing anybody should have to hit is somewhere between
        public const float Least = 0.024f;

        // a plane has no thickness and still has to be hittable
        public const float Thinnest = 0.004f;

        // the touch body for something this big on screen
        public static Vector3 Around(Vector3 visual)
        {
            var span = new Vector3(Mathf.Abs(visual.X), Mathf.Abs(visual.Y), Mathf.Abs(visual.Z));

            int thin = Thin(span);

            for (int axis = 0; axis < 3; axis++)
                span[axis] = Mathf.Max(span[axis], axis == thin ? Thinnest : Least);

            return span;
        }

        // is a body already big enough to aim at. The two broad axes, each on its own: a long thin
        // sliver is not generous however much area it has
        public static bool Generous(Vector3 span) => Short(span) <= 0f;

        // how many metres the meanest broad axis is short by, so a failure can say by how much
        public static float Short(Vector3 span)
        {
            var size = new Vector3(Mathf.Abs(span.X), Mathf.Abs(span.Y), Mathf.Abs(span.Z));

            int thin = Thin(size);

            float worst = 0f;

            for (int axis = 0; axis < 3; axis++)
            {
                if (axis == thin) continue;

                worst = Mathf.Max(worst, Least - size[axis]);
            }

            return worst;
        }

        // which axis is the thickness rather than a face. Ties go to the first, which is only
        // reachable for a cube - and a cube big enough to be a cube is generous on every axis
        static int Thin(Vector3 span)
        {
            int thin = 0;

            for (int axis = 1; axis < 3; axis++)
                if (span[axis] < span[thin]) thin = axis;

            return thin;
        }
    }
}
