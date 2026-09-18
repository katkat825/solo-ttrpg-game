using System.Collections.Generic;
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

        // ACCEPTED QUESTS THIS PLACE CAN MOVE ON, derived on the way in (Exploring.TurningInHere).
        //
        // It rides on the arrival rather than being asked for separately because the moment you
        // walk in is the only moment it is worth saying: the companion mentions it once, and a
        // player who ignores it is not told again every time they cross the square.
        public IReadOnlyList<string> Nearby => _nearby;

        readonly List<string> _nearby = new List<string>();

        public void Nudges(string quest)
        {
            if (!string.IsNullOrEmpty(quest) && !_nearby.Contains(quest)) _nearby.Add(quest);
        }

        // developer diagnostic, not a player-facing line
        public string Why { get; } = "";

        public override string ToString() =>
            Refused ? $"did not arrive - {Why}"
                    : $"in {Place.Id} at {Hero}" + (First ? ", for the first time" : "") +
                      (_nearby.Count > 0 ? $", and {string.Join(", ", _nearby)} turns in here" : "") +
                      Said();
    }
}
