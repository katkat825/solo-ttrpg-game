namespace Game.Tray
{
    // what a die on the felt is, once the rules have looked at the throw
    // the only thing the view is allowed to decide highlighting from
    //
    // a ROLE, not a mark. the rules hand out roles; DieMark draws one and TrayMarks owns
    // the set of them for a throw. keeping the three words apart is the whole point of the
    // name - an enum called TrayMark sitting next to a node called TrayMarks was one letter
    // away from a mistake nothing would have caught
    public enum DieRole
    {
        // rolled, didn't count, isn't Impact - impossible in a three-die pool
        None,

        Counted,

        // the largest die left over - the one thrown again for damage
        Impact,
    }
}
