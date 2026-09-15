using System.Collections.Generic;
using Godot;
using Content.Minis;
using Core.Characters;

namespace Game.Board
{
    public sealed class MiniScenes
    {
        readonly Dictionary<string, PackedScene> _scenes =
            new Dictionary<string, PackedScene>(System.StringComparer.Ordinal);

        readonly Dictionary<Tier, PackedScene> _tiers = new Dictionary<Tier, PackedScene>();

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

        // null for an id the base game does not ship; MiniMaker builds those from their own model
        public PackedScene Of(string id) =>
            id != null && _scenes.TryGetValue(id, out PackedScene scene) ? scene : null;

        // fallback by tier for a foe that named no mini
        public PackedScene For(Tier tier) =>
            _tiers.TryGetValue(tier, out PackedScene scene) ? scene : Of(SharedMinis.Rival);

        // developer only, not localized, never reaches a player
        public override string ToString() => $"{_scenes.Count} shared minis";
    }
}
