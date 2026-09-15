using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Core.Dice;
using Core.Statistics;
using Game.Dice;

namespace Game.Diagnostics
{
    public partial class DiceFairness : HeadlessCheck
    {
        protected override string Subject => "fairness";

        const uint TrayLayer = 1;

        const uint SoloLayer = 2;

        // 31 = Godot's 32 collision layers minus the tray's
        const int MaxCollidingPools = 31;

        // the sweep's defaults live only here; the script once had its own copies and they diverged
        public const int DefaultThrows = 2000;

        public const int DefaultSoloPools = 50;

        public const int DefaultPoolSize = 1;

        public const Die DefaultShape = Die.D6;

        const int TargetDiceInFlight = 48;

        public IReadOnlyList<DieBody> SceneDice { get; set; }

        // pool member i uses point i, so a pool cannot exceed the number of points
        public IReadOnlyList<Node3D> ThrowPoints { get; set; }

        public int Throws { get; set; } = DefaultThrows;

        // dice thrown together and colliding; 1 means each die is alone
        public int PoolSize { get; set; } = DefaultPoolSize;

        public Die Shape { get; set; } = DefaultShape;

        public int Pools { get; set; }

        // set by DiceTray from the skin it applied, so the report names the tray actually measured
        // no default tray: the scene's own skin is the default; 'unset' shows only if DiceTray forgets to set it
        public string TrayName { get; set; } = "unset";

        public double StuckSeconds { get; set; } = 10.0;

        sealed class PoolRun
        {
            public readonly List<DieBody> Dice = new();
            public double LaunchedAt;
            public bool InFlight;
        }

        readonly List<PoolRun> _pools = new();

        FaceTally _tally;

        FaceTally[] _bySeat;

        int _recorded;
        int _nudges;
        int _cockedRethrows;
        int _acceptedCocked;
        int _leftTray;
        int _stuck;
        double _elapsed;
        int _nextReportAt;
        bool _finished;

        public override void _Ready()
        {
            if (SceneDice == null || SceneDice.Count == 0 || ThrowPoints == null || ThrowPoints.Count == 0)
                throw new InvalidOperationException("DiceFairness needs at least one die and one throw point.");

            PoolSize = Math.Max(1, PoolSize);

            if (PoolSize > ThrowPoints.Count)
                throw new InvalidOperationException(
                    $"A pool of {PoolSize} needs {PoolSize} throw points; the scene has {ThrowPoints.Count}. " +
                    "Colliding dice must not spawn inside each other.");

            if (Pools <= 0)
                Pools = PoolSize == 1 ? DefaultSoloPools : Math.Max(1, TargetDiceInFlight / PoolSize);

            if (PoolSize > 1 && Pools > MaxCollidingPools)
            {
                GD.Print($"fairness: {Pools} colliding pools would need {Pools} collision layers; capping at {MaxCollidingPools}");
                Pools = MaxCollidingPools;
            }

            int sides = Shape.Sides();
            _tally = new FaceTally(sides);
            _bySeat = Enumerable.Range(0, PoolSize).Select(_ => new FaceTally(sides)).ToArray();
            _nextReportAt = Math.Max(1, Throws / 10);

            DieBody template = SceneDice[0];
            Node parent = template.GetParent();

            // set the template's shape before cloning, so every copy is right
            template.ShowNumbers = false;
            template.Size = Shape;

            // scene dice fill pool zero; the rest are benched before any throw
            int reused = Math.Min(PoolSize, SceneDice.Count);
            for (int i = reused; i < SceneDice.Count; i++) Bench(SceneDice[i]);

            for (int p = 0; p < Pools; p++)
            {
                var pool = new PoolRun();

                for (int seat = 0; seat < PoolSize; seat++)
                {
                    DieBody die = p == 0 && seat < reused
                        ? SceneDice[seat]
                        : Clone(template, parent, p, seat);

                    Enlist(die, p);
                    pool.Dice.Add(die);
                }

                _pools.Add(pool);
            }

            if (reused < SceneDice.Count)
                GD.Print($"fairness: benched {SceneDice.Count - reused} scene dice the sweep doesn't need");

            GD.Print(PoolSize == 1
                ? $"fairness: {Throws} throws of a d{sides}, {Pools} isolated dice at a time"
                : $"fairness: {Throws} throws of a d{sides}, pools of {PoolSize} COLLIDING dice, " +
                  $"{Pools} pools at a time ({Pools * PoolSize} dice)");

            GD.Print($"fairness: tray '{TrayName}'");

            foreach (PoolRun pool in _pools) Launch(pool);
        }

        DieBody Clone(DieBody template, Node parent, int poolIndex, int seat)
        {
            var clone = (DieBody)template.Duplicate();
            clone.Name = $"P{poolIndex:00}D{seat}";
            parent.AddChild(clone);

            // Duplicate copies exports but not the tray binding, so a clone must be re-bound or its 'left the tray' count is meaningless
            clone.BoundLike(template);

            return clone;
        }

        static void Bench(DieBody die)
        {
            die.CollisionLayer = 0;
            die.CollisionMask = 0;
            die.Freeze = true;
            die.Visible = false;
        }

        void Enlist(DieBody die, int poolIndex)
        {
            die.ShowNumbers = false;
            die.Size = Shape;

            if (PoolSize == 1)
            {
                // solo dice see the tray and nothing else
                die.CollisionLayer = SoloLayer;
                die.CollisionMask = TrayLayer;
            }
            else
            {
                // a layer per pool, so members collide with each other but not across pools
                uint layer = 1u << (poolIndex + 1);
                die.CollisionLayer = layer;
                die.CollisionMask = layer | TrayLayer;
            }

            die.LogSettles = false;

            die.ReportContacts = false;

            // one handler each: Nudged and Cocked were once one signal, which inflated this counter ~3x
            die.Nudged += (_, _) => _nudges++;
            die.Cocked += (_, _) => _cockedRethrows++;
            die.LeftTray += _ => _leftTray++;
        }

        void Launch(PoolRun pool)
        {
            if (_recorded >= Throws) return;

            pool.InFlight = true;
            pool.LaunchedAt = _elapsed;

            // same frame for the whole pool: that is the experiment when they collide
            for (int seat = 0; seat < pool.Dice.Count; seat++)
                pool.Dice[seat].Throw(ThrowPoints[seat].GlobalTransform);
        }

        public override void _Process(double delta)
        {
            if (_finished) return;

            _elapsed += delta;

            foreach (PoolRun pool in _pools)
            {
                if (!pool.InFlight) continue;

                if (pool.Dice.All(d => d.IsSettled && d.IsAtRest))
                {
                    Record(pool);
                    if (_recorded >= Throws) { Conclude(); return; }
                    Launch(pool);
                    continue;
                }

                if (_elapsed - pool.LaunchedAt > StuckSeconds)
                {
                    _stuck++;
                    GD.Print($"fairness: a pool never came to rest after {StuckSeconds}s - re-throwing ({_stuck} so far)");
                    Launch(pool);
                }
            }
        }

        void Record(PoolRun pool)
        {
            pool.InFlight = false;

            for (int seat = 0; seat < pool.Dice.Count; seat++)
            {
                // read live, the same way DiceTray does, so it measures the game's number
                (int value, float alignment) = pool.Dice[seat].ReadFace();

                if (alignment < pool.Dice[seat].RequiredAlignment) _acceptedCocked++;

                _tally.Add(value);
                _bySeat[seat].Add(value);
                _recorded++;
            }

            if (_recorded >= _nextReportAt && _recorded < Throws)
            {
                GD.Print($"fairness: {_recorded}/{Throws} after {_elapsed:0.0}s - {_tally}");
                _nextReportAt += Math.Max(1, Throws / 10);
            }
        }

        // named apart from the base Finish: this is the whole report, and base Finish is its last line
        void Conclude()
        {
            _finished = true;

            GD.Print("");
            foreach (string line in _tally.DebugLines()) GD.Print(line);

            // each seat judged on its own: a pool can look uniform while a middle seat, hit from both sides, is skewed
            if (PoolSize > 1)
            {
                GD.Print("");
                for (int seat = 0; seat < PoolSize; seat++)
                    GD.Print($"seat {seat + 1} (from {ThrowPoints[seat].Name}): {_bySeat[seat]} | " +
                             $"chi-squared {_bySeat[seat].ChiSquare:0.00} | {_bySeat[seat].Verdict}");
            }

            GD.Print("");
            // the maths is on FaceTally (tested in core); this file only knows the subject is a tray
            GD.Print($"average pip {_tally.MeanPip:0.0000} against {_tally.ExpectedMeanPip:0.0000} " +
                     $"expected - {_tally.MeanDriftZ:+0.00;-0.00} standard errors ({DriftSentence(_tally)})");

            GD.Print("");
            GD.Print($"took {_elapsed:0.0}s | nudges {_nudges} | cocked re-throws {_cockedRethrows} | " +
                     $"cocked accepted anyway {_acceptedCocked} | left the tray {_leftTray} | stuck pools {_stuck}");

            if (_acceptedCocked > 0)
                GD.Print($"WARNING: {_acceptedCocked} of {_recorded} results came off a die that " +
                         "never lay flat. Those faces are guesses, not readings.");

            bool biased = _tally.Verdict == Fairness.Biased
                       || _bySeat.Any(t => t.Verdict == Fairness.Biased)
                       || _tally.DriftVerdict == Fairness.Biased;

            // verdict and exit code come off HeadlessCheck, so checks cannot disagree on what 0 and 1 mean
            Finish(!biased);
        }

        // numbers from FaceTally; the tray wording is this file's, because core knows no tray
        static string DriftSentence(FaceTally tally) => tally.DriftVerdict switch
        {
            Fairness.Biased => "BIASED - this tray rolls " + (tally.DriftsHigh ? "high" : "low"),
            Fairness.Suspicious => "suspicious, throw more at it",
            _ => "no directional drift",
        };

        // --parallel is only a speed knob; it changes nothing about the measurement
        public static bool RequestedFrom(
            IEnumerable<string> args,
            out int throws, out int poolSize, out int pools, out Die shape, out string tray)
        {
            throws = DefaultThrows;
            poolSize = DefaultPoolSize;
            pools = 0;
            shape = DefaultShape;

            // null means leave the scene's skin alone
            tray = null;

            bool asked = false;

            foreach (string arg in args ?? Enumerable.Empty<string>())
            {
                string[] parts = arg.Split('=', 2);

                int value = 0;
                bool hasValue = parts.Length == 2 && int.TryParse(parts[1], out value);

                switch (parts[0])
                {
                    case "--fairness":
                        asked = true;
                        if (hasValue) throws = value;
                        break;

                    case "--dice":
                        if (hasValue) poolSize = Math.Max(1, value);
                        break;

                    case "--shape":
                        if (!hasValue || !Enum.IsDefined(typeof(Die), value) || !((Die)value).IsReal())
                            throw new ArgumentException(
                                $"--shape wants a number of sides the game has a die for: 4, 6, 8, 10 or 12. Got '{arg}'.");

                        shape = (Die)value;
                        break;

                    case "--parallel":
                        if (hasValue) pools = Math.Max(1, value);
                        break;

                    case "--tray":
                        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[1]))
                            throw new ArgumentException($"--tray wants the bare name of a skin, e.g. --tray=wood. Got '{arg}'.");

                        tray = parts[1];
                        break;
                }
            }

            return asked;
        }
    }
}
