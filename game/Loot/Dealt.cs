using System.Collections.Generic;
using Godot;

namespace Game.Loot
{
    // WHAT YOU ARE CARRYING, AS A STACK OF CARDS BESIDE YOU.
    //
    // The DM hands you a card for anything you gain and it goes on the stack; you turn one over to
    // read what it is. There is no inventory panel and there is no list - a stack of cards on a
    // table is what an inventory has always been at a real table, and it is what this is.
    //
    // Pure, so where the cards lie and what turning one over means can be read and tested without
    // an engine. The rule that does the work is that only ONE is ever face up: a fan of open cards
    // is a list again, and turning the second one over puts the first one back.
    public sealed class Dealt
    {
        // how far along the stack each card is offset, so the one under is visible as an edge
        public const float Step = 0.010f;

        // HOW FAR THE STACK MAY SPREAD before the cards start sitting on top of one another. Past
        // this it is a pile rather than a fan, because a stack that grows without bound walks off
        // the table by the third dungeon
        public const int MostSpread = 8;

        // how far a turned card comes up off the stack to be read
        public const float Raised = 0.012f;

        readonly List<string> _cards = new List<string>();

        public IReadOnlyList<string> Cards => _cards;

        public int Count => _cards.Count;

        // which card is turned over, or -1 for a square stack
        public int FaceUp { get; private set; } = -1;

        public string Reading => FaceUp >= 0 ? _cards[FaceUp] : "";

        // two of the same thing are two cards. A stack is what you are carrying, not a set
        public int Deal(string item)
        {
            if (string.IsNullOrWhiteSpace(item)) return -1;

            _cards.Add(item);

            return _cards.Count - 1;
        }

        public string At(int index) =>
            index >= 0 && index < _cards.Count ? _cards[index] : "";

        public bool Holds(int index) => index >= 0 && index < _cards.Count;

        // turning the one that is already up turns it back down, which is how you stop reading
        public bool Flip(int index)
        {
            if (!Holds(index)) return false;

            FaceUp = FaceUp == index ? -1 : index;

            return true;
        }

        public void Square() => FaceUp = -1;

        public bool Take(int index)
        {
            if (!Holds(index)) return false;

            _cards.RemoveAt(index);

            FaceUp = FaceUp == index ? -1
                   : FaceUp > index ? FaceUp - 1
                   : FaceUp;

            return true;
        }

        public void Sweep()
        {
            _cards.Clear();
            FaceUp = -1;
        }

        // where a card lies, in the stack's own space. Past MostSpread they stop moving along and
        // only lift a hair, which is what a pile of cards does
        public Vector3 Sits(int index)
        {
            if (!Holds(index)) return Vector3.Zero;

            int along = Mathf.Min(index, MostSpread);

            // every card sits a shade higher than the one under it, so no two faces are coplanar
            float height = 0.0004f * index + (index == FaceUp ? Raised : 0f);

            return new Vector3(Step * along, height, 0f);
        }

        // how wide the stack is on the table, which is the number that must stay a hand's width
        public float Spread => Step * Mathf.Min(Mathf.Max(_cards.Count - 1, 0), MostSpread);

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            _cards.Count == 0
                ? "carrying nothing"
                : $"carrying {_cards.Count}" +
                  (FaceUp >= 0 ? $", reading '{Reading}'" : ", stacked square");
    }
}
