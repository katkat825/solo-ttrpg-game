using Godot;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Localization;
using Core.Resolution;
using Game.Localization;

namespace Game
{
    // smoke test for the core library on its own scene; the real game scene is table.tscn
    public partial class Main : Node
    {
        readonly IArchetypeSource _archetypes = Game.Campaigns.Library.Load();

        public override void _Ready()
        {
            var loc = new GodotLocalizer();
            var rng = new SeededRng((int)Time.GetTicksMsec());

            // the label is a key, resolved here at the edge, never inside the rules
            var hero = _archetypes.Create(EngineIds.Barbarian);
            var result = new StandardResolver(rng).Resolve(hero.BuildPool(Attr.Might, Skill.Blades));

            GD.Print("core library reachable from Godot");
            foreach (var d in result.Rolls)
                GD.Print($"  {loc.Get(d.LabelKey),-22} {d.Die.Label(),-4} -> {d.Value}  " +
                         (d.Counted ? "counted" : "IMPACT"));

            GD.Print($"  total {result.Total} vs Standard {Difficulty.Standard}: " +
                     (result.Beats(Difficulty.Standard) ? "success" : "failure"));

            GD.Print("");
            new CombatEngine(new StandardResolver(rng), observer: new RecordingCombatObserver(GD.Print))
                .Run(hero, _archetypes.Standard());
        }
    }
}
