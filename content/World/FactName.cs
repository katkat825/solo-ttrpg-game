using System;
using System.Linq;

namespace Content.World
{
    public static class FactName
    {
        public const char Separator = '.';

        // enough for <subject>.<place>.<aspect> and one more; a fact that needs five is a sentence
        public const int MostSegments = 4;

        public static bool IsLocal(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;

            string[] parts = name.Split(Separator);

            return parts.Length >= 1 && parts.Length <= MostSegments && parts.All(IsSegment);
        }

        static bool IsSegment(string s) =>
            s.Length > 0 &&
            s.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');

        public static string Scoped(string campaign, string local)
        {
            if (!Campaigns.ContentId.IsCampaign(campaign))
                throw new ArgumentException($"'{campaign}' is not a campaign id.", nameof(campaign));

            if (!IsLocal(local))
                throw new ArgumentException($"'{local}' is not a fact.", nameof(local));

            return campaign + Separator + local;
        }

        public static string LocalOf(string campaign, string scoped)
        {
            if (string.IsNullOrEmpty(scoped)) return "";

            string prefix = campaign + Separator;

            return scoped.StartsWith(prefix, StringComparison.Ordinal)
                ? scoped.Substring(prefix.Length)
                : scoped;
        }


        // the engine's own derived aspects; adding one means adding it here and nowhere else

        public const string DeadAspect = "dead";
        public const string LootedAspect = "looted";
        public const string OpenAspect = "open";
        public const string SpokenAspect = "spoken";
        public const string VisitedAspect = "visited";
        public const string ClearedAspect = "cleared";
        public const string AcceptedAspect = "accepted";
        public const string HappenedAspect = "happened";

        public static readonly System.Collections.Generic.IReadOnlyList<string> Aspects = new[]
        {
            DeadAspect, LootedAspect, OpenAspect, SpokenAspect,
            VisitedAspect, ClearedAspect, AcceptedAspect, HappenedAspect,
        };

        public static string Dead(string entity) => Of(entity, DeadAspect);

        public static string Looted(string entity) => Of(entity, LootedAspect);

        public static string Open(string entity) => Of(entity, OpenAspect);

        public static string Spoken(string entity) => Of(entity, SpokenAspect);

        public static string Visited(string place) => Of(place, VisitedAspect);

        public static string Cleared(string place) => Of(place, ClearedAspect);

        public static string Accepted(string quest) => Of(quest, AcceptedAspect);

        public static string Happened(string happening) => Of(happening, HappenedAspect);

        static string Of(string subject, string aspect) => subject + Separator + aspect;

        // the subject half of a derived fact, or empty when this fact is none of the engine's
        public static string SubjectOf(string local, string aspect)
        {
            if (string.IsNullOrEmpty(local)) return "";

            string tail = Separator + aspect;

            return local.EndsWith(tail, StringComparison.Ordinal)
                ? local.Substring(0, local.Length - tail.Length)
                : "";
        }

        public static string Explain(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "a fact needs a name - 'bob.dead', 'chest.ash_yard.looted', " +
                       "'door.cellar.open'";

            string[] parts = name.Split(Separator);

            if (parts.Length > MostSegments)
                return $"'{name}' has {parts.Length} parts and a fact has at most " +
                       $"{MostSegments} - it is a thing that is true, not a sentence";

            string bad = parts.FirstOrDefault(p => !IsSegment(p));

            if (bad != null)
                return bad.Length == 0
                    ? $"'{name}' has an empty part - the dots separate words and cannot sit " +
                      "beside each other"
                    : $"'{bad}' is not part of a fact - lowercase a-z, 0-9 and underscore, " +
                      "separated by dots";

            return WellFormed;
        }

        public const string WellFormed = "well formed";
    }
}
