using System;
using System.Collections.Generic;
using Content.Campaigns;
using Core.Localization;

namespace Content.Minis
{
    // A MINI, AS A PACK WROTE IT DOWN (MINIS_AND_ART.md A0 and A1).
    //
    //     minis/oldbones.json
    //     {
    //       "id": "oldbones",
    //       "variant": "rabble",          a mini the game already has - A0, the safe gesture
    //       "tint": "#D8CFB8",
    //       "height": 0.082
    //     }
    //
    //     minis/skeleton.json
    //     {
    //       "id": "skeleton",
    //       "model": "models/skeleton.glb",   a file in this pack - A2, the sharp edge
    //       "fit": "cell",
    //       "foot": 0.0,
    //       "clips": { "move": "Walk", "topple": "Death_A" },
    //       "foley": { "placed": "audio/skeleton/placed" }
    //     }
    //
    // EITHER A `variant` OR A `model`, NEVER BOTH AND NEVER NEITHER. That exclusivity is the whole
    // phasing argument of `MINIS_AND_ART.md` expressed as a schema: a variant re-dresses something
    // the build already parsed and is safe by construction, a model is a stranger's binary file
    // and goes through `Content.Models.ModelReader`'s caps before anything instances it. One field
    // each, so which of the two a manifest is cannot be inferred wrongly.
    //
    // THE DISPLAY NAME IS NOT IN HERE, AND IT IS THE SAME ARGUMENT `Manifest` MAKES ABOUT A
    // CAMPAIGN'S TITLE. A1 asks for "its keyed display name"; a NAME in a data file is a string
    // that cannot be translated, and a KEY in a data file is a second description of the id free
    // to point at somebody else's string. So it is derived - `mini.grimdark.oldbones.name` out of
    // the scoped id - and the pack's own `locale/` carries it, exactly as its monsters' names are
    // carried (CONVENTIONS.md section 7).
    //
    // UNSTATED IS A THIRD THING, WHICH IS WHY THE MEASUREMENTS ARE NULLABLE. A variant inherits
    // what it does not say, so "the shipped rabble, bone-white" has to be distinguishable from
    // "the shipped rabble, bone-white, and fitted to the cell" - and `Fit.Cell` being the enum's
    // zero would quietly turn the first into the second. `MiniRegistry` is what walks the chain;
    // this is what remembers what was actually written.
    //
    // A MINI NEVER BECOMES A RULE. There is no field here that touches the dice, Vigor, Defense or
    // a behaviour, and there is not going to be one: a scary model on a weak statblock is a weak
    // monster, and that is correct (MINIS_AND_ART.md, "Architecture notes"). Everything below is
    // geometry, colour, animation and foley.
    public sealed class MiniManifest
    {
        public MiniManifest(string id, string variant, string model, Fit? fit, float? height,
                            float? foot, Tint tint, IReadOnlyDictionary<Motion, string> clips,
                            IReadOnlyDictionary<Motion, string> foley)
        {
            Id = id;
            Variant = variant ?? "";
            Model = model ?? "";
            Fit = fit;
            Height = height;
            Foot = foot;
            Tint = tint;
            Clips = clips ?? new Dictionary<Motion, string>();
            Foley = foley ?? new Dictionary<Motion, string>();
        }

        // SCOPED BY THE PACK THAT SHIPPED IT - `grimdark.oldbones`. The engine's own minis keep
        // their un-prefixed form, because they belong to no pack (Campaigns/ContentId.cs)
        public string Id { get; }

        // the mini this one re-dresses, by id, or empty. Resolved across every root by the
        // registry, so a pack can vary another pack's mini as well as one of the shared ones
        public string Variant { get; }

        // a model file, as a path relative to the pack folder, or empty. Forward slashes, always
        // inside the pack - `MiniReader.Inside` refuses anything that climbs out (A2)
        public string Model { get; }

        public bool IsVariant => Variant.Length > 0;

        public bool HasModel => Model.Length > 0;

        // A MINI THAT IS NEITHER is one of the shared roster's, and a pack cannot write one -
        // `MiniReader` refuses the shape. See `SharedMinis`
        public bool IsShipped => !IsVariant && !HasModel;

        // null when the file did not say, which means "whatever the mini I am a variant of does"
        public Fit? Fit { get; }

        // what `Fit.Height` is measuring, in metres
        public float? Height { get; }

        // EXTRA FOOT CORRECTION, in metres, on top of the one the loader always applies. A piece
        // stands ON the felt: `Mini.Stand` already subtracts the model's own `bounds.Position.Y`,
        // which is right for every model whose lowest vertex is its feet. This is for the ones
        // where it is not - a figure modelled on a plinth, or one whose cloak hangs below its
        // boots - and it is a nudge rather than the correction itself, because the correction is
        // arithmetic and arithmetic should not be re-typed by an author
        public float? Foot { get; }

        // what colour to paint it, or `Tint.None` for "say nothing", which inherits
        public Tint Tint { get; }

        // which animation in the model plays which motion. Every entry is optional and a missing
        // one is missing POLISH, never a broken fight - the engine's procedural fall stands in for
        // an absent `topple`, and a model with no rig at all is a static piece that still slides
        // and still gets struck (A1, "The clip contract degrades")
        public IReadOnlyDictionary<Motion, string> Clips { get; }

        // and which folder of samples plays it (A3). A folder, not a file - the folder is the
        // list, exactly as `ImpactPool` reads one, so adding a variation is copying a wav in.
        // Empty for a mini with no foley of its own, which then uses the shared pool
        public IReadOnlyDictionary<Motion, string> Foley { get; }

        // ---- the key that falls out of it, derived and never listed ----

        // ASKED BEFORE RESOLUTION, deliberately - a locale checklist is about what a pack NAMES,
        // and a mini whose variant chain is broken still has a name that needs translating
        // (Game.Campaigns.Loaded.Keys). Everything that needs the RESOLVED answer holds a
        // `Mounted` instead
        public string NameKey => KeyConventions.MiniName(Id);

        // which pack shipped it, or empty for one of the shared ones
        public string Pack => ContentId.CampaignOf(Id);

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString()
        {
            string from = IsVariant ? "a variant of " + Variant
                        : HasModel ? Model
                        : "a figure the base game ships";

            return $"{Id}: {from}" +
                   (Fit.HasValue ? $", {Fit.Value.ToString().ToLowerInvariant()}" : "") +
                   (Height.HasValue ? $" {Height.Value * 1000f:0} mm" : "") +
                   (Tint.IsSomething ? $", tinted {Tint}" : "") +
                   (Clips.Count > 0 ? $", {Clips.Count} clips" : "") +
                   (Foley.Count > 0 ? $", {Foley.Count} foley sets" : "");
        }
    }

    // HOW BIG A PIECE IS, and there are only two honest answers.
    //
    // `PaintedModel` has had both since B5 and the board already chooses between them the way a
    // table does: terrain is sized ACROSS because a square is what it has to fit in, and a figure
    // is sized BY HEIGHT because 75 mm of miniature on a 60 mm base is what a miniature is. A pack
    // author picks the same two, by name, rather than doing the arithmetic (MINIS_AND_ART.md A1,
    // "a scale hint (or 'fit to cell', the `ToFitWidth` default)")
    public enum Fit
    {
        // as wide as a square, whatever height that leaves it - `PaintedModel.ToFitWidth`
        Cell,

        // exactly this tall, whatever width that leaves it - `PaintedModel.ToFitHeight`
        Height,
    }
}
