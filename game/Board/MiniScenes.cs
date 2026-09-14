using System.Collections.Generic;
using Godot;
using Content.Minis;
using Core.Characters;

namespace Game.Board
{
    // WHICH SCENE EACH OF THE SHARED MINIS STANDS AS (MINIS_AND_ART.md A1).
    //
    // `Content.Minis.SharedMinis` names the four figures the base game ships and deliberately
    // knows no file, because `content/` must never name a Godot scene (CONVENTIONS.md 1). This is
    // the other side of that boundary and the ONLY place the mapping lives: `barbarian` is
    // `mini.tscn`, `rabble` is `rabble.tscn`, and so on.
    //
    // IT IS ALSO THE REPLACEMENT FOR `Fight.ModelFor`. That method chose a figure by TIER, and
    // said at the line why: "the last thing in here a campaign cannot say for itself, and it goes
    // when a statblock can name its own mini". A statblock can now, so the tier lookup becomes the
    // FALLBACK rather than the rule - a monster that names a mini gets the one it named, and one
    // that names none gets the tiered proxy it has had since Phase C.
    //
    // THE SCENES COME FROM EXPORTS RATHER THAN FROM PATHS. `Fight` already carries them as
    // `[Export] PackedScene` fields, which is what lets a different figure be a change to a scene
    // rather than to a file of string constants - and a `res://` path typed into C# is a path that
    // is not checked until the day it is wrong.
    public sealed class MiniScenes
    {
        readonly Dictionary<string, PackedScene> _scenes =
            new Dictionary<string, PackedScene>(System.StringComparer.Ordinal);

        readonly Dictionary<Tier, PackedScene> _tiers = new Dictionary<Tier, PackedScene>();

        // <paramref name="hero"/> is the piece the board already has standing on it, and the other
        // three are the tiered proxies a fight musters foes onto
        public MiniScenes(PackedScene hero, PackedScene rabble, PackedScene rival, PackedScene dread)
        {
            Put(SharedMinis.Hero, hero);
            Put(SharedMinis.Rabble, rabble);
            Put(SharedMinis.Rival, rival);
            Put(SharedMinis.Dread, dread);

            if (rabble != null) _tiers[Tier.Rabble] = rabble;
            if (rival != null) _tiers[Tier.Rival] = rival;
            if (dread != null) _tiers[Tier.Dread] = dread;
        }

        void Put(string id, PackedScene scene)
        {
            if (scene != null) _scenes[id] = scene;
        }

        // null for an id the base game does not ship, which is every pack's mini and is not an
        // error here - `MiniMaker` builds those out of their own model file
        public PackedScene Of(string id) =>
            id != null && _scenes.TryGetValue(id, out PackedScene scene) ? scene : null;

        // WHAT A FOE THAT NAMED NO MINI STANDS AS. By tier, because tier is the one thing about a
        // foe that is visible across the table - a Rabble is small and a Dread is not - and
        // because it is what every campaign written before this phase relies on
        public PackedScene For(Tier tier) =>
            _tiers.TryGetValue(tier, out PackedScene scene) ? scene : Of(SharedMinis.Rival);

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() => $"{_scenes.Count} shared minis";
    }
}
