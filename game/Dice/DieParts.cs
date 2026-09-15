using System;
using System.Collections.Generic;
using Godot;

namespace Game.Dice
{
    public static class DieParts
    {
        const float Lift = 0.0004f;      // how far the numerals float off the face

        const int GlyphResolution = 64;  // rasterised at this and scaled down

        public const string NumbersNode = "Numbers";

        // flat-shaded - each face keeps its own corners, or the edges round off in the lighting
        // wound clockwise from outside (godot front-facing), so the rims are walked backwards here, or the die renders inside out
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

        // convex hull, never a trimesh: trimesh on a moving body is slow and lets fast bodies through
        public static ConvexPolygonShape3D BuildHull(DieSolid solid) =>
            new() { Points = solid.Vertices };

        // 6 and 9 are the same glyph turned round; underline the 6 to tell them apart
        static readonly int[] Underlined = { 6 };

        // underline geometry in ems of the glyph, measured not guessed
        const float UnderlineDrop = 0.47f;
        const float UnderlineWidth = 0.62f;
        const float UnderlineThickness = 0.07f;

        // one Label3D per face; three per face on the d4, which is numbered at its corners
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

                // a child of the label so it inherits the numeral's plane and its offsets stay in glyph units
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

        // +z runs along facing; up of zero picks a fallback for the top and bottom faces, where an in-plane up is undefined
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
}
