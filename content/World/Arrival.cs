using Content.Places;
using Core.Space;

namespace Content.World
{
    public sealed class Arrival : Happened
    {
        Arrival(string why)
        {
            Why = why ?? "";
        }

        public Arrival(Place place, Cell hero, bool first)
        {
            Place = place;
            Hero = hero;
            First = first;
        }

        public static Arrival No(string why) => new Arrival(why);

        public Place Place { get; }

        public Cell Hero { get; }

        public bool First { get; }

        public bool Refused => Place == null;

        // developer diagnostic, not a player-facing line
        public string Why { get; } = "";

        public override string ToString() =>
            Refused ? $"did not arrive - {Why}"
                    : $"in {Place.Id} at {Hero}" + (First ? ", for the first time" : "") + Said();
    }
}
