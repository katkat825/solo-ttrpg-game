using System.Collections.Generic;
using System.Linq;
using Godot;
using Game.Access;
using Game.Room;

namespace Game.Diagnostics
{
    // EVERY WORD ON THIS TABLE IS INSIDE THE PICTURE.
    //
    // check-room.ps1 measures where the objects STAND, which is a fixed scene and settles once.
    // The words do not stand anywhere: a verb card, a speech bubble, an initiative list and the
    // DM's note are made when the moment arrives, sized to the line they carry, and put down
    // beside whatever the moment is about. That is the half that kept going wrong - a speech card
    // centred on a companion sitting near the left edge, the same card growing from its middle
    // until its corner left the side, an initiative list lying flat in nine pixels of type, and a
    // column of verb cards running off the near edge of the table one card at a time - and it is
    // the half a fixed measurement can never catch, because where it lands depends on the words.
    //
    // So it is asked WHILE the table is holding them, at each moment that makes some. Derived from
    // the scene rather than listed: every Label3D anywhere under the table, whatever put it there.
    //
    // Shared by the fight and the table checks because both make cards and neither owns the rule.
    public static class InThePicture
    {
        // what the camera cannot read, one complaint per line, empty when everything is legible
        public static IReadOnlyList<string> Unreadable(Node3D table, Framing frame, out int read)
        {
            read = 0;

            var lost = new List<string>();

            if (table == null || !frame.Exists) return lost;

            foreach (Node node in table.FindChildren("*", nameof(Label3D),
                                                     recursive: true, owned: false))
            {
                if (node is not Label3D words || !words.Visible || words.Text.Length == 0) continue;

                if (Hidden(words) || Going(words)) continue;

                read++;

                Aabb around = words.GlobalTransform * words.GetAabb();

                // a font that never rasterised has no box; the place it stands is still checkable
                float margin = around.Size == Vector3.Zero
                    ? frame.Margin(words.GlobalPosition)
                    : frame.Margin(around.GetCenter(), around.Size);

                if (margin < 0f)
                    lost.Add($"\"{words.Text}\" is {-margin * 1000f:0} mm outside the picture " +
                             $"at {around.GetCenter()} ({words.GetPath()})");
            }

            return lost;
        }

        // HOW BIG EVERY LINE ON THIS TABLE ACTUALLY IS ON SCREEN.
        //
        // A Label3D's size is metres, and metres say nothing about whether a word can be read: 16 mm
        // of type is enormous on a card in your hand and nine pixels tall from across a room, and
        // this camera stands a metre and a half back from a full-sized table. Every surface here was
        // authored in millimetres by eye with no camera in the question, so nothing ever converted
        // one into the other, and the whole game came out too small to read.
        //
        // Reports one line per distinct size, smallest first, so a sweep prints a table somebody can
        // act on rather than four hundred identical rows.
        //
        // TWO DISTANCES, because the table has two kinds of writing on it and only one rule would
        // be wrong about one of them. A card that arrives, is answered and is swept - a bubble, the
        // DM's note, a verb card, the turn order - is read from where you are sitting and has to be
        // legible THERE. A character sheet is A4 and is picked up: 20 px of em on a 210 mm page at
        // a metre and a half would be eleven characters to the line, so a sheet that can be read
        // without leaning in is a sheet with nothing written on it. That one has to be legible IN
        // HAND, and the thing it is written on has to actually be pick-up-able, which is a separate
        // question this cannot answer and TheSheetCanBeRead does.
        public static IReadOnlyList<string> TooSmallToRead(Node3D table, Framing frame,
                                                           out int read, out float worst)
        {
            read = 0;
            worst = float.MaxValue;

            var sizes = new Dictionary<string, (float Px, float Held, int Count)>();
            var lost = new List<string>();

            if (table == null || !frame.Exists) { worst = 0f; return lost; }

            foreach (Node node in table.FindChildren("*", nameof(Label3D),
                                                     recursive: true, owned: false))
            {
                if (node is not Label3D words || !words.Visible || words.Text.Length == 0) continue;

                if (Hidden(words) || Going(words)) continue;

                read++;

                float em = words.FontSize * words.PixelSize *
                           Mathf.Abs(words.GlobalBasis.Scale.Y);

                float depth = frame.Depth(words.GlobalPosition);

                if (depth <= 0f) continue;

                float px = em * frame.PixelsPerMetre(depth);

                // the same line with the thing held up in front of you, which is the distance
                // anything you pick up is actually read at
                float held = em * frame.PixelsPerMetre(Lean.Held);

                worst = Mathf.Min(worst, held);

                // grouped by the owner that made them, which is how a fix is applied
                string whose = words.GetParent()?.GetType().Name ?? "?";

                string at = $"{whose} {em * 1000f:0.0} mm";

                sizes[at] = sizes.TryGetValue(at, out var had)
                    ? (Mathf.Min(had.Px, px), Mathf.Min(had.Held, held), had.Count + 1)
                    : (px, held, 1);
            }

            if (read == 0) worst = 0f;

            foreach (KeyValuePair<string, (float Px, float Held, int Count)> one in
                     sizes.OrderBy(s => s.Value.Px))
            {
                string line = $"{one.Key,-26} {one.Value.Px,5:0.0} px sitting back, " +
                              $"{one.Value.Held,5:0.0} px in hand  x{one.Value.Count}";

                lost.Add(one.Value.Held < Legible.Least ? line + "  UNREADABLE"
                       : one.Value.Px < Legible.Least ? line + "  only in hand"
                       : line);
            }

            return lost;
        }

        // the camera the table is actually seen through, found rather than named
        public static Framing SeenThrough(Node3D table) => Framing.Of(CameraIn(table));

        public static Camera3D CameraIn(Node3D table)
        {
            if (table == null) return null;

            foreach (Node node in table.FindChildren("*", nameof(Camera3D),
                                                     recursive: true, owned: false))
                if (node is Camera3D eye) return eye;

            return null;
        }

        // a card whose owner is switched off is not on the table, whatever its own flag says
        static bool Hidden(Node3D words)
        {
            for (Node up = words.GetParent(); up != null; up = up.GetParent())
                if (up is Node3D box && !box.Visible) return true;

            return false;
        }

        // an offer that has been answered is swept with QueueFree, so its cards are still in the
        // tree for the rest of this frame and are not on the table any more
        static bool Going(Node words)
        {
            for (Node up = words; up != null; up = up.GetParent())
                if (up.IsQueuedForDeletion()) return true;

            return false;
        }
    }
}
