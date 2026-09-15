using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Models
{
    public sealed class ModelFacts
    {
        public ModelFacts(string file, long bytes, int vertices, int triangles, int nodes,
                          int meshes, IReadOnlyList<string> clips,
                          IReadOnlyList<TextureFact> textures)
        {
            File = file ?? "";
            Bytes = bytes;
            Vertices = vertices;
            Triangles = triangles;
            Nodes = nodes;
            Meshes = meshes;
            Clips = clips ?? Array.Empty<string>();
            Textures = textures ?? Array.Empty<TextureFact>();
        }

        public string File { get; }

        public long Bytes { get; }

        public int Vertices { get; }

        public int Triangles { get; }

        public int Nodes { get; }

        public int Meshes { get; }

        public IReadOnlyList<string> Clips { get; }

        public IReadOnlyList<TextureFact> Textures { get; }

        // no rig is fine (a static piece still slides); but it flags a manifest naming clips a file doesn't have
        public bool IsRigged => Clips.Count > 0;

        public bool Has(string clip) =>
            clip != null && Clips.Any(c => string.Equals(c, clip, StringComparison.Ordinal));

        public override string ToString() =>
            $"{File}: {Vertices:n0} vertices, {Triangles:n0} triangles, {Meshes} " +
            (Meshes == 1 ? "mesh" : "meshes") + $", {Nodes} nodes, {Bytes / 1024:n0} KB" +
            (Clips.Count > 0 ? $", clips: {string.Join(", ", Clips)}" : ", no animation") +
            (Textures.Count > 0 ? $", {Textures.Count} textures" : "");
    }

    // measured rather than decoded (ImageSize)
    public sealed class TextureFact
    {
        public TextureFact(string name, int width, int height)
        {
            Name = name ?? "";
            Width = width;
            Height = height;
        }

        // the image's name, its uri, or image[3] when it has neither, whatever helps an author find it
        public string Name { get; }

        public int Width { get; }

        public int Height { get; }

        public int Widest => Math.Max(Width, Height);

        public override string ToString() => $"{Name} {Width}x{Height}";
    }
}
