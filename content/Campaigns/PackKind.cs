namespace Content.Campaigns
{
    // WHAT KIND OF FOLDER THIS IS (MODDING.md section 2, MINIS_AND_ART.md A4).
    //
    // "A pack is a folder. A campaign is one kind of pack; a mini pack and a class pack are
    // others. All three are namespaced, all three are Workshop items, and a campaign can declare
    // it needs another pack through the `dependencies` field CONTENT_PIPELINE.md P4 reserved."
    //
    // THE LOADER GAINS KINDS OF FOLDER, NOT A SECOND MECHANISM. Everything that already existed
    // stays exactly as it was: one id, one namespace, one isolation boundary, one validator, one
    // list of roots. What a kind decides is which folders inside are expected to have anything in
    // them, and therefore which absences are worth a sentence - a campaign with no chapters has
    // nothing to play, and a MINI PACK with no chapters is simply a mini pack.
    //
    // `mixed` IS NOT A HEDGE. `MODDING.md` calls it "a campaign that ships its own art and classes
    // - which is most of them", and that is the honest common case: an author who makes a campaign
    // and draws their own monsters has one folder, one id, one Workshop item, and no reason to
    // split it in two.
    public enum PackKind
    {
        // rooms, monsters, encounters, chapters - what `CONTENT_PIPELINE.md` built
        Campaign,

        // minis and models, standing on their own so a campaign can depend on them
        Minis,

        // classes and kits - `CLASSES_AND_KITS.md`, phase K. Named here rather than later so a
        // pack written for that phase is refused with "this build does not load class packs yet"
        // rather than with "that is not a kind of pack", which reads as a typo
        Classes,

        // a campaign that brings its own art - most of them
        Mixed,
    }
}
