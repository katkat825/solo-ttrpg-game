using System.Collections.Generic;

namespace Content.Minis
{
    // THE MINIS THAT SHIP WITH THE GAME AND BELONG TO NO PACK (MINIS_AND_ART.md A1).
    //
    // "A mini registry in the loader: resolves a mini id to a loaded, treated model, across every
    // pack root, WITH THE SHARED ROSTER AS THE FALLBACK SOURCE." This is that roster, and it is
    // un-prefixed for the same reason `actor.rabble.*` is: it came with the base game
    // (CONVENTIONS.md section 7).
    //
    // IT IS ALSO WHAT A0'S FIRST GESTURE IS A VARIANT OF. "The shipped skeleton, but bone-white
    // and a head taller" needs a shipped skeleton to be a variant OF, and a pack author needs to
    // be able to name it without reading the source - so the four ids below are the published
    // vocabulary of this phase.
    //
    // IT KNOWS NO FILE, AND THAT IS THE BOUNDARY, NOT AN OMISSION. What the base game's minis
    // actually stand as is `mini.tscn`, `rabble.tscn`, `rival.tscn` and `dread.tscn` - Godot
    // scenes, with bases and voices and a shader on them, which `content/` must never name
    // (CONVENTIONS.md 1). So each entry here is a manifest with neither a `variant` nor a `model`,
    // which is a shape `MiniReader` refuses from a pack on purpose: a pack cannot declare one of
    // these, only the base game can, and `Game.Board.MiniScenes` is the one place that says which
    // scene each is. A pack asking for `rabble` gets the right figure; a pack claiming to BE
    // `rabble` gets `grimdark.rabble` and collides with nobody.
    //
    // IT IS A PLACEHOLDER IN EXACTLY THE WAY `BuiltInArchetypes` IS, and says so for the same
    // reason: the day the base game's own art ships as a pack folder like everybody else's, this
    // file is deleted and nothing above it changes, because everything above it holds a
    // `MiniCatalogue` and never this.
    public static class SharedMinis
    {
        // the hero the board has thrown checks with since B5 - KayKit's Barbarian
        public const string Hero = "barbarian";

        // and the three foes, which are tiered proxies until a campaign names its own
        public const string Rabble = "rabble";

        public const string Rival = "rival";

        public const string Dread = "dread";

        // A FIGURE IS 75 MM AND A RABBLE IS 50, which is the tiering the eye reads across the
        // table before it reads anything else (rabble.tscn). Stated here as well as in the scenes
        // because a variant of one has to know what it is varying FROM - "a head taller" is
        // arithmetic on this number
        public static IReadOnlyList<MiniManifest> All { get; } = new[]
        {
            Standing(Hero, 0.075f),
            Standing(Rabble, 0.05f),
            Standing(Rival, 0.062f),
            Standing(Dread, 0.09f),
        };

        static MiniManifest Standing(string id, float height) =>
            new MiniManifest(id, variant: null, model: null, Fit.Height, height, foot: 0f,
                             Tint.None, clips: null, foley: null);

        // the catalogue every registry starts from. Built once - these are immutable and there is
        // no folder behind them to re-read
        public static MiniCatalogue Catalogue { get; } = MiniCatalogue.Of(All);

        public static bool Has(string id) => Catalogue.Has(id);
    }
}
