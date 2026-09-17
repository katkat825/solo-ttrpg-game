using Content.Places;

namespace Content.World
{
    public sealed class Going
    {
        Going(string why)
        {
            Why = why ?? "";
        }

        public Going(string to, int arriving, Road road, Happening happening)
        {
            To = to ?? "";
            Arriving = arriving;
            Road = road;
            Happening = happening;
        }

        public static Going No(string why) => new Going(why);

        public string To { get; } = "";

        public int Arriving { get; }

        // null for a step through a door
        public Road Road { get; }

        // null for the quiet walk this should usually be
        public Happening Happening { get; }

        public bool Interrupted => Happening != null;

        // the journey did not finish; where you are now is not where you were going
        public bool Stopped => Happening != null && Happening.Stops;

        public bool Refused => To.Length == 0;

        public string Why { get; } = "";

        public string LineKey(string campaign) => Happening?.LineKey(campaign);

        public override string ToString() =>
            Refused ? $"did not go - {Why}"
                    : (Road == null ? $"through to {To}" : $"along {Road.Id} to {To}") +
                      (Arriving > 0 ? $", arriving on spawn {Arriving}" : "") +
                      (Interrupted ? $", but {Happening}" : "");
    }
}
