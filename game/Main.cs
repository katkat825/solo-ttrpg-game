using Godot;
using Rules.Characters;
using Rules.Combat;
using Rules.Dice;
using Rules.Localization;
using Rules.Resolution;

// smoke test for the rules library, kept on its own scene (main.tscn)
// not in the normal run path - the game's main scene is dice_tray.tscn
// the fastest way to confirm Godot can still see and use rules/ after a refactor
// open main.tscn, press F6, read the output

public partial class Main : Node
{
    public override void _Ready()
    {
        var loc = new GodotLocalizer();
        var rng = new SeededRng((int)Time.GetTicksMsec());

        // the label is a KEY, resolved here at the edge and never inside the rules
        var hero = BuiltInArchetypes.Barbarian();
        var result = new StandardResolver(rng).Resolve(hero.BuildPool(Attr.Might, Skill.Blades));

        GD.Print("rules library reachable from Godot");
        foreach (var d in result.Rolls)
            GD.Print($"  {loc.Get(d.LabelKey),-22} {d.Die.Label(),-4} -> {d.Value}  " +
                     (d.Counted ? "counted" : "IMPACT"));

        GD.Print($"  total {result.Total} vs Standard {Difficulty.Standard}: " +
                 (result.Beats(Difficulty.Standard) ? "success" : "failure"));

        // a whole fight, narrated through the observer seam into Godot's output
        GD.Print("");
        new CombatEngine(new StandardResolver(rng), observer: new RecordingCombatObserver(GD.Print))
            .Run(hero, BuiltInArchetypes.StandardEncounter());
    }
}
