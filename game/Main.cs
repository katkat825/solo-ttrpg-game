using Godot;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Resolution;

// smoke test for the core library, kept on its own scene (main.tscn)
// not in the normal run path - the game's main scene is dice_tray.tscn
// the fastest way to confirm Godot can still see and use core/ after a refactor
// open main.tscn, press F6, read the output

public partial class Main : Node
{
    // the one place this scene names a concrete roster
    // swapping in a data-backed source is this line and nothing else - which is the whole
    // reason nothing below reaches past IArchetypeSource to build a fighter
    readonly IArchetypeSource _archetypes = new BuiltInArchetypes();

    public override void _Ready()
    {
        var loc = new GodotLocalizer();
        var rng = new SeededRng((int)Time.GetTicksMsec());

        // the label is a KEY, resolved here at the edge and never inside the rules
        var hero = _archetypes.Create(EngineIds.Barbarian);
        var result = new StandardResolver(rng).Resolve(hero.BuildPool(Attr.Might, Skill.Blades));

        GD.Print("core library reachable from Godot");
        foreach (var d in result.Rolls)
            GD.Print($"  {loc.Get(d.LabelKey),-22} {d.Die.Label(),-4} -> {d.Value}  " +
                     (d.Counted ? "counted" : "IMPACT"));

        GD.Print($"  total {result.Total} vs Standard {Difficulty.Standard}: " +
                 (result.Beats(Difficulty.Standard) ? "success" : "failure"));

        // a whole fight, narrated through the observer seam into Godot's output
        GD.Print("");
        new CombatEngine(new StandardResolver(rng), observer: new RecordingCombatObserver(GD.Print))
            .Run(hero, _archetypes.Standard());
    }
}
