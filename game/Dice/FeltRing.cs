using Godot;

// builds the flat ring of light that gets drawn on the felt around a die
// a real annulus in the XZ plane, not a TorusMesh on its side
// a tube has thickness that catches the light wrong - a ring on a table is a mark, not an object
// pure geometry and material - knows nothing about dice, outcomes or rules

public static class FeltRing
{
    const int Segments = 64;   // smooth at tray scale

    // centred on the origin, lying in the XZ plane, face up
    public static ArrayMesh Build(float innerRadius, float outerRadius)
    {
        var vertices = new Vector3[Segments * 6];
        var normals = new Vector3[Segments * 6];

        for (int i = 0; i < Segments; i++)
        {
            float a = Mathf.Tau * i / Segments;
            float b = Mathf.Tau * (i + 1) / Segments;

            Vector3 innerA = Point(a, innerRadius);
            Vector3 outerA = Point(a, outerRadius);
            Vector3 innerB = Point(b, innerRadius);
            Vector3 outerB = Point(b, outerRadius);

            int v = i * 6;

            vertices[v + 0] = innerA;
            vertices[v + 1] = outerA;
            vertices[v + 2] = outerB;

            vertices[v + 3] = innerA;
            vertices[v + 4] = outerB;
            vertices[v + 5] = innerB;

            for (int k = 0; k < 6; k++) normals[v + k] = Vector3.Up;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    static Vector3 Point(float angle, float radius) =>
        new(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

    // additive and unshaded, so it reads as light on the felt rather than paint
    // and the dark green underneath never muddies the colour
    // culling off - a flat ring has no inside to get wrong
    // depth test left on, so a die rolling over its own mark occludes it
    public static StandardMaterial3D Ink(Color colour) => new()
    {
        AlbedoColor = colour,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };
}
