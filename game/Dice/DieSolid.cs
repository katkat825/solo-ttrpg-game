using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Dice;

// the shape of one die, as maths rather than as an asset
// Godot ships box, sphere and cylinder, so d4/d8/d10/d12 need geometry from somewhere
// a downloaded mesh and a hand-written face table are two descriptions that can silently disagree
// generating them means the render mesh, the hull, the numerals and the face table are all one
// a solid is vertices plus either normals or polygons - the rest, winding included, is derived

public sealed class DieSolid
{
    // one face, in the die's own space
    public readonly struct Facet
    {
        public readonly Vector3 Normal;

        public readonly Vector3 Centre;

        // counter-clockwise seen from outside
        public readonly int[] Ring;

        public readonly int Value;

        // centre to nearest edge - sets how big the numeral can be
        public readonly float Inradius;

        public Facet(Vector3 normal, Vector3 centre, int[] ring, int value, float inradius)
        {
            Normal = normal;
            Centre = centre;
            Ring = ring;
            Value = value;
            Inradius = inradius;
        }
    }

    public readonly struct Numeral
    {
        public readonly Vector3 Position;
        public readonly Vector3 Facing;

        // zero leaves the choice to DieParts - right in the middle of a face,
        // wrong for a d4's, which has to point at the corner it belongs to
        public readonly Vector3 Up;

        public readonly int Value;

        // em size of the glyph, in metres - a digit renders about 0.7 of it
        public readonly float Height;

        public Numeral(Vector3 position, Vector3 facing, Vector3 up, int value, float height)
        {
            Position = position;
            Facing = facing;
            Up = up;
            Value = value;
            Height = height;
        }
    }

    // die size as centre to furthest corner
    // not by edge length or volume - a shared edge length gives a tiny d12 and an enormous d4
    // picked so each solid stands about 50 mm on the felt, which makes five shapes read as one set
    // the d6 is the exact cube the M0-M3 tuning and fairness numbers were measured on - don't drift it
    static readonly Dictionary<Die, float> Radius = new()
    {
        [Die.D4] = 0.0380f,
        [Die.D6] = 0.0250f * 1.7320508f,
        [Die.D8] = 0.0425f,
        [Die.D10] = 0.0400f,
        [Die.D12] = 0.0350f,
    };

    readonly Facet[] _facets;

    DieSolid(Die size, Vector3[] vertices, Facet[] facets, DieFaceTable.ReadFrom readFrom, Numeral[] numerals)
    {
        Size = size;
        Vertices = vertices;
        _facets = facets;
        ReadFrom = readFrom;
        Numerals = numerals;

        Circumradius = vertices.Max(v => v.Length());
        Volume = MeasureVolume(vertices, facets);
        MinFlatAlignment = MeasureFlatness(facets);
    }

    public Die Size { get; }

    // corners, in the die's own space, centred on the origin
    public Vector3[] Vertices { get; }

    public IReadOnlyList<Facet> Facets => _facets;

    public DieFaceTable.ReadFrom ReadFrom { get; }

    public Numeral[] Numerals { get; }

    public float Circumradius { get; }

    // cubic metres - DieBody turns this into mass, so a set behaves like one material
    public float Volume { get; }

    // the alignment below which this shape cannot be lying flat
    // two faces of a d10 meet at about 145 degrees, so a d10 on that edge still scores 0.95,
    // where a cube on its edge scores 0.71 - one fixed number misses the d10 or rejects flat d4s
    // so this sits halfway between flat and on the shallowest edge, per shape
    public float MinFlatAlignment { get; }

    // the same normals the mesh was built from, by construction
    public DieFaceTable FaceTable() =>
        new(_facets.Select(f => new DieFaceTable.Face(f.Normal, f.Value)).ToArray(), ReadFrom);

    static readonly Dictionary<Die, DieSolid> Cache = new();

    // cached - the geometry is immutable and every die of a size shares it
    public static DieSolid For(Die size)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(size, out DieSolid solid)) return solid;

            solid = size switch
            {
                Die.D4 => Tetrahedron(),
                Die.D6 => Cube(),
                Die.D8 => Octahedron(),
                Die.D10 => Trapezohedron(),
                Die.D12 => Dodecahedron(),
                _ => throw new ArgumentOutOfRangeException(
                        nameof(size), size, "No solid for that die size."),
            };

            Cache[size] = solid;
            return solid;
        }
    }

    // ---------------------------------------------------------------- the five solids

    // the d4, and the one genuine special case in the set
    // a tetrahedron at rest has a vertex on top and no face, so the upward normal is noise
    // apex-read convention: a corner's number belongs to the face opposite it, the one on the felt,
    // so the code reads DOWNWARD, the player reads the apex, and they always agree
    // get it wrong and nothing crashes, nothing looks broken, and every d4 is quietly wrong
    static DieSolid Tetrahedron()
    {
        Vector3[] v =
        {
            new(1, 1, 1),
            new(1, -1, -1),
            new(-1, 1, -1),
            new(-1, -1, 1),
        };

        // face i is the one opposite vertex i, so its outward normal points away from it
        var faces = new (Vector3 Normal, int Value)[v.Length];
        for (int i = 0; i < v.Length; i++) faces[i] = (-v[i].Normalized(), i + 1);

        return FromNormals(Die.D4, v, faces, DieFaceTable.ReadFrom.DownwardFace, AtCorners);
    }

    static DieSolid Cube()
    {
        var v = new List<Vector3>();
        foreach (int x in new[] { -1, 1 })
        foreach (int y in new[] { -1, 1 })
        foreach (int z in new[] { -1, 1 })
            v.Add(new Vector3(x, y, z));

        var faces = new (Vector3, int)[]
        {
            (Vector3.Up, 1),
            (Vector3.Down, 6),
            (Vector3.Right, 3),
            (Vector3.Left, 4),
            (Vector3.Back, 2),
            (Vector3.Forward, 5),
        };

        return FromNormals(Die.D6, v.ToArray(), faces, DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
    }

    static DieSolid Octahedron()
    {
        Vector3[] v =
        {
            Vector3.Right, Vector3.Left,
            Vector3.Up, Vector3.Down,
            Vector3.Back, Vector3.Forward,
        };

        // each face faces a corner of the cube, and opposite corners sum to nine
        var faces = new (Vector3, int)[]
        {
            (new Vector3(1, 1, 1), 1),   (new Vector3(-1, -1, -1), 8),
            (new Vector3(1, 1, -1), 2),  (new Vector3(-1, -1, 1), 7),
            (new Vector3(1, -1, 1), 3),  (new Vector3(-1, 1, -1), 6),
            (new Vector3(-1, 1, 1), 4),  (new Vector3(1, -1, -1), 5),
        };

        return FromNormals(Die.D8, v, faces, DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
    }

    // a pentagonal trapezohedron - ten kite faces, opposite faces summing to eleven
    // the ring offset c is not free: a kite's four corners are only coplanar when the apex
    // sits at (3 + 4*phi) times it
    // wrong and the faces bow, the hull rounds them off, and the die rolls oddly with nothing to see
    static DieSolid Trapezohedron()
    {
        const float h = 1f;
        float c = h / (3f + 4f * ((1f + Mathf.Sqrt(5f)) / 2f));

        var v = new List<Vector3> { new(0, h, 0), new(0, -h, 0) };

        // upper ring at 0, 72, 144 ... lower ring offset by 36, so they interleave
        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.Tau * i / 5f;
            v.Add(new Vector3(Mathf.Cos(a), c, Mathf.Sin(a)));
        }

        for (int i = 0; i < 5; i++)
        {
            float a = Mathf.Tau * (i + 0.5f) / 5f;
            v.Add(new Vector3(Mathf.Cos(a), -c, Mathf.Sin(a)));
        }

        int Upper(int i) => 2 + i % 5;
        int Lower(int i) => 7 + i % 5;

        // face i on top is opposite face i+2 underneath, which is what makes the pairs sum to eleven
        int[] under = { 7, 6, 10, 9, 8 };

        var faces = new List<(int[] Ring, int Value)>();

        for (int i = 0; i < 5; i++)
            faces.Add((new[] { 0, Upper(i), Lower(i), Upper(i + 1) }, i + 1));

        for (int i = 0; i < 5; i++)
            faces.Add((new[] { 1, Lower(i), Upper(i + 1), Lower(i + 1) }, under[i]));

        return FromPolygons(Die.D10, v.ToArray(), faces.ToArray(),
                            DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
    }

    static DieSolid Dodecahedron()
    {
        float phi = (1f + Mathf.Sqrt(5f)) / 2f;
        float inv = 1f / phi;

        var v = new List<Vector3>();

        foreach (int x in new[] { -1, 1 })
        foreach (int y in new[] { -1, 1 })
        foreach (int z in new[] { -1, 1 })
            v.Add(new Vector3(x, y, z));

        foreach (int a in new[] { -1, 1 })
        foreach (int b in new[] { -1, 1 })
        {
            v.Add(new Vector3(0, a * inv, b * phi));
            v.Add(new Vector3(a * inv, b * phi, 0));
            v.Add(new Vector3(a * phi, 0, b * inv));
        }

        // the twelve face directions of THIS vertex set
        // note the order inside each triple - the icosahedron's vertices as usually written
        // give twelve normals that touch one corner each and no face at all
        var faces = new (Vector3, int)[]
        {
            (new Vector3(0, phi, 1), 1),   (new Vector3(0, -phi, -1), 12),
            (new Vector3(0, phi, -1), 2),  (new Vector3(0, -phi, 1), 11),
            (new Vector3(1, 0, phi), 3),   (new Vector3(-1, 0, -phi), 10),
            (new Vector3(1, 0, -phi), 4),  (new Vector3(-1, 0, phi), 9),
            (new Vector3(phi, 1, 0), 5),   (new Vector3(-phi, -1, 0), 8),
            (new Vector3(phi, -1, 0), 6),  (new Vector3(-phi, 1, 0), 7),
        };

        return FromNormals(Die.D12, v.ToArray(), faces, DieFaceTable.ReadFrom.UpwardFace, AtFaceCentres);
    }

    // ---------------------------------------------------------------- construction

    delegate Numeral[] Numbering(Vector3[] vertices, Facet[] facets, float radius);

    // a face's rim is every vertex furthest along its normal - saves hand-listing sixty
    // indices for the d12, and a typo gives a missing face rather than a subtly wrong one
    static DieSolid FromNormals(
        Die size, Vector3[] vertices, (Vector3 Normal, int Value)[] faces,
        DieFaceTable.ReadFrom readFrom, Numbering numbering)
    {
        var polygons = faces
            .Select(f => (Ring: RimAlong(vertices, f.Normal.Normalized()), f.Value))
            .ToArray();

        return FromPolygons(size, vertices, polygons, readFrom, numbering);
    }

    static DieSolid FromPolygons(
        Die size, Vector3[] vertices, (int[] Ring, int Value)[] faces,
        DieFaceTable.ReadFrom readFrom, Numbering numbering)
    {
        float radius = Radius[size];
        float scale = radius / vertices.Max(v => v.Length());
        Vector3[] scaled = vertices.Select(v => v * scale).ToArray();

        var facets = new Facet[faces.Length];

        for (int i = 0; i < faces.Length; i++)
        {
            int[] ring = (int[])faces[i].Ring.Clone();

            Vector3 centre = Average(ring.Select(k => scaled[k]));
            Vector3 normal = (scaled[ring[1]] - scaled[ring[0]])
                .Cross(scaled[ring[2]] - scaled[ring[0]]).Normalized();

            // outward, whatever order the ring was written in
            // every solid here is centred on the origin, so the face's own middle says which way is out
            if (normal.Dot(centre) < 0f)
            {
                Array.Reverse(ring);
                normal = -normal;
            }

            facets[i] = new Facet(normal, centre, ring, faces[i].Value, EdgeDistance(scaled, ring, centre));
        }

        return new DieSolid(size, scaled, facets, readFrom, numbering(scaled, facets, radius));
    }

    // everything within a whisker of the furthest is on that face - on a solid this
    // regular the next vertex in is nowhere near
    static int[] RimAlong(Vector3[] vertices, Vector3 normal)
    {
        float furthest = vertices.Max(v => v.Dot(normal));

        int[] on = Enumerable.Range(0, vertices.Length)
            .Where(i => vertices[i].Dot(normal) > furthest - 0.001f)
            .ToArray();

        if (on.Length < 3)
            throw new InvalidOperationException(
                $"A face normal of {normal} touches {on.Length} vertices - that is a corner or an edge, not a face.");

        Vector3 centre = Average(on.Select(i => vertices[i]));

        Vector3 u = (vertices[on[0]] - centre).Normalized();
        Vector3 w = normal.Cross(u);

        return on
            .OrderBy(i => Mathf.Atan2((vertices[i] - centre).Dot(w), (vertices[i] - centre).Dot(u)))
            .ToArray();
    }

    static Numeral[] AtFaceCentres(Vector3[] vertices, Facet[] facets, float radius) =>
        facets
            .Select(f => new Numeral(f.Centre, f.Normal, Vector3.Zero, f.Value, f.Inradius))
            .ToArray();

    const float CornerInset = 0.50f;   // face middle towards the corner, as a share of the way

    const float CornerHeight = 0.90f;  // numeral size, as a share of the face's inradius

    // the d4's numbers go in the corners - three to a face, twelve in all
    // a tetrahedron at rest shows three faces and hides the fourth, and the hidden one is the answer
    // so an apex-read d4 prints it at the top CORNER of all three faces you can see
    // numbering the faces instead puts the result face down on the felt where nobody can read it
    static Numeral[] AtCorners(Vector3[] vertices, Facet[] facets, float radius)
    {
        var numerals = new List<Numeral>();

        foreach (Facet f in facets)
        foreach (int corner in f.Ring)
        {
            // facet i was built as the face opposite vertex i - see Tetrahedron
            // so this corner's number is the facet with its index, which is also the face
            // the die rests on when this corner is uppermost
            int value = facets[corner].Value;

            Vector3 outward = vertices[corner] - f.Centre;

            numerals.Add(new Numeral(
                f.Centre + outward * CornerInset,
                f.Normal,

                // points at the corner it belongs to, so it reads upright when that corner is on top
                outward.Normalized(),
                value,
                f.Inradius * CornerHeight));
        }

        return numerals.ToArray();
    }

    // ---------------------------------------------------------------- measurements

    static Vector3 Average(IEnumerable<Vector3> points)
    {
        var sum = Vector3.Zero;
        int n = 0;

        foreach (Vector3 p in points) { sum += p; n++; }

        return sum / n;
    }

    static float EdgeDistance(Vector3[] vertices, int[] ring, Vector3 centre)
    {
        float nearest = float.PositiveInfinity;

        for (int i = 0; i < ring.Length; i++)
        {
            Vector3 a = vertices[ring[i]];
            Vector3 b = vertices[ring[(i + 1) % ring.Length]];
            nearest = Mathf.Min(nearest, centre.DistanceTo((a + b) * 0.5f));
        }

        return nearest;
    }

    // fans each face into triangles and sums the tetrahedra back to the origin
    static float MeasureVolume(Vector3[] vertices, Facet[] facets)
    {
        float total = 0f;

        foreach (Facet f in facets)
        for (int i = 1; i < f.Ring.Length - 1; i++)
        {
            Vector3 a = vertices[f.Ring[0]];
            Vector3 b = vertices[f.Ring[i]];
            Vector3 c = vertices[f.Ring[i + 1]];
            total += a.Dot(b.Cross(c));
        }

        return Mathf.Abs(total) / 6f;
    }

    // halfway, in dot product, between a face lying flat and the die on its shallowest edge
    static float MeasureFlatness(Facet[] facets)
    {
        float shallowest = -1f;

        for (int i = 0; i < facets.Length; i++)
        for (int j = 0; j < facets.Length; j++)
        {
            if (i == j) continue;
            shallowest = Mathf.Max(shallowest, facets[i].Normal.Dot(facets[j].Normal));
        }

        // on that edge the better face leans half the angle between them, and the half-angle
        // cosine of an angle whose cosine is d is sqrt((1+d)/2)
        float onEdge = Mathf.Sqrt((1f + shallowest) / 2f);

        return (onEdge + 1f) / 2f;
    }
}
