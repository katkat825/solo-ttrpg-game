using System;
using System.Collections.Generic;

namespace Game.Board
{
    public static class ClipMatch
    {
        static readonly char[] Qualifiers = { '|', '/' };

        public static string In(IEnumerable<string> have, string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted) || have == null) return "";

            var clips = new List<string>();

            foreach (string clip in have)
                if (!string.IsNullOrEmpty(clip)) clips.Add(clip);

            string name = wanted.Trim();

            // 1. exact name, the common case
            foreach (string clip in clips)
                if (string.Equals(clip, name, StringComparison.Ordinal)) return clip;

            // 2. same name, different case
            foreach (string clip in clips)
                if (string.Equals(clip, name, StringComparison.OrdinalIgnoreCase)) return clip;

            // 3. the part after the qualifier: Armature|Walk matches Walk
            foreach (string clip in clips)
                if (string.Equals(Unqualified(clip), name, StringComparison.OrdinalIgnoreCase))
                    return clip;

            // 4. the other way: a qualified name against an unqualified clip
            foreach (string clip in clips)
                if (string.Equals(clip, Unqualified(name), StringComparison.OrdinalIgnoreCase))
                    return clip;

            // no fifth step: a substring match would pick Walk_Backwards for Walk
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
