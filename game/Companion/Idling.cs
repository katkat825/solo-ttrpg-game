using System;
using Content.Dialogue;
using Core.Dice;

namespace Game.Companion
{
    // "Idle animation is ~90% of the value here" (THE_TABLE.md section 4). A dozen idles and a
    // handful of reactions read as a living creature - as long as the dozen are not a loop you can
    // count, which is what this is for: the idles are DEALT, exactly as the bark bank is, so every
    // one is seen before any is seen twice and none follows itself.
    //
    // Godot-free, so the thing that decides what a living creature does next is testable without
    // one. The scene only plays what it is told.
    public sealed class Idling
    {
        readonly ShuffleBag _bag;

        readonly IRng _rng;

        public Idling(int idles, IRng rng, double hold = DefaultHold, double jitter = DefaultJitter)
        {
            if (idles < 0) throw new ArgumentOutOfRangeException(nameof(idles));

            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _bag = new ShuffleBag(idles, _rng);

            Count = idles;
            Hold = hold;
            Jitter = jitter;

            Change();
        }

        // seconds; a little over three is where an idle stops reading as a twitch and starts
        // reading as a thing a creature decided to do
        public const double DefaultHold = 3.2;

        public const double DefaultJitter = 1.4;

        public int Count { get; }

        public double Hold { get; }

        // even spacing is the thing that gives a loop away, so every hold is drawn a bit different
        public double Jitter { get; }

        // 1-based, matching the idle's number in the model; 0 when there are none
        public int Idle { get; private set; }

        public double Left { get; private set; }

        public int Changes { get; private set; }

        // true on the frame the idle changed, so a scene can start the new animation exactly then
        public bool Tick(double delta)
        {
            if (Count == 0) return false;

            Left -= delta;

            if (Left > 0.0) return false;

            Change();

            return true;
        }

        // something happened: come out of the idle now rather than on its own schedule
        public void Break() => Left = 0.0;

        void Change()
        {
            Idle = _bag.Next();
            Changes++;

            // Roll is 1..n, so this lands in [Hold - Jitter/2, Hold + Jitter/2] and never below zero
            double spread = Jitter <= 0.0 ? 0.0 : Jitter * ((_rng.Roll(1000) - 1) / 999.0 - 0.5);

            Left = Math.Max(0.1, Hold + spread);
        }

        public override string ToString() =>
            Count == 0 ? "no idles" : $"idle {Idle} of {Count}, {Left:0.0}s left";
    }
}
