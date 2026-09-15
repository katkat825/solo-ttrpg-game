using Godot;

namespace Game.Tray
{
    [GlobalClass]
    public partial class TraySurface : Resource
    {
        [Export] public StandardMaterial3D Material { get; set; }

        // physics_material_override lives on the StaticBody3D, not the shapes, so a floor and walls in one body share one bounce - hence two bodies
        // bounce decides how a die settles, so this can change the game
        [Export] public PhysicsMaterial Physics { get; set; }

        // fully qualified on purpose: Godot copies this initialiser into a generated partial with none of this file's usings, so a bare ImpactPool would fail there
        [Export(PropertyHint.Dir)] public string AudioPool { get; set; } = Game.Audio.ImpactPool.Default;

        // per-impact trim in decibels, negative for a surface that deadens; a felt floor is dulled wood until a felt recording exists
        [Export] public float ImpactDb { get; set; }

        // below 1 is duller and softer-edged
        [Export] public float ImpactPitch { get; set; } = 1f;
    }
}
