using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Game.Access
{
    // WHICH KEY DOES WHICH ACT, AND HOW YOU CHANGE ONE (AX1).
    //
    // Full key rebinding, which is a standing requirement. Pure, so the three things that make
    // rebinding either safe or a trap can be held by a test rather than by care:
    //
    //   - A KEY DOES ONE THING. Binding a key that is already taken is refused and says what has
    //     it, rather than quietly leaving two acts on it and letting whichever is asked first win
    //   - AN ESSENTIAL ACT CANNOT BE UNBOUND. Reaching and pressing are how a keyboard player gets
    //     anywhere at all; a settings page that can strand somebody in the room with no way to
    //     touch anything is worse than one with no rebinding in it
    //   - IT IS WRITTEN DOWN AS TEXT, act by act, so a file from an older build that has never heard
    //     of an act still loads and that act keeps its default
    //
    // REBINDING IS ARM-THEN-PRESS, which is the only way that works without a modal dialog: you
    // point at the line in the book, and the next key you press is the one it takes. So the page
    // needs no OK, no Cancel and no overlay - which is as well, because this game has none of those.
    public sealed class Bindings
    {
        public readonly struct Bound : IEquatable<Bound>
        {
            public Bound(Key key, bool shift = false)
            {
                Key = key;
                Shift = shift;
            }

            public Key Key { get; }

            public bool Shift { get; }

            public bool Any => Key != Key.None;

            public bool Equals(Bound other) => Key == other.Key && Shift == other.Shift;

            public override bool Equals(object other) => other is Bound it && Equals(it);

            public override int GetHashCode() => HashCode.Combine(Key, Shift);

            // WHAT IS PRINTED ON THE KEY, and it is not localized: "Tab" is what that key says on
            // every keyboard that has one, and a translated word here would name a key the player
            // cannot find. The same call the "?" makes about its own glyph
            public override string ToString() =>
                !Any ? "-" : (Shift ? "Shift+" : "") + Enum.GetName(Key);
        }

        readonly Dictionary<Act, Bound> _bound = new Dictionary<Act, Bound>();

        public Bindings()
        {
            Plainly();
        }

        public void Plainly()
        {
            _bound.Clear();

            foreach (Act act in Enum.GetValues<Act>())
                _bound[act] = new Bound(act.Standard(), act.Shifted());
        }

        public Bound Of(Act act) => _bound.TryGetValue(act, out Bound bound) ? bound : default;

        // in the enum's order, so a page listing them cannot leave one out
        public IEnumerable<KeyValuePair<Act, Bound>> All
        {
            get
            {
                foreach (Act act in Enum.GetValues<Act>())
                    yield return new KeyValuePair<Act, Bound>(act, Of(act));
            }
        }

        public int Changed => Enum.GetValues<Act>()
                                  .Count(a => !Of(a).Equals(new Bound(a.Standard(), a.Shifted())));

        // which act already has this key, if any
        public Act? Clash(Bound key, Act except)
        {
            if (!key.Any) return null;

            foreach (Act act in Enum.GetValues<Act>())
                if (act != except && Of(act).Equals(key)) return act;

            return null;
        }

        // refused rather than forced: the caller is expected to say what has the key
        public bool Bind(Act act, Bound key)
        {
            if (!key.Any && act.Essential()) return false;

            if (Clash(key, act) != null) return false;

            _bound[act] = key;

            Armed = null;

            return true;
        }

        public bool Bind(Act act, Key key, bool shift = false) => Bind(act, new Bound(key, shift));


        // ---- arm, then press ------------------------------------------------------------------

        // which line in the book is waiting for a key. Null when nothing is
        public Act? Armed { get; private set; }

        public void Arm(Act act) => Armed = act;

        public void Disarm() => Armed = null;

        // THE NEXT KEY PRESSED, WHATEVER IT IS. Escape lets go - it is the one key that cannot be
        // bound, because a rebinding surface you cannot back out of is a trap with a paper texture
        // on it. Returns what happened, so the page can say it.
        public enum Took
        {
            // nothing was armed
            Nothing,

            Bound,

            LetGo,

            // another act has that key
            Taken,
        }

        public Took Pressed(Key key, bool shift = false)
        {
            if (Armed is not { } act) return Took.Nothing;

            if (key == Key.Escape) { Armed = null; return Took.LetGo; }

            var want = new Bound(key, shift);

            if (Clash(want, act) != null) return Took.Taken;

            return Bind(act, want) ? Took.Bound : Took.Taken;
        }


        // ---- written down --------------------------------------------------------------------

        public const char Between = '=';

        public const string WithShift = "+shift";

        public IEnumerable<string> Lines()
        {
            foreach (KeyValuePair<Act, Bound> one in All)
                yield return one.Key.Word() + Between +
                             (one.Value.Any ? Enum.GetName(one.Value.Key).ToLowerInvariant() : "") +
                             (one.Value.Shift ? WithShift : "");
        }

        // a line naming an act this build does not have is skipped rather than refused: a settings
        // file is the player's and outlives any one version of the game
        public int Read(IEnumerable<string> lines)
        {
            int took = 0;

            foreach (string line in lines ?? Array.Empty<string>())
            {
                int at = line?.IndexOf(Between) ?? -1;

                if (at <= 0) continue;

                if (!Acts.TryWord(line.Substring(0, at), out Act act)) continue;

                string said = line.Substring(at + 1).Trim().ToLowerInvariant();

                bool shift = said.EndsWith(WithShift, StringComparison.Ordinal);

                if (shift) said = said.Substring(0, said.Length - WithShift.Length);

                if (said.Length == 0)
                {
                    if (Bind(act, new Bound(Key.None))) took++;
                    continue;
                }

                if (!Enum.TryParse(said, ignoreCase: true, out Key key)) continue;

                if (Bind(act, new Bound(key, shift))) took++;
            }

            return took;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"keys: {string.Join(", ", All.Select(b => $"{b.Key.Word()}={b.Value}"))}" +
            (Armed is { } armed ? $" (waiting on {armed.Word()})" : "");
    }
}
