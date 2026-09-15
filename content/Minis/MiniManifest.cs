using System;
using System.Collections.Generic;
using Content.Campaigns;
using Core.Localization;

namespace Content.Minis
{
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

        public string Id { get; }

        public string Variant { get; }

        // path relative to the pack folder, or empty; MiniReader.Inside refuses anything that climbs out
        public string Model { get; }

        public bool IsVariant => Variant.Length > 0;

        public bool HasModel => Model.Length > 0;

        // neither variant nor model means one of the shared roster's; a pack can't write that shape
        public bool IsShipped => !IsVariant && !HasModel;

        // null means the file didn't say: inherit from what this is a variant of
        public Fit? Fit { get; }

        public float? Height { get; }

        // extra foot correction in metres, on top of the loader's automatic one; a nudge for plinths and hanging cloaks
        public float? Foot { get; }

        // Tint.None means say nothing, which inherits
        public Tint Tint { get; }

        // missing entries are missing polish, never a broken fight; the engine's fallback stands in
        public IReadOnlyDictionary<Motion, string> Clips { get; }

        // a folder, not a file (the folder is the list); empty falls back to the shared pool
        public IReadOnlyDictionary<Motion, string> Foley { get; }


        // on the manifest, not Mounted: a mini still owes a name even if its variant chain is broken
        public string NameKey => KeyConventions.MiniName(Id);

        public string Pack => ContentId.CampaignOf(Id);

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

    public enum Fit
    {
        Cell,

        Height,
    }
}
