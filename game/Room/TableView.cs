using Godot;

namespace Game.Room
{
    // WHICH WAY IS THE PLAYER LOOKING FROM.
    //
    // The camera is fixed, at the 60 degrees off the felt idea_notes.txt asks for (table.tscn says
    // so at the line). Everything on this table is a physical object, and most of them do not care
    // - a die reads from anywhere and a mini is a mini. The things that DO care are the ones with
    // WORDS on them: a note, a bubble, a choice card, the initiative list down the side of the mat.
    //
    // Lying flat, a card is seen at 30 degrees grazing and its text is squashed to half height and
    // barely legible. Standing upright, it is seen at 60 degrees off-normal and is worse. The eye
    // check found both, and they were the same bug wearing two hats.
    //
    // So: a card with words on it is TILTED TO FACE THE PLAYER, the way you tilt a piece of paper
    // toward yourself to read it. It asks the live camera rather than repeating its angle, so this
    // stays right if the camera is ever moved - and falls back to the documented angle when there
    // is no camera at all, which is every headless check.
    public static class TableView
    {
        // table.tscn's camera, for the headless case; the live camera is preferred wherever there is one
        public const float PitchDegrees = 60f;

        // the rotation that squares a card in the XY plane (a QuadMesh, a Label3D) to the viewer
        public static Basis Facing(Node3D card)
        {
            Camera3D camera = card?.IsInsideTree() == true
                ? card.GetViewport()?.GetCamera3D()
                : null;

            if (camera == null) return Tilted(PitchDegrees);

            // the card's own +Z must point at the camera; the pitch is whatever that takes
            Vector3 toward = camera.GlobalTransform.Origin - card.GlobalTransform.Origin;

            if (toward.LengthSquared() < 0.0000001f) return Tilted(PitchDegrees);

            toward = toward.Normalized();

            // pitch only. Yawing a card toward the viewer as well makes a row of them fan out, and
            // a fanned row of notes on a table reads as a bug rather than as a hand of cards.
            float pitch = Mathf.Atan2(toward.Y, Mathf.Abs(toward.Z));

            return new Basis(Vector3.Right, -pitch);
        }

        static Basis Tilted(float degrees) =>
            new Basis(Vector3.Right, -Mathf.DegToRad(degrees));

        // turns a node to face the viewer, keeping where it stands. IN GLOBAL SPACE, because half
        // of these hang off something that moves - a bubble belongs to a companion that idles, and
        // a note is pushed across the table - and a card that tilts with its owner is the bug again.
        public static void Face(Node3D card)
        {
            if (card == null || !card.IsInsideTree()) return;

            card.GlobalTransform = new Transform3D(Facing(card), card.GlobalTransform.Origin);
        }
    }
}
