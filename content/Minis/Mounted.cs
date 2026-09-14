using System.Collections.Generic;
using System.IO;

namespace Content.Minis
{
    // A MINI WITH EVERY QUESTION ABOUT IT ALREADY ANSWERED (MINIS_AND_ART.md A0, A1).
    //
    // `MiniRegistry.Mount` walks the variant chain and collapses it into this: no more inheriting,
    // no more nulls, no more relative paths. What the presentation layer gets is one flat answer -
    // which file, how big, what colour, which clip plays which motion, which folder of wavs each
    // motion is heard out of - and it does none of the deciding.
    //
    // THAT SPLIT IS THE POINT, and it is the same one `StandardResolver` makes with `PoolResult`:
    // "the visual layer only draws and translates what the resolver reports" (CONVENTIONS.md).
    // Every rule about variants, inheritance, defaults and cycles is tested headlessly in
    // `content.tests`; `game/` holds a struct's worth of values and a `GltfDocument`.
    //
    // A PATH HERE IS ABSOLUTE AND HAS ALREADY BEEN CHECKED. `MiniReader.Inside` refused anything
    // that could climb out of the pack at the field where it was written, and the combination
    // below is done once, here - so no caller is ever holding a relative path it has to decide
    // what to do with, which is exactly how a path escapes a sandbox.
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

        // what was asked for - `grimdark.oldbones`
        public string Id { get; }

        // and what it turned out to be made of: the id at the END of the variant chain, which is
        // either one of the shared roster's or the mini that owns the model file. Carried because
        // a report that says "oldbones stood as rabble" is how an author sees a variant working
        public string Source { get; }

        // the pack folder the model came out of, or empty for one of the shared roster's
        public string Folder { get; }

        // the model file's path RELATIVE to that folder, or empty
        public string Model { get; }

        // THE SHARED ROSTER'S MINIS HAVE NO FILE, and this is how a caller tells. What one of
        // those stands as is a Godot scene with a base and a voice on it, which `content/` must
        // never name - `Game.Board.MiniScenes` is the one place that mapping lives
        public bool IsShipped => Model.Length == 0;

        // the whole path on this disk, or empty for a shipped one. Combined here, once
        public string File =>
            IsShipped ? "" : Path.Combine(Folder, Model.Replace('/', Path.DirectorySeparatorChar));

        public Fit Fit { get; }

        // metres, and only meaningful when `Fit` is `Fit.Height`
        public float Height { get; }

        public float Foot { get; }

        public Tint Tint { get; }

        // model-local clip names, keyed by what the piece is doing. An absent motion is missing
        // polish and never a broken fight (A1, "The clip contract degrades")
        public IReadOnlyDictionary<Motion, string> Clips { get; }

        // absolute folders of wav files, keyed the same way. Absent means "use the shared pool",
        // which is what every mini in the game did before A3 and what most will keep doing
        public IReadOnlyDictionary<Motion, string> Foley { get; }

        public string ClipFor(Motion motion) =>
            Clips.TryGetValue(motion, out string clip) ? clip : "";

        public string FoleyFor(Motion motion) =>
            Foley.TryGetValue(motion, out string folder) ? folder : "";

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{Id} as {(IsShipped ? Source : Model)}" +
            (Fit == Fit.Height ? $", {Height * 1000f:0} mm" : ", fitted to the cell") +
            (Tint.IsSomething ? $", tinted {Tint}" : "") +
            (Clips.Count > 0 ? $", {Clips.Count} clips" : "") +
            (Foley.Count > 0 ? $", {Foley.Count} foley sets" : "");
    }
}
