using System.Collections.Generic;
using System.IO;

namespace Content.Minis
{
    public sealed class Mounted
    {
        public Mounted(string id, string source, string folder, string model, Fit fit,
                       float height, float foot, Tint tint,
                       IReadOnlyDictionary<Motion, string> clips,
                       IReadOnlyDictionary<Motion, string> foley)
        {
            Id = id;
            Source = source;
            Folder = folder ?? "";
            Model = model ?? "";
            Fit = fit;
            Height = height;
            Foot = foot;
            Tint = tint;
            Clips = clips ?? new Dictionary<Motion, string>();
            Foley = foley ?? new Dictionary<Motion, string>();
        }

        public string Id { get; }

        // the id at the end of the variant chain: what it actually stands as
        public string Source { get; }

        public string Folder { get; }

        public string Model { get; }

        // the shared roster's minis have no file; the scene they stand as is game/'s to name, never content/'s
        public bool IsShipped => Model.Length == 0;

        public string File =>
            IsShipped ? "" : Path.Combine(Folder, Model.Replace('/', Path.DirectorySeparatorChar));

        public Fit Fit { get; }

        // metres, only meaningful when Fit is Fit.Height
        public float Height { get; }

        public float Foot { get; }

        public Tint Tint { get; }

        // an absent motion is missing polish, never a broken fight
        public IReadOnlyDictionary<Motion, string> Clips { get; }

        // absent means use the shared pool
        public IReadOnlyDictionary<Motion, string> Foley { get; }

        public string ClipFor(Motion motion) =>
            Clips.TryGetValue(motion, out string clip) ? clip : "";

        public string FoleyFor(Motion motion) =>
            Foley.TryGetValue(motion, out string folder) ? folder : "";

        public override string ToString() =>
            $"{Id} as {(IsShipped ? Source : Model)}" +
            (Fit == Fit.Height ? $", {Height * 1000f:0} mm" : ", fitted to the cell") +
            (Tint.IsSomething ? $", tinted {Tint}" : "") +
            (Clips.Count > 0 ? $", {Clips.Count} clips" : "") +
            (Foley.Count > 0 ? $", {Foley.Count} foley sets" : "");
    }
}
