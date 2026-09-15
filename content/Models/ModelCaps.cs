namespace Content.Models
{
    public static class ModelCaps
    {
        // per model, summed over every mesh in it
        public const int Vertices = 200_000;

        public const int Triangles = 120_000;

        // a rigged figure is a few dozen nodes; ten thousand empties is an export mistake that stalls with no visible cause
        public const int Nodes = 2_000;

        // whole file, embedded textures included; big enough for any miniature, small enough not to be a memory attack
        public const long Bytes = 64L * 1024L * 1024L;

        public const int Texture = 2048;

        // a clip map names at most five; a thousand is a mistake or an attempt to stall the import
        public const int Animations = 256;

        // so a model can't be a hundred individually-legal 2K atlases
        public const int Images = 32;

        // .glb first: the self-contained format, and the one a stranger should be sending
        public static readonly string[] Extensions = { ".glb", ".gltf" };
    }
}
