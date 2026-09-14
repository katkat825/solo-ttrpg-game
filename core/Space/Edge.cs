namespace Core.Space
{
    // what stands on the LINE between two squares.
    //
    // THE THING B2 GOT WRONG AND EDGE_WALLS.md PUTS RIGHT. A wall used to be a whole square: the
    // Wizardry model, where the map is made of solid blocks and a wall costs you a cell. On a wet-
    // erase battle map a wall is drawn ON the grid line and the squares on BOTH sides are ordinary
    // floor - you can stand against it, be shoved into it, fight across it. A one-square-thick
    // wall drawn as a cell also reads as hollow once it is modelled, because it is two faces with
    // the square's own thickness between them.
    //
    // So a wall is a property of an edge, and a `Tile` is only what a square IS. Nothing else in
    // the model changed shape: cells and edges are two layers over one extent, the same
    // arrangement `MapLayout` and `Grid` already had for terrain and occupancy.
    //
    // A CLOSED SET, like Tile, and engine vocabulary rather than content for the same reason: what
    // a wall DOES is a rule, and the map that says where the walls are is data.
    public enum Edge
    {
        // nothing on the line. the overwhelming majority of a map's edges, which is why it is the
        // default an empty array already holds
        None = 0,

        Wall,

        // shut. blocks exactly like a wall, which is what makes it worth having: B4 opens one on a
        // passed check, and a door that never did anything would be a wall with a different mesh.
        // an OPEN door is not a value here - it is Edge.None, because a doorway you can walk and
        // see through is a gap in the wall, and the frame around it is the view's business
        Door,
    }

    // the rules of an edge, in the one place - the same shape `Tiles` and `Die.IsReal()` use
    public static class Edges
    {
        // can a piece cross this line
        public static bool IsOpen(this Edge edge) => edge == Edge.None;

        // can a line of sight cross it. SEPARATE FROM IsOpen ON PURPOSE, exactly as
        // Tiles.IsTransparent is separate from Tiles.IsPassable: they agree for every edge that
        // exists today and they are not the same question. A portcullis, a rail, a low wall you can
        // shoot over - each is one of these two without being the other, and folding them together
        // now would have to be unfolded the first time one shows up
        public static bool IsTransparent(this Edge edge) => edge == Edge.None;
    }
}
