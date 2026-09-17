using Core.Dice;

namespace Game.Dm
{
    public sealed class SecretRoll
    {
        // HOW OFTEN THE DM ROLLS FOR NOTHING. The pair below averages one rattle a minute or so:
        // after the rest, the wait is 1/IdleChance seconds, so the gap is Rest + 1/Chance.
        //
        // The shipped defaults were 9 and 0.18 - a rattle every fifteen seconds - and the eye check
        // called it exactly what the line below warns about. A presence is something you notice; a
        // tic is something you stop hearing. Dm exports both, because the only way to tune this is
        // to sit at the table and move the number.
        public const double IdleChance = 0.025;

        // the pause after the rattle; the pause is the device, a second of not knowing what it meant
        public const double PauseSeconds = 0.9;

        // shortest gap between two idle rolls, so unlucky draws can't stack two rattles together
        public const double RestSeconds = 30.0;

        // granularity of the per-frame draw; large enough that a 144 Hz frame still has a non-zero chance
        const int Sides = 100000;

        readonly IRng _rng;

        readonly double _rest;

        readonly double _chance;

        double _since;

        // the rng must not be the fight's stream, or a roll for nothing shifts the dice a swing is about to get and breaks a seeded replay
        public SecretRoll(IRng rng, double rest = RestSeconds, double chance = IdleChance)
        {
            _rng = rng;

            // a rest of zero or a chance of zero means "never", which is a legitimate way to turn
            // the tic off entirely at the table; a negative one is a typo and reads as never too
            _rest = rest > 0.0 ? rest : RestSeconds;
            _chance = chance > 0.0 ? chance : 0.0;
            _since = _rest;
        }

        // seconds between rattles, on average; developer diagnostic, and the number worth arguing with
        public double Every => _chance <= 0.0 ? double.PositiveInfinity : _rest + 1.0 / _chance;

        public bool ForNothing(double delta)
        {
            if (_chance <= 0.0) return false;

            _since += delta;

            if (_since < _rest) return false;

            // rate is per second and delta is a frame, so the chance scales to the frame - or a 144 Hz DM twitches more than a 30 Hz one
            int chance = (int)(_chance * delta * Sides);

            if (chance <= 0 || _rng.Roll(Sides) > chance) return false;

            _since = 0.0;

            return true;
        }

        // marks a real hidden roll; resets the gap so an idle rattle can't stack on top
        public void Meant() => _since = 0.0;

        // developer only, never localized
        public override string ToString() =>
            _chance <= 0.0
                ? "the DM never rolls for nothing"
                : $"a rattle for nothing every {Every:0} s or so";
    }
}
