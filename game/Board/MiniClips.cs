using System.Collections.Generic;
using Godot;
using Content.Minis;

namespace Game.Board
{
    public sealed class MiniClips
    {
        readonly AnimationPlayer _player;

        readonly Dictionary<Motion, string> _clips = new Dictionary<Motion, string>();

        MiniClips(AnimationPlayer player) => _player = player;

        // null means bind nothing: no player or no clips, which is 'moves procedurally'
        public static MiniClips Over(Node figure, Mounted mounted)
        {
            if (figure == null || mounted == null || mounted.Clips.Count == 0) return null;

            AnimationPlayer player = Find(figure);

            if (player == null)
            {
                // warn once: clips named over a model with no rig is a wrong export; the piece just slides
                GD.PushWarning($"mini: '{mounted.Id}' names {mounted.Clips.Count} clips and its " +
                               "model has no AnimationPlayer - the piece will move on the " +
                               "engine's own motion");
                return null;
            }

            var clips = new MiniClips(player);

            string[] have = player.GetAnimationList();

            foreach (KeyValuePair<Motion, string> named in mounted.Clips)
            {
                // tolerant match: an exporter's 'Armature|Walk' and an author's 'Walk' are one clip
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

        // true when a clip played, so Mini can fall back to procedural
        public bool Play(Motion motion)
        {
            if (_player == null || !_clips.TryGetValue(motion, out string clip)) return false;

            _player.Play(clip);

            return true;
        }

        // developer only, not localized, never reaches the screen
        public override string ToString() => $"{_clips.Count} clips bound";
    }
}
