using System;
using System.Collections.Generic;

namespace Game.Access
{
    // WHERE YOUR HAND IS WHEN THERE IS NO MOUSE UNDER IT (AX1).
    //
    // Everything the player can do by clicking, they can do by keyboard, and in a room made of
    // objects that means one thing: a hand that moves from object to
    // object and presses the one it is on. Not a focus rectangle travelling a widget tree - there
    // are no widgets - and not a cursor driven by arrow keys, which is a mouse with extra steps.
    //
    // Pure, because the two properties that make keyboard nav either usable or infuriating are both
    // about bookkeeping and neither is about drawing:
    //
    //   - THE HAND STAYS ON WHAT IT WAS ON when the list is gathered again. The room re-gathers
    //     whenever anything changes - a page is turned, a book is shelved - and a hand that fell
    //     back to the first object every time would make the book unusable by keyboard while
    //     staying perfectly usable by mouse, which is exactly the bug nobody notices
    //   - A MOUSE MOVES THE SAME HAND. Reaching with the keyboard and then clicking elsewhere must
    //     not leave two ideas of where you are, or the next Tab jumps somewhere you have not been
    //     looking
    //
    // A thing that is reachable but not live - a line of the story you read rather than press - is
    // skipped by the hand and still read out. That is what keeps the log readable aloud without
    // making it twelve stops on the way to the next page.
    public sealed class Pointing
    {
        IReadOnlyList<Reachable> _ring = Array.Empty<Reachable>();

        int _at = -1;

        public IReadOnlyList<Reachable> Ring => _ring;

        public int Count => _ring.Count;

        // how many the hand can actually stop on
        public int Live
        {
            get
            {
                int live = 0;

                foreach (Reachable one in _ring) if (one.Live) live++;

                return live;
            }
        }

        public Reachable On => _at >= 0 && _at < _ring.Count ? _ring[_at] : null;

        public int At => _at;

        public bool Holding => On != null;

        // THE LIST, GATHERED AGAIN. Whatever the hand was on, it is still on - by the same body if
        // that body is still in the room, else by the same name, else nothing rather than something
        // arbitrary. A hand silently moved to a different object is worse than a hand let go of.
        public void Over(IReadOnlyList<Reachable> reachables)
        {
            Reachable was = On;

            _ring = reachables ?? Array.Empty<Reachable>();

            _at = was == null ? -1 : Again(was);
        }

        int Again(Reachable was)
        {
            if (was.Body != null)
                for (int at = 0; at < _ring.Count; at++)
                    if (_ring[at].Owns(was.Body)) return at;

            if (was.Called.Length > 0)
                for (int at = 0; at < _ring.Count; at++)
                    if (_ring[at].Live &&
                        string.Equals(_ring[at].Called, was.Called, StringComparison.Ordinal))
                        return at;

            return -1;
        }

        public void Nothing() => _at = -1;

        public bool Next() => Step(1);

        public bool Back() => Step(-1);

        // wraps, because a ring of objects on a table has no first and no last - and stops rather
        // than spinning when there is nothing live to stop on
        bool Step(int way)
        {
            if (Live == 0) { _at = -1; return false; }

            int from = _at < 0 ? (way > 0 ? -1 : 0) : _at;

            for (int tried = 0; tried < _ring.Count; tried++)
            {
                from = ((from + way) % _ring.Count + _ring.Count) % _ring.Count;

                if (!_ring[from].Live) continue;

                _at = from;
                return true;
            }

            return false;
        }

        // the mouse arrived somewhere: the hand is there now, so the next reach carries on from it
        public bool Onto(Godot.GodotObject body)
        {
            if (body == null) return false;

            for (int at = 0; at < _ring.Count; at++)
            {
                if (!_ring[at].Owns(body)) continue;

                _at = at;
                return true;
            }

            return false;
        }

        public bool Press()
        {
            Reachable one = On;

            if (one == null || !one.Live) return false;

            one.Touch();

            return true;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            On == null
                ? $"nothing in hand, {Live} of {Count} reachable"
                : $"on {On} ({_at + 1} of {Count})";
    }
}
