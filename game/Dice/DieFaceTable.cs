using Godot;
using Rules.Dice;

// which number is showing, given a die's orientation
// split out of DieBody because the table is the piece that varies per shape
// it must match the numerals painted on the die - a mismatch makes the die lie silently
// every factory here reads DieSolid, so there is no second copy to keep in step

public sealed class DieFaceTable
{
    public readonly struct Face
    {
        // outward normal, in the die's own space
        public readonly Vector3 Normal;

        public readonly int Value;

        public Face(Vector3 normal, int value)
        {
            Normal = normal;
            Value = value;
        }
    }

    // which face carries the result
    // a d4 rests with a corner up and no face at all
    // so its number is on the face against the table, read DOWNWARD
    // get this wrong and it returns the wrong number forever without looking broken
    public enum ReadFrom
    {
        UpwardFace,
        DownwardFace,
    }

    readonly Face[] _faces;
    readonly Vector3 _reference;

    public DieFaceTable(Face[] faces, ReadFrom readFrom = ReadFrom.UpwardFace)
    {
        _faces = faces;
        ReadDirection = readFrom;
        _reference = readFrom == ReadFrom.DownwardFace ? Vector3.Down : Vector3.Up;
    }

    public ReadFrom ReadDirection { get; }

    public int Sides => _faces.Length;

    // alignment is a dot product - 1.0 is dead flat, low means cocked
    // live, not cached - valid mid-tumble, it just won't mean much until the die stops
    public (int Value, float Alignment) Read(Basis orientation)
    {
        int best = 0;
        float bestDot = float.NegativeInfinity;

        foreach (Face face in _faces)
        {
            float d = (orientation * face.Normal).Dot(_reference);

            if (d > bestDot)
            {
                bestDot = d;
                best = face.Value;
            }
        }

        return (best, bestDot);
    }

    // a tetrahedron, read from the face against the felt
    public static DieFaceTable D4() => For(Die.D4);

    public static DieFaceTable D6() => For(Die.D6);

    public static DieFaceTable D8() => For(Die.D8);

    public static DieFaceTable D10() => For(Die.D10);

    public static DieFaceTable D12() => For(Die.D12);

    public static DieFaceTable For(Die size) => DieSolid.For(size).FaceTable();
}
