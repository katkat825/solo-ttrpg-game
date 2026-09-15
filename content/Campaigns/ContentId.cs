using System;
using System.Linq;
using Core.Localization;

namespace Content.Campaigns
{
    public static class ContentId
    {
        public const char Separator = '.';

        // held to one key segment so a bad folder name is refused at load, not in a later locale audit
        public static bool IsCampaign(string campaign) =>
            !string.IsNullOrEmpty(campaign) && IsSegment(campaign);

        public static bool IsLocal(string id) => !string.IsNullOrEmpty(id) && IsSegment(id);

        static bool IsSegment(string s) =>
            s.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_');

        public static string Scoped(string campaign, string id)
        {
            if (!IsCampaign(campaign))
                throw new ArgumentException($"'{campaign}' is not a campaign id.", nameof(campaign));

            if (!IsLocal(id))
                throw new ArgumentException($"'{id}' is not a content id.", nameof(id));

            return campaign + Separator + id;
        }

        public static bool IsScoped(string id) =>
            !string.IsNullOrEmpty(id) && id.IndexOf(Separator) > 0;

        // empty for the engine's own, unscoped ids
        public static string CampaignOf(string id)
        {
            if (!IsScoped(id)) return "";

            return id.Substring(0, id.IndexOf(Separator));
        }

        public static string LocalOf(string id)
        {
            if (!IsScoped(id)) return id ?? "";

            return id.Substring(id.IndexOf(Separator) + 1);
        }

        public static string ActorName(string id) => KeyConventions.ActorName(id);

        public static bool NamesSomethingKeyable(string id) =>
            !string.IsNullOrEmpty(id) && KeyConventions.IsWellFormed(KeyConventions.ActorName(id));
    }
}
