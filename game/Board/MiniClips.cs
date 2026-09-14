using System.Collections.Generic;
using Godot;
using Content.Minis;

namespace Game.Board
{
    // WHICH ANIMATION PLAYS WHEN THE PIECE DOES SOMETHING (MINIS_AND_ART.md A1).
    //
    // The manifest's clip map, bound to the `AnimationPlayer` that actually came out of the model.
    // `Mini` owns the five moments - placed, moved, struck, wobbled, toppled - and has owned them
    // since B1; this is the thin thing that turns one of those into a clip name and asks a player
    // to play it.
    //
    // THE CONTRACT DEGRADES AT EVERY STEP, which is the whole of A1's last bullet. A model with no
    // `AnimationPlayer` is a static piece; a piece whose manifest named no clip for a motion does
    // that motion procedurally; a clip that is named and missing is a warning ONCE and then the
    // procedural motion forever after. None of the three is an exception, and none of the three
    // stops a fight - "missing motion is missing polish, never a broken fight."
    //
    // WHY IT HOLDS NAMES RATHER THAN `Animation` OBJECTS. A resolved name is what an
    // `AnimationPlayer` is asked for anyway, and resolving once at load means the per-motion path
    // is a dictionary lookup rather than a search - which matters because `Play` is called on
    // every step of every move.
    public sealed class MiniClips
    {
        readonly AnimationPlayer _player;

        readonly Dictionary<Motion, string> _clips = new Dictionary<Motion, string>();

        MiniClips(AnimationPlayer player) => _player = player;

        // null when there is nothing to bind - no player, or no clip map - because a null here is
        // exactly what "this piece moves procedurally" means, and `Mini` already treats it that way
        public static MiniClips Over(Node figure, Mounted mounted)
        {
            if (figure == null || mounted == null || mounted.Clips.Count == 0) return null;

            AnimationPlayer player = Find(figure);

            if (player == null)
            {
                // WORTH SAYING ONCE. A manifest that names five clips over a model with no rig is
                // an author who exported the wrong thing, and they cannot see it from the table -
                // the piece just slides, which is what an unrigged piece is supposed to do
                GD.PushWarning($"mini: '{mounted.Id}' names {mounted.Clips.Count} clips and its " +
                               "model has no AnimationPlayer - the piece will move on the " +
                               "engine's own motion");
                return null;
            }

            var clips = new MiniClips(player);

            string[] have = player.GetAnimationList();

            foreach (KeyValuePair<Motion, string> named in mounted.Clips)
            {
                // the tolerant lookup, and its reasons, are in `ClipMatch` - an exporter's
                // `Armature|Walk` and an author's `Walk` are one clip
                string found = ClipMatch.In(have, named.Value);

                if (found.Length == 0)
                {
                    GD.PushWarning($"mini: '{mounted.Id}' names '{named.Value}' for " +
                                   $"{named.Key.ToString().ToLowerInvariant()} and its model has " +
                                   $"{string.Join(", ", have)} - that motion will be the engine's own");
                    continue;
                }

                clips._clips[named.Key] = found;
            }

            return clips._clips.Count > 0 ? clips : null;
        }

        // the first one under the model, which is where a glTF import puts it. Depth-first the
        // same way `PaintedModel.Meshes` walks, so the two agree about what "under here" means
        static AnimationPlayer Find(Node node)
        {
            if (node is AnimationPlayer player) return player;

            foreach (Node child in node.GetChildren())
            {
                AnimationPlayer deeper = Find(child);

                if (deeper != null) return deeper;
            }

            return null;
        }

        // TRUE WHEN SOMETHING WAS PLAYED, which is what lets `Mini` say "and otherwise do it the
        // procedural way" in one line at each of the five call sites
        public bool Play(Motion motion)
        {
            if (_player == null || !_clips.TryGetValue(motion, out string clip)) return false;

            _player.Play(clip);

            return true;
        }

        // DEVELOPER ONLY - not localized, never reaches the screen
        public override string ToString() => $"{_clips.Count} clips bound";
    }
}
