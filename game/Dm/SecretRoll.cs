using Core.Dice;

namespace Game.Dm
{
    public sealed class SecretRoll
    {
        // how often the DM rolls for nothing, per idle check; kept low or it becomes a nervous tic rather than a presence
        public const double IdleChance = 0.18;

        // the pause after the rattle; the pause is the device, a second of not knowing what it meant
        public const double PauseSeconds = 0.9;

        // shortest gap between two idle rolls, so unlucky draws can't stack two rattles together
        public const double RestSeconds = 9.0;

        // granularity of the per-frame draw; large enough that a 144 Hz frame still has a non-zero chance
        const int Sides = 100000;

        readonly IRng _rng;

        double _since = RestSeconds;

        // the rng must not be the fight's stream, or a roll for nothing shifts the dice a swing is about to get and breaks a seeded replay
        public SecretRoll(IRng rng) => _rng = rng;

        public bool ForNothing(double delta)
        {
            _since += delta;

            if (_since < RestSeconds) return false;

            // rate is per second and delta is a frame, so the chance scales to the frame - or a 144 Hz DM twitches more than a 30 Hz one
            int chance = (int)(IdleChance * delta * Sides);

            if (chance <= 0 || _rng.Roll(Sides) > chance) return false;

            _since = 0.0;

            return true;
        }

        // marks a real hidden roll; resets the gap so an idle rattle can't stack on top
        public void Meant() => _since = 0.0;
    }
}
