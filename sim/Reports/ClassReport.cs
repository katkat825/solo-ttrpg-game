using System;
using System.Collections.Generic;
using System.Linq;
using Content.Campaigns;
using Content.Classes;
using Content.Items;
using Core.Characters;
using Core.Combat;
using Core.Dice;
using Core.Resolution;

namespace Sim
{
    static class ClassReport
    {
        static readonly IArchetypeSource Engine = new BuiltInArchetypes();

        // "on the curve" is near 85% survival for the starting hero; the band is wide, a smell test not a spec
        const double Floor = 55.0;

        const double Ceiling = 97.0;

        public static void Run(int trials)
        {
            List<(string Pack, ClassCard Card, ItemCatalogue Items)> classes = OnDisk();

            Table.Title("Classes on disk, against the standard fight (4 Rabble + 1 Rival)");
            Table.Header("class", "win %", "avg rounds", "vigor left");

            Stats baseline = Sweep(trials, () => Engine.Create(EngineIds.Barbarian));

            WriteRow("barbarian (the baseline)", baseline);

            if (classes.Count == 0)
            {
                Table.Row("no classes installed", "-", "-", "-");
                return;
            }

            var warnings = new List<string>();

            foreach ((string pack, ClassCard card, ItemCatalogue items) in classes)
            {
                Stats stats = Sweep(trials, () => card.Create(items));

                WriteRow(card.Id, stats);

                if (stats.WinPct < Floor)
                    warnings.Add($"{card.Id} survives {stats.WinPct:0.0}% of the standard fight, " +
                                 $"against the baseline's {baseline.WinPct:0.0}% - that is a hard " +
                                 "class, which is allowed. Defense is the scalpel and Vigor is the " +
                                 "blunt instrument");

                if (stats.WinPct > Ceiling)
                    warnings.Add($"{card.Id} survives {stats.WinPct:0.0}% of the standard fight, " +
                                 $"against the baseline's {baseline.WinPct:0.0}% - the standard " +
                                 "fight is not a fight for it any more. One point of Defense is " +
                                 "worth more than eight of Vigor");
            }

            if (warnings.Count == 0) return;

            Table.Title("And what is off the curve");

            foreach (string warning in warnings) Console.WriteLine("  warn  " + warning);
        }

        static List<(string, ClassCard, ItemCatalogue)> OnDisk()
        {
            var found = new List<(string, ClassCard, ItemCatalogue)>();

            foreach (string root in Roots())
            {
                if (!System.IO.Directory.Exists(root)) continue;

                foreach (string folder in System.IO.Directory.EnumerateDirectories(root)
                                                   .OrderBy(f => f, StringComparer.Ordinal))
                {
                    Package package = Package.Read(folder);

                    if (package.Failed) continue;

                    var items = new ItemCatalogue();
                    items.Absorb(package.Items);
                    items.Absorb(SharedGear.Catalogue);

                    foreach (ClassCard card in package.Classes.All)
                        found.Add((package.Id, card, items));
                }
            }

            return found;
        }

        // the sim can't ask godot for res://, so it walks up from the working directory to find packs
        static IEnumerable<string> Roots()
        {
            var at = new System.IO.DirectoryInfo(AppContext.BaseDirectory);

            for (int up = 0; up < 8 && at != null; up++, at = at.Parent)
            {
                string campaigns = System.IO.Path.Combine(at.FullName, "campaigns");

                if (System.IO.Directory.Exists(campaigns)) yield return campaigns;
            }
        }

        readonly struct Stats
        {
            public readonly double WinPct;
            public readonly double AvgRounds;
            public readonly double AvgVigorLeft;

            public Stats(double win, double rounds, double vigor)
            {
                WinPct = win;
                AvgRounds = rounds;
                AvgVigorLeft = vigor;
            }
        }

        static void WriteRow(string label, Stats s) =>
            Table.Row(label, Table.Pct(s.WinPct), Table.Num(s.AvgRounds), Table.Num(s.AvgVigorLeft));

        // initiative off so every row runs against the same rng sequence, only the class changing
        static Stats Sweep(int trials, Func<Actor> hero)
        {
            var engine = new CombatEngine(
                new StandardResolver(new SeededRng(4242)),
                new CombatOptions { RollInitiative = false });

            int wins = 0;
            long rounds = 0;
            long vigor = 0;

            for (int i = 0; i < trials; i++)
            {
                EncounterResult r = Fight(engine, hero(), Engine.Standard());

                rounds += r.Rounds;

                if (r.HeroWon)
                {
                    wins++;
                    vigor += r.HeroVigorRemaining;
                }
            }

            return new Stats(
                100.0 * wins / trials,
                (double)rounds / trials,
                wins > 0 ? (double)vigor / wins : 0);
        }

        static EncounterResult Fight(CombatEngine engine, Actor hero, IList<Actor> foes)
        {
            var fight = new Encounter(engine, hero, foes);

            fight.Begin();

            while (!fight.IsOver)
            {
                Actor acting = fight.Acting;

                if (ReferenceEquals(acting, hero))
                {
                    Actor target = engine.HeroTargeting.Choose(acting, fight.Foes);

                    if (target == null || fight.ActionsLeft <= 0) { fight.EndTurn(); continue; }

                    fight.Strike(target, engine.Options.HeroAttackAttr, engine.Options.HeroAttackSkill);
                    continue;
                }

                Actor prey = fight.TargetFor(acting);

                if (prey == null || fight.ActionsLeft <= 0) { fight.EndTurn(); continue; }

                fight.Strike(prey, acting.Tier.AttackAttr(), acting.Tier.AttackSkill());
            }

            return fight.Result;
        }
    }
}
