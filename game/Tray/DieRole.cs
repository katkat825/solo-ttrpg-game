namespace Game.Tray
{
    public enum DieRole
    {
        // rolled, didn't count, isn't Impact - impossible in a three-die pool
        None,

        Counted,

        // the largest die left over - the one thrown again for damage
        Impact,
    }
}
