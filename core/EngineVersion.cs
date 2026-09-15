using System;

namespace Core
{
    // the rules version - a campaign names a minimum build so a too-new one is refused, not half-loaded
    public static class EngineVersion
    {
        public static readonly Version Current = new Version(0, 4);

        public static bool Satisfies(Version wanted) => wanted == null || wanted <= Current;
    }
}
