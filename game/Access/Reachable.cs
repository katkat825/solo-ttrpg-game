using System;
using System.Collections.Generic;
using Godot;

namespace Game.Access
{
    // ONE THING IN THE ROOM YOU CAN REACH, AND THE ONLY DESCRIPTION OF IT THERE IS.
    //
    // The room already knew how to be clicked: one raycast, and everything touchable asked in one
    // order. What it did not know was how to be reached any other way - and a keyboard walk built
    // beside the raycast would be a second list of what is in the room, free to fall behind the
    // first. That is the same mistake the locale audit made twice (V0), and it is worse here,
    // because the thing that falls behind is the only way a player without a mouse can play at all.
    //
    // So there is ONE list. A mouse finds the reachable whose body is under the cursor; a keyboard
    // walks the list in order; a screen reader reads the one the hand is on; and the hitbox sweep
    // measures every span in it. Four features, one description, and nothing to keep in step.
    //
    // It carries WORDS, already localized, exactly as the note and the bubbles do - a key becomes
    // text in one place and it is not here.
    public sealed class Reachable
    {
        public Reachable(string called, Action press, GodotObject body = null,
                         Vector3 span = default, bool live = true, IReadOnlyList<string> also = null,
                         Action<bool> lit = null)
        {
            Called = called ?? "";
            Press = press;
            Body = body;
            Span = span;
            Live = live;
            Also = also ?? Array.Empty<string>();
            Lit = lit;
        }

        // what a player would call it, in their own language. The name a screen reader reads
        public string Called { get; }

        // WHAT ELSE IS TRUE OF IT, as whole sentences. A state a sighted player reads off the object
        // - the value a settings line is set to, that a save tab is the one you are on - has to be
        // said out loud too, and it is said as complete sentences rather than assembled out of
        // fragments, because word order differs by language
        public IReadOnlyList<string> Also { get; }

        public Action Press { get; }

        // the collision body a raycast would hit. Null for a thing that has no body yet, which is
        // every one of these in a test
        public GodotObject Body { get; }

        // how big that body is, so "generous hitboxes" is a measurement rather than a promise
        public Vector3 Span { get; }

        // a line you read rather than press is reachable and not live: the hand skips it, and a
        // screen reader still says it
        public bool Live { get; }

        public bool Owns(GodotObject what) =>
            Body != null && what != null && ReferenceEquals(Body, what);

        // HOW IT SHOWS THAT YOUR HAND IS ON IT, which is the object's own affordance and not a
        // rectangle drawn round it: the thing lights up, exactly as it does under a cursor. A hand
        // you cannot see where it is is a hand you cannot use
        public Action<bool> Lit { get; }

        public void Light(bool lit) => Lit?.Invoke(lit);

        public void Touch() => Press?.Invoke();

        // developer only, not localized, never reaches the screen
        public override string ToString() =>
            $"\"{Called}\"{(Live ? "" : " (read only)")}" +
            (Span == default ? "" : $" {Span.X:0.000}x{Span.Z:0.000}m");
    }
}
