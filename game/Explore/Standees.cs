using System.Collections.Generic;
using Godot;
using Content.Campaigns;
using Content.World;
using Core.Characters;
using Core.Space;
using Game.Board;

namespace Game.Explore
{
    // EVERYBODY STANDING IN THE PLACE, AS FIGURES ON THE MAT.
    //
    // The board has only ever put foes down, because until there was a world outside a fight a
    // figure on the map was a thing to hit. A man on a quay is on the map in exactly the same
    // sense and gets exactly the same figure - which is the point, because you cannot tell from
    // the table which of them the author marked attackable, and that is how a table works.
    //
    // It owns nothing durable: the world rebuilds who is present after anything that changes a
    // fact, and this lays that out again. A figure lifted off is not remembered anywhere.
    public sealed class Standees
    {
        readonly Board.Board _board;

        readonly MiniMaker _minis;

        readonly Game.Campaigns.Library _shelf;

        readonly Dictionary<int, Mini> _standing = new Dictionary<int, Mini>();

        public Standees(Board.Board board, MiniMaker minis, Game.Campaigns.Library shelf)
        {
            _board = board;
            _minis = minis;
            _shelf = shelf;
        }

        public string Campaign { get; set; } = "";

        public IReadOnlyDictionary<int, Mini> Standing => _standing;

        public Mini Of(int slot) => _standing.TryGetValue(slot, out Mini mini) ? mini : null;

        public int Count => _standing.Count;

        // what is on the mat now, laid out fresh. Cheaper than reconciling and it cannot drift:
        // the world's list is the only list
        public void Lay(Exploring world)
        {
            Clear();

            if (world?.Where == null || _board == null) return;

            foreach (Present present in world.OnTheTable)
            {
                Mini figure = Figure(present);

                if (figure == null) continue;

                Mini stood = _board.Place(figure, present.At, $"Slot{present.Slot}");

                if (stood == null) continue;

                _standing[present.Slot] = stood;
            }

            GD.Print($"standing {_standing.Count} figure(s) on the mat in {world.Where.Id}");
        }

        Mini Figure(Present present)
        {
            if (present.IsAFoe)
            {
                string scoped = ContentId.Scoped(Campaign, present.Id);

                Tier tier = _shelf != null && _shelf.Has(scoped)
                    ? _shelf.Create(scoped).Tier
                    : Tier.Rival;

                return _minis.Make(Campaign, _shelf?.MiniFor(scoped), tier, present.Id);
            }

            // A PERSON IS ONE FIGURE. Rabble is a crowd pawn and means "several of these", which
            // is the wrong thing to say about the only carpenter in the village
            return _minis.Make(Campaign, present.What.Mini, Tier.Rival, present.Id);
        }

        public void Clear()
        {
            foreach (Mini standing in _standing.Values) _board?.Lift(standing);

            _standing.Clear();
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"{_standing.Count} standing on the mat";
    }
}
