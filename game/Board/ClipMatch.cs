using System;
using System.Collections.Generic;

namespace Game.Board
{
    // FINDING THE CLIP A MANIFEST NAMED, IN THE LIST THE ENGINE ACTUALLY IMPORTED
    // (MINIS_AND_ART.md A1, "the `FindLeaf`-style name lookup, generalised").
    //
    // A pack author writes `"move": "Walk"` after reading the clip out of their modelling tool.
    // What arrives in an `AnimationPlayer` is whatever the exporter wrote and the importer then
    // made of it, and the three of them do not always agree about the spelling:
    //
    //   Blender's glTF exporter writes `Armature|Walk` when an action is on a named armature
    //   some rigs ship `Walk_A`, `walk`, `CharacterArmature|Walk`
    //   Godot adds its own library prefix, so a name can arrive as `library/Walk`
    //
    // Refusing all of those would be technically correct and would mean almost every supplied
    // model animates on nothing, with the author certain they typed the name off the screen in
    // front of them. So the lookup widens in steps, and every step is narrower than a guess.
    //
    // IT IS PURE AND IT IS TESTED (`game.tests`), which is the whole reason it is a file of its
    // own rather than four lines inside `MiniMaker`: this is string matching, it has exactly the
    // kind of edge cases that are miserable to find by looking at a miniature, and `game.tests`
    // reaches anything in `game/` that needs no engine (CONVENTIONS.md).
    //
    // IT NEVER MATCHES NOTHING TO SOMETHING. An empty name, or a name nothing resembles, comes
    // back empty - and an empty answer is the procedural motion, which is missing polish and never
    // a broken fight.
    public static class ClipMatch
    {
        // Blender and Godot both use these to qualify a clip with what it belongs to
        static readonly char[] Qualifiers = { '|', '/' };

        // <paramref name="wanted"/> is what the manifest wrote; <paramref name="have"/> is what the
        // model turned out to carry. Returns the name to ask the player for, or empty
        public static string In(IEnumerable<string> have, string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted) || have == null) return "";

            var clips = new List<string>();

            foreach (string clip in have)
                if (!string.IsNullOrEmpty(clip)) clips.Add(clip);

            string name = wanted.Trim();

            // 1. THE NAME, EXACTLY. The overwhelmingly common case, and the only one that needs no
            //    justification - a pack whose clips validate against `ModelReader` lands here
            foreach (string clip in clips)
                if (string.Equals(clip, name, StringComparison.Ordinal)) return clip;

            // 2. the same name in a different case. `Walk` and `walk` are one clip and nobody who
            //    typed the second meant a different one
            foreach (string clip in clips)
                if (string.Equals(clip, name, StringComparison.OrdinalIgnoreCase)) return clip;

            // 3. THE PART AFTER THE QUALIFIER. `Armature|Walk` is the exporter saying which rig
            //    the action was on, which is a fact about the file and not about the motion
            foreach (string clip in clips)
                if (string.Equals(Unqualified(clip), name, StringComparison.OrdinalIgnoreCase))
                    return clip;

            // 4. and the same from the other side, for an author who copied the qualified name out
            //    of Blender while the importer dropped it
            foreach (string clip in clips)
                if (string.Equals(clip, Unqualified(name), StringComparison.OrdinalIgnoreCase))
                    return clip;

            // AND NO FIFTH STEP. A substring match would make `Walk` find `Walk_Backwards` and
            // `Idle` find `Idle_Dead`, which is a piece animating on the wrong clip - worse than
            // one animating on none, because nothing about it looks like a mistake
            return "";
        }

        public static string Unqualified(string clip)
        {
            if (string.IsNullOrEmpty(clip)) return "";

            int at = clip.LastIndexOfAny(Qualifiers);

            return at >= 0 && at + 1 < clip.Length ? clip.Substring(at + 1) : clip;
        }
    }
}
