namespace Content.Models
{
    // THE CEILINGS A SUPPLIED MODEL IS HELD TO, IN ONE PLACE (MINIS_AND_ART.md A2).
    //
    // "Hard caps, enforced BEFORE the model is instanced: a vertex/triangle ceiling, a
    // texture-resolution ceiling, a node-count ceiling, and a file-size ceiling. A model over any
    // cap is refused by name, not clamped silently."
    //
    // WHY A CAP AT ALL, GIVEN THE GAME DRAWS A DOZEN PIECES. Not frame rate - a modern card would
    // not notice a million-triangle miniature. Two other reasons, and they are the ones that
    // matter on a storefront:
    //
    //   a cap is a BUDGET a parser is willing to spend before it knows anything about the file.
    //   The counts below are read out of the glTF's own header and refused before a byte of
    //   geometry is decoded, which is the difference between refusing a hostile file and being
    //   consumed by one
    //
    //   and a cap is HONEST FEEDBACK. A pack author who exports a 400k-triangle sculpt has made a
    //   mistake they cannot see on their own machine - the piece is 20 mm tall on a table, seen
    //   from one fixed camera at 60 degrees, wearing a shader that throws its shading away. Told
    //   at validation, they decimate; told never, they ship it and every subscriber pays
    //
    // THE NUMBERS ARE GENEROUS ON PURPOSE. The shipped Barbarian is a few thousand triangles and
    // KayKit's dungeon pieces are fewer; every cap here is an order of magnitude above the art the
    // game actually ships, so nothing a reasonable author does is near one. A cap that bites
    // ordinary work is a cap that gets argued with instead of respected.
    //
    // THE TEXTURE CEILING IS THE ONE THAT IS ABOUT QUALITY AS WELL AS SAFETY. `ASSET_MANIFEST.md`
    // is explicit - "512 for small, 1K tray/felt, 2K table top. Bigger is wasted VRAM, not better"
    // - and a 4K atlas on a figure the size of a thumbnail is exactly that.
    public static class ModelCaps
    {
        // PER MODEL, summed over every mesh in it
        public const int Vertices = 200_000;

        public const int Triangles = 120_000;

        // a rigged figure is a few dozen nodes; a kit-bashed scene graph with ten thousand empties
        // in it is a export mistake, and instancing one is a stall with no visible cause
        public const int Nodes = 2_000;

        // a whole file, including its textures when they are embedded. 64 MB is far beyond any
        // miniature and still small enough that a hostile file cannot be a memory attack
        public const long Bytes = 64L * 1024L * 1024L;

        // the widest or tallest a texture in it may be
        public const int Texture = 2048;

        // ANIMATIONS, capped for the same reason as nodes: a clip map names at most five, and a
        // file with a thousand in it is either a mistake or an attempt to make the import loop
        public const int Animations = 256;

        // and images, so that "a model" cannot mean "a hundred 2K atlases, each individually legal"
        public const int Images = 32;

        // WHAT A FILE MAY BE CALLED. One open, well-specified, widely-tooled format, so "export a
        // mini" has an answer in every 3D tool and the parser surface is one format and not ten
        // (A2). `.glb` first because it is the self-contained one and the one a stranger should be
        // sending
        public static readonly string[] Extensions = { ".glb", ".gltf" };
    }
}
