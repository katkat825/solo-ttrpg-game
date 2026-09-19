using System.Collections.Generic;
using Godot;

namespace Game.Access
{
    // BIGGER LETTERS, AND HARDER ONES (AX3).
    //
    // Every word in this game is a Label3D on a physical object - there is no text layer, because
    // there is no HUD - so resizable text is not a font setting somewhere central. It is this: walk
    // what is on the table, multiply what each label was authored at, and put it back when the player
    // turns the dial down again.
    //
    // WHICH IS WHY IT REMEMBERS. Scaling a label from its current size compounds: three visits to the
    // settings page and the card is unreadable in the other direction. So the first time a label is
    // seen, what the author gave it is written down, and every size after that is computed from that
    // rather than from the last one. The same for the ink, which high contrast replaces outright.
    //
    // HIGH CONTRAST IS AN OUTLINE, NOT A THEME. A word on this table can be over parchment, over
    // felt, over a painted mat or over a mini, and which of those it is depends on where the camera
    // is - so there is no backdrop to pick an ink against. What works regardless is to push the ink
    // to whichever end it was already nearer and put the opposite colour behind it as an edge: the
    // letters then carry their own contrast with them wherever they are read.
    //
    // A LABEL MADE AFTER THE LAST SWEEP KEEPS ITS AUTHORED SIZE until the next one. That is a real
    // limit and not a hidden one: the room sweeps when a dial is turned and whenever it lays words
    // out, which covers everything that is read rather than glanced at.
    public sealed class Legible
    {
        readonly struct Authored
        {
            public Authored(Label3D label)
            {
                FontSize = label.FontSize;
                Ink = label.Modulate;
                Outline = label.OutlineSize;
                Edge = label.OutlineModulate;
            }

            public int FontSize { get; }

            public Color Ink { get; }

            public int Outline { get; }

            public Color Edge { get; }
        }

        readonly Dictionary<ulong, Authored> _was = new Dictionary<ulong, Authored>();

        // how many labels this has ever seen. A room with none is a room whose words are somewhere
        // this cannot reach, which would be worth knowing
        public int Known => _was.Count;

        // the smallest outline that reads as an outline at this scale
        public const int Edge = 6;

        // THE SMALLEST A LINE MAY BE ON SCREEN AND STILL BE READ: pixels of em height on a 1080p
        // display, at the depth the thing it is written on actually stands.
        //
        // This is a floor, not a size. The text dial multiplies up from whatever an author gave a
        // label, so somebody who needs larger has it; what the floor says is that the DEFAULT view
        // has to be playable. Nothing checked that. Every surface on this table was authored in
        // millimetres, by eye, in the editor, with no camera anywhere in the question - and the
        // answer came out between 7 and 19 px with most of it near 11, which is not small but
        // unreadable, and was the first thing said on looking at the game.
        //
        // 20 px of em is the usual floor for body text and it is not generous here, because this
        // text is also read at sixty degrees off the felt.
        public const float Least = 20f;

        // WHAT THE PLAYER ASKED FOR, APPLIED TO EVERY WORD UNDER HERE. Returns how many it touched.
        public int Apply(Node root, Adjustments how)
        {
            if (root == null || how == null) return 0;

            int touched = 0;

            foreach (Label3D label in Words(root))
            {
                ulong id = label.GetInstanceId();

                if (!_was.TryGetValue(id, out Authored was))
                {
                    was = new Authored(label);
                    _was[id] = was;
                }

                label.FontSize = Mathf.Max(1, Mathf.RoundToInt(was.FontSize * how.TextScale));

                if (how.HighContrast)
                {
                    Color ink = Contrast.Luminance(was.Ink) > 0.18f ? Colors.White : Colors.Black;

                    label.Modulate = new Color(ink, was.Ink.A);
                    label.OutlineSize = Mathf.Max(was.Outline, Edge);
                    label.OutlineModulate = Contrast.Edge(ink);
                }
                else
                {
                    label.Modulate = was.Ink;
                    label.OutlineSize = was.Outline;
                    label.OutlineModulate = was.Edge;
                }

                touched++;
            }

            return touched;
        }

        // every word under a node, however deep. A label is a label whether it is on a card, on the
        // sheet, or lettered beside a die
        public static IEnumerable<Label3D> Words(Node node)
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is Label3D label) yield return label;

                foreach (Label3D deeper in Words(child)) yield return deeper;
            }
        }

        // a label that has gone away leaves its authored size behind it; a long sitting lays out
        // thousands of cards, and an instance id is never reused for a live node
        public void Forget(Node root)
        {
            var alive = new HashSet<ulong>();

            foreach (Label3D label in Words(root)) alive.Add(label.GetInstanceId());

            foreach (ulong id in new List<ulong>(_was.Keys))
                if (!alive.Contains(id)) _was.Remove(id);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"legible: {Known} label(s) remembered";
    }
}
