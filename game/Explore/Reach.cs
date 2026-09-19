using System.Collections.Generic;
using Content.Places;
using Content.World;
using Core.Space;

namespace Game.Explore
{
    // WHAT A CLICK ON A SQUARE MEANS WHEN YOU ARE WALKING A PLACE RATHER THAN FIGHTING IN IT.
    //
    // A fight already answers this its own way - beside a foe is a swing, anywhere else is a move -
    // and exploring has to answer it too, on the same board, with the same click. There are four
    // answers and no more, which is what keeps the board from growing a mode switch: walk there,
    // go up to somebody and deal with them, step out through a way that is open, or find out the
    // way is shut.
    //
    // Pure, and it reads the world rather than changing it: working out what a click means must
    // never be the thing that moves the hero. The caller does the moving, having been told.
    public enum Means
    {
        // off the board, walled off, or there is nothing there to do
        Nothing,

        Walk,

        // go up to whoever is standing there; what they offer is the response system's to put out
        Reach,

        Leave,

        // a way out that this campaign's facts have not opened yet
        Shut,
    }

    public sealed class Reach
    {
        Reach(Means means, Cell stand)
        {
            Is = means;
            Stand = stand;
        }

        public Means Is { get; }

        // the square the hero ends up on. For a Reach that is a square BESIDE the thing, because
        // you do not walk through somebody to talk to them
        public Cell Stand { get; }

        public Present It { get; private set; }

        public Exit Way { get; private set; }

        // developer diagnostic, never a player-facing line
        public string Why { get; private set; } = "";

        public bool Refused => Is == Means.Nothing;

        public static Reach No(Cell at, string why) =>
            new Reach(Means.Nothing, at) { Why = why ?? "" };

        public static Reach Of(Exploring world, Cell clicked)
        {
            if (world == null) return No(clicked, "there is no world being walked");

            if (world.Where == null) return No(clicked, "nowhere has been entered yet");

            if (world.InAFight)
                return No(clicked, "there is a fight going on - the fight owns the board");

            if (world.Map == null || !world.Map.Contains(clicked))
                return No(clicked, "that is not a square on this map");

            Present it = world.At(clicked);

            if (it != null) return Toward(world, it, clicked);

            if (clicked == world.Hero) return No(clicked, "the hero is already standing there");

            if (world.Route(clicked) == null)
                return No(clicked, "there is no way there from where the hero is standing");

            return Out(world, clicked);
        }

        // a thing standing on the board is reached from beside it, never from its own square
        static Reach Toward(Exploring world, Present it, Cell clicked)
        {
            if (world.Hero == clicked) return No(clicked, "the hero cannot be reached");

            Cell? beside = Beside(world, clicked);

            if (beside == null)
                return No(clicked, $"there is no square beside '{it.Id}' the hero can reach");

            return new Reach(Means.Reach, beside.Value) { It = it };
        }

        // the nearest reachable square next to it, measured by the route the hero would actually
        // walk rather than by how far away it looks
        public static Cell? Beside(Exploring world, Cell thing)
        {
            Cell? nearest = null;
            int shortest = int.MaxValue;

            foreach (Cell side in Round(thing))
            {
                if (!world.Map.Contains(side) || !world.Map.IsPassable(side)) continue;

                if (!world.Map.CanCross(side, thing)) continue;

                if (side == world.Hero) return side;

                if (world.Occupied(side)) continue;

                IReadOnlyList<Cell> route = world.Route(side);

                if (route == null || route.Count >= shortest) continue;

                shortest = route.Count;
                nearest = side;
            }

            return nearest;
        }

        static IEnumerable<Cell> Round(Cell cell)
        {
            yield return new Cell(cell.X, cell.Y - 1);
            yield return new Cell(cell.X + 1, cell.Y);
            yield return new Cell(cell.X, cell.Y + 1);
            yield return new Cell(cell.X - 1, cell.Y);
        }

        // a square with a way out drawn on it is a way out; one without is somewhere to stand
        static Reach Out(Exploring world, Cell clicked)
        {
            foreach (Exit exit in world.Where.Exits)
            {
                Cell? at = world.Map.SpawnAt(exit.Slot);

                if (at == null || at.Value != clicked) continue;

                return exit.Needs.Met(world.Facts)
                    ? new Reach(Means.Leave, clicked) { Way = exit }
                    : new Reach(Means.Shut, clicked) { Way = exit };
            }

            return new Reach(Means.Walk, clicked);
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() => Is switch
        {
            Means.Nothing => $"nothing at {Stand} - {Why}",
            Means.Walk => $"walk to {Stand}",
            Means.Reach => $"up to '{It?.Id}', standing on {Stand}",
            Means.Leave => $"out of {Stand} to '{Way?.To}'",
            Means.Shut => $"the way out of {Stand} to '{Way?.To}' is not open",
            _ => Is.ToString(),
        };
    }
}
