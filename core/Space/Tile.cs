namespace Core.Space
{
    // what one square of a map is made of.
    //
    // THREE KINDS, AND NO WALL AMONG THEM. Walls and doors moved onto the lines between squares in
    // EDGE_WALLS.md - see `Edge` for why - so a Tile is only what a square IS, and every square
    // that is not solid rock can hold a piece. That is the change: the squares that used to be
    // walls are now ordinary floor with a wall drawn on the line beside them.
    //
    // A CLOSED SET, AND IT IS ENGINE VOCABULARY RATHER THAN CONTENT. A campaign writes maps out of
    // these; it does not invent new ones. That is the same call `Attr` and `Skill` make and it is
    // made for the same reason: what difficult ground DOES - cost double to enter - is a rule, and
    // rules are code (CONVENTIONS.md, "rules in code, world in data"). The map that says which
    // squares are difficult is content and lives in a data file.
    //
    // Adding water, lava or a chasm later is a value here plus its rules below plus a glyph in
    // MapReader - and no campaign has to be touched. If a campaign ever needs a tile the engine
    // has never heard of, that is the moment for a data-driven tile table with `IArchetypeSource`'s
    // shape, and not before: `BuiltInArchetypes` is the register of that debt and this would join
    // it. Three kinds is not a taxonomy worth paying for yet.
    public enum Tile
    {
        // not part of this map at all - a cell off the edge of the layout, and SOLID ROCK inside
        // one. impassable and opaque, so pathfinding and sight need no bounds check of their own,
        // and the answer stays honest rather than a wall being invented where the map simply stops.
        //
        // it is the other way to make a barrier, and the author picks: a Void cell is a thick mass
        // you cannot be on either side of, a Wall edge is a thin partition with usable floor on
        // both. A pillar, the bulk outside the rooms, the rock a corridor is cut through - Void.
        // The wall between two rooms - an edge
        Void = 0,

        Floor,

        // difficult ground - rubble, scree, a spill. passable, and it costs double to enter, which
        // is the whole of what "difficult" means to a route
        Rough,
    }

    // the rules of a tile, in the one place. extension methods rather than properties on a class,
    // the same shape `Die.IsReal()` and `Die.Label()` already use
    public static class Tiles
    {
        // can a piece stand here
        public static bool IsPassable(this Tile tile) => tile == Tile.Floor || tile == Tile.Rough;

        // can a line be drawn through here. SEPARATE FROM IsPassable ON PURPOSE - they agree for
        // every tile that exists today and they are not the same question. A chasm is transparent
        // and impassable; a curtain or a thicket is passable and opaque. Folding them into one
        // flag now would have to be unfolded the first time either shows up - and `Edges` keeps
        // the pair apart on the lines for the same reason
        public static bool IsTransparent(this Tile tile) => tile == Tile.Floor || tile == Tile.Rough;

        // what it costs to enter, in ordinary squares. an impassable tile has no cost - asking is
        // the caller's mistake and the answer must never be small enough to look attractive
        public static int MoveCost(this Tile tile) => tile == Tile.Rough ? 2 : 1;

        // the cheapest any square can be, which is what keeps a route's heuristic admissible
        public const int MinimumCost = 1;
    }
}
