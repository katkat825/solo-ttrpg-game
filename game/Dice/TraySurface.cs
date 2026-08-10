using Godot;

// one surface of a tray - look, physics and impact audio
// a tray is two of these, a floor and a set of walls, and they are genuinely separable
// felt grabs and deadens, wood slides and clacks
// a resource rather than code, so adding one is a .tres and a fairness sweep

[GlobalClass]
public partial class TraySurface : Resource
{
    // applied to every mesh under the body this dresses
    [Export] public StandardMaterial3D Material { get; set; }

    // physics_material_override lives on StaticBody3D, never on CollisionShape3D
    // so one body holding a floor shape and four wall shapes is forced to share
    // one friction and one bounce - that is why the tray scene is two bodies
    //
    // bounce is what decides how a die settles, so this can change the game
    // see the fairness note on TraySkin
    [Export] public PhysicsMaterial Physics { get; set; }

    // folder of impact samples for hits on this surface
    // sliced by tools/slice_impacts.py - the folder is the list, so a new set is a new folder
    [Export(PropertyHint.Dir)] public string AudioPool { get; set; } = ImpactPool.Default;

    // trim on every impact here, in decibels - negative for a surface that deadens
    //
    // there is one folder of samples today and there will be several later
    // until a felt recording exists, a felt floor is wood samples pulled down and dulled
    // keeping it in the tray's data means a reader can see the approximation
    // rather than it hiding as a constant inside the voice
    [Export] public float ImpactDb { get; set; }

    // below 1 is duller and softer-edged
    [Export] public float ImpactPitch { get; set; } = 1f;
}
