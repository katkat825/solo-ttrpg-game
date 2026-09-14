namespace Content.Minis
{
    // HOW BIG A THING ON A TABLE IS ALLOWED TO BE.
    //
    // Not an import ceiling - those are `Content.Models.ModelCaps` and they are about what a
    // parser is willing to chew on. This is about the TABLE: a manifest measures in metres, and
    // the one mistake every author will make once is typing 75 where 0.075 was meant, which is a
    // figure taller than the room standing on a 60 mm square.
    //
    // ONE NUMBER, DELIBERATELY LOOSE. It is not a balance lever and it is not a budget - it exists
    // only so a unit mix-up is refused with a sentence about metres rather than accepted and
    // discovered by eye. A siege engine or a dragon on a 3x3 base has room under it.
    public static class MiniCaps
    {
        // a metre. Nothing on this table is a metre of anything - the board's squares are 0.06 and
        // the whole mat is well under one - so this is an order of magnitude of headroom over
        // anything real and still two orders below the mistake it catches
        public const float LargestPiece = 1f;
    }
}
