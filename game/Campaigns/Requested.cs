using System.Collections.Generic;
using Godot;

namespace Game.Campaigns
{
    public static class Requested
    {
        public const string CampaignArg = "--campaign=";

        public const string EncounterArg = "--encounter=";

        public const string HeroArg = "--hero=";

        public const string GrownArg = "--grown=";

        public static string Campaign => Value(CampaignArg);

        public static string Encounter => Value(EncounterArg);

        public static string Hero => Value(HeroArg);

        public static IReadOnlyList<string> Grown =>
            Value(GrownArg)
                .Split(',', System.StringSplitOptions.RemoveEmptyEntries |
                            System.StringSplitOptions.TrimEntries);

        static string Value(string flag)
        {
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg.StartsWith(flag, System.StringComparison.Ordinal))
                    return arg.Substring(flag.Length).Trim();

            return "";
        }
    }
}
