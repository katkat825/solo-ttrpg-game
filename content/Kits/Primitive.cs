namespace Content.Kits
{
    public enum Primitive
    {
        Channel,

        Check,
    }

    public enum Magnitude
    {
        Impact,

        Fixed,
    }

    public enum Cost
    {
        None,

        Strain,

        Nerve,
    }

    // named Target, not Range, to avoid colliding with System.Range
    public enum Target
    {
        Reach,
        Sight,
        Self,
    }

    // closed set; an open string here would be a scripting hook, and content is data, not code
    public enum Effect
    {
        Damage,

        Recoil,

        // who it lands on and which condition is decided by the primitive, not declared twice
        Condition,

        Rough,

        Open,

        Shove,

        Steady,
    }
}
