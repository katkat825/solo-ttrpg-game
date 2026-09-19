using System;
using Godot;

namespace Game.Access
{
    // WHERE THE BINDINGS MEET THE ENGINE (AX1).
    //
    // Bindings is pure and knows which key does what; the InputMap is Godot's and knows nothing until
    // it is told. This is the one place the two are put together, so neither has to know about the
    // other - and so the InputMap is written from the bindings rather than the bindings being read
    // back out of the InputMap, which would make the file on disk a copy of engine state.
    //
    // EXACT MATCHING, AND IT MATTERS MORE THAN IT LOOKS. Reaching forward is Tab and reaching back is
    // Shift+Tab, and Godot will happily match an action bound to plain Tab when Shift is also down -
    // so without exact matching, reaching backward reached both ways at once and the hand never moved.
    public static class Keyboard
    {
        // every act written into the InputMap, replacing whatever was there. Returns how many got a
        // key; an act with none is reachable another way or is deliberately unbound
        public static int Install(Bindings keys)
        {
            if (keys == null) return 0;

            int bound = 0;

            foreach (System.Collections.Generic.KeyValuePair<Act, Bindings.Bound> one in keys.All)
            {
                string action = one.Key.Word();

                if (!InputMap.HasAction(action)) InputMap.AddAction(action);
                else InputMap.ActionEraseEvents(action);

                if (!one.Value.Any) continue;

                InputMap.ActionAddEvent(action, new InputEventKey
                {
                    // PHYSICAL, not the keycode: the key in that position on the keyboard, so a
                    // player on a French or Dvorak layout gets the key they actually pressed
                    PhysicalKeycode = one.Value.Key,
                    ShiftPressed = one.Value.Shift,
                });

                bound++;
            }

            return bound;
        }

        // which act this event is, if any. Exact, for the reason above
        public static Act? Pressed(InputEvent what)
        {
            if (what == null) return null;

            foreach (Act act in Enum.GetValues<Act>())
                if (what.IsActionPressed(act.Word(), exactMatch: true)) return act;

            return null;
        }

        // the key and the shift of a raw press, for the arm-then-press rebinding. Physical again, and
        // echoes are ignored: holding a key down should bind it once
        public static bool Raw(InputEvent what, out Key key, out bool shift)
        {
            key = Key.None;
            shift = false;

            if (what is not InputEventKey press || !press.Pressed || press.Echo) return false;

            key = press.PhysicalKeycode != Key.None ? press.PhysicalKeycode : press.Keycode;
            shift = press.ShiftPressed;

            return key != Key.None;
        }
    }
}
