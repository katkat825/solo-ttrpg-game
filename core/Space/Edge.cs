namespace Core.Space
{
    // what stands on the line between two squares - a wall is a property of the edge, not a square
    public enum Edge
    {
        None = 0,

        Wall,

        // shut; an open door isn't a value here, it's Edge.None - a gap in the wall
        Door,
    }

    public static class Edges
    {
        public static bool IsOpen(this Edge edge) => edge == Edge.None;

        // separate from IsOpen on purpose - a portcullis or low wall is one without the other
        public static bool IsTransparent(this Edge edge) => edge == Edge.None;
    }
}
