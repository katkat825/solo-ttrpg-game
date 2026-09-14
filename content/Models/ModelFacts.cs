using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Models
{
    // WHAT A MODEL FILE TURNED OUT TO CONTAIN (MINIS_AND_ART.md A2).
    //
    // Counted out of the glTF's own tables rather than out of its geometry, which is the whole
    // trick: a glTF declares how many vertices each accessor holds and how many nodes are in its
    // scene graph, so every cap can be checked from the JSON header without a byte of mesh data
    // being decoded. Refusing a hostile file must not mean parsing it first.
    //
    // THE CLIP NAMES ARE THE OTHER HALF, and they are what turns A1's "rename a clip wrong and it
    // degrades with a NAMED warning" into a sentence worth reading: knowing the file has
    // `Idle`, `Walk_A` and `Death_A` means the validator can say which of those the author
    // probably meant, rather than "no such clip".
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

        // as the author would name it - relative to the pack folder
        public string File { get; }

        public long Bytes { get; }

        public int Vertices { get; }

        public int Triangles { get; }

        public int Nodes { get; }

        public int Meshes { get; }

        // the animation names the file actually carries, in the file's own order
        public IReadOnlyList<string> Clips { get; }

        public IReadOnlyList<TextureFact> Textures { get; }

        // A MODEL WITH NO RIG IS NOT A PROBLEM - "a model with no rig at all is a static piece
        // that still slides and gets struck" (A1). It IS worth knowing, because a manifest that
        // names five clips over a file with none is an author who exported the wrong thing
        public bool IsRigged => Clips.Count > 0;

        public bool Has(string clip) =>
            clip != null && Clips.Any(c => string.Equals(c, clip, StringComparison.Ordinal));

        // DEVELOPER AND AUTHOR FACING, and deliberately not localized - this is what the validator
        // prints when a pack is clean, and it is the line an author checks their export against
        public override string ToString() =>
            $"{File}: {Vertices:n0} vertices, {Triangles:n0} triangles, {Meshes} " +
            (Meshes == 1 ? "mesh" : "meshes") + $", {Nodes} nodes, {Bytes / 1024:n0} KB" +
            (Clips.Count > 0 ? $", clips: {string.Join(", ", Clips)}" : ", no animation") +
            (Textures.Count > 0 ? $", {Textures.Count} textures" : "");
    }

    // ONE TEXTURE IN IT, measured rather than decoded (`ImageSize`)
    public sealed class TextureFact
    {
        public TextureFact(string name, int width, int height)
        {
            Name = name ?? "";
            Width = width;
            Height = height;
        }

        // the image's own name, its uri, or `image[3]` when it has neither - whatever gives an
        // author the best chance of finding it in their export
        public string Name { get; }

        public int Width { get; }

        public int Height { get; }

        public int Widest => Math.Max(Width, Height);

        // DEVELOPER ONLY - not localized
        public override string ToString() => $"{Name} {Width}x{Height}";
    }
}
