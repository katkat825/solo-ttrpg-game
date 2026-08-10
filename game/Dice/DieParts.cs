using System;
using System.Collections.Generic;
using Godot;

// turns a DieSolid into the three things Godot needs to show a die
// a mesh to draw, a shape to collide with, and the numerals painted on the faces
// everything here is a pure function of the solid
// so no die is ever assembled by hand - one die.tscn covers all five sizes

public static class DieParts
{
    const float Lift = 0.0004f;      // how far the numerals float off the face

    const int GlyphResolution = 64;  // rasterised at this and scaled down

    public const string NumbersNode = "Numbers";

    // flat-shaded - each face keeps its own corners, or the edges round off in the lighting
    // wound clockwise seen from outside, which is Godot's front-facing, so the solid's rims
    // are walked backwards here
    // get it wrong and the die renders inside out, visible only as the faces vanishing
    public static ArrayMesh BuildMesh(DieSolid solid)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();

        foreach (DieSolid.Facet facet in solid.Facets)
        {
            int[] ring = facet.Ring;

            for (int i = ring.Length - 2; i >= 1; i--)
            {
                vertices.Add(solid.Vertices[ring[0]]);
                vertices.Add(solid.Vertices[ring[i + 1]]);
                vertices.Add(solid.Vertices[ring[i]]);

                for (int k = 0; k < 3; k++) normals.Add(facet.Normal);
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // a convex hull, never a trimesh - trimesh on a moving body is slow and lets fast bodies through
    // a hull is exact for a convex solid anyway
    public static ConvexPolygonShape3D BuildHull(DieSolid solid) =>
        new() { Points = solid.Vertices };

    // a 6 and a 9 are the same glyph turned round, and a die gives no other clue
    // one mark on one of the pair separates them, and keeps the mark meaning "six"
    // add 9 here for the more usual look; the d4 has neither
    static readonly int[] Underlined = { 6 };

    // underline geometry in multiples of the glyph's em, measured rather than guessed
    // at font size 64 the baseline sits 0.39 em below the label's middle, a digit is 0.58 em wide
    const float UnderlineDrop = 0.47f;
    const float UnderlineWidth = 0.62f;
    const float UnderlineThickness = 0.07f;

    // one Label3D per face - three per face on the d4, which is numbered at its corners
    // the numerals and the face table come off the same solid, so they cannot disagree
    public static Node3D BuildNumbers(DieSolid solid, Color ink)
    {
        var root = new Node3D { Name = NumbersNode };

        // unshaded to match Label3D - a lit bar under an unlit numeral reads as a smudge
        var inkMaterial = new StandardMaterial3D
        {
            AlbedoColor = ink,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        foreach (DieSolid.Numeral numeral in solid.Numerals)
        {
            var label = new Label3D
            {
                Name = $"Face{numeral.Value}",
                Text = numeral.Value.ToString(),
                FontSize = GlyphResolution,
                PixelSize = numeral.Height / GlyphResolution,
                Modulate = ink,
                OutlineSize = 0,
                Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
                DoubleSided = false,
                AlphaCut = Label3D.AlphaCutMode.Discard,
                Transform = Facing(numeral.Position + numeral.Facing * Lift, numeral.Facing, numeral.Up),
            };

            root.AddChild(label);

            if (Array.IndexOf(Underlined, numeral.Value) < 0) continue;

            // a child of the label, so it inherits the numeral's plane and its offsets stay in glyph units
            // coplanar with the text but never on top of it
            label.AddChild(new MeshInstance3D
            {
                Name = "Underline",
                Mesh = new QuadMesh
                {
                    Size = new Vector2(numeral.Height * UnderlineWidth, numeral.Height * UnderlineThickness),
                    Material = inkMaterial,
                },
                Position = new Vector3(0f, -numeral.Height * UnderlineDrop, 0f),
            });
        }

        return root;
    }

    // +Z looks along facing, which is the way a Label3D reads
    // up of zero means choose one, which is what a numeral in the middle of a face wants
    // the choice dodges the faces pointing straight up and down, where up in the plane means
    // nothing - the camera sits at the near side, so the top face's text has to run away from it
    static Transform3D Facing(Vector3 position, Vector3 facing, Vector3 up)
    {
        Vector3 z = facing.Normalized();

        if (up == Vector3.Zero)
        {
            float vertical = z.Dot(Vector3.Up);

            up = vertical > 0.999f ? Vector3.Forward
               : vertical < -0.999f ? Vector3.Back
               : Vector3.Up;
        }

        Vector3 x = up.Cross(z).Normalized();
        Vector3 y = z.Cross(x);

        return new Transform3D(new Basis(x, y, z), position);
    }
}
