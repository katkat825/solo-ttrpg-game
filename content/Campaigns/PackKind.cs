namespace Content.Campaigns
{
    public enum PackKind
    {
        Campaign,

        Minis,

        // named though unsupported, so a class pack is refused with "not loaded yet", not "not a kind of pack"
        Classes,

        Mixed,
    }
}
