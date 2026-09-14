using System;
using System.Linq;
using Core.Localization;

namespace Content.Campaigns
{
    // EVERY CAMPAIGN IS NAMESPACED BY ITS OWN UNIQUE ID, AND THIS IS THAT RULE.
    //
    // `CONVENTIONS.md` section 7 and `SEAMS.md` section 11: two players will independently make a
    // "ghoul", and without this their subscribed campaigns collide - in the key table AND in
    // `IArchetypeSource`'s id space - and one silently overwrites the other. So a campaign's
    // content ids carry the campaign id as their first segment: `ashfall.ghoul`, never `ghoul`.
    //
    // ONE STRING, NOT TWO. The scoped id IS the id, which means `Actor.Id` is already unique,
    // `IArchetypeSource` needs no second key, and `KeyConventions.ActorName` composes the right
    // key out of it for free - `actor.ashfall.ghoul.name`, which is exactly the example
    // CONVENTIONS section 7 gives. The alternative, an id plus a campaign field carried alongside
    // it, is two things that have to be kept together by everybody who touches either.
    //
    // THE ENGINE'S OWN VOCABULARY KEEPS ITS UN-PREFIXED FORM - `rabble`, `rival`, the companion's
    // `dialogue.wolf.*` - because it ships in the base game and belongs to no campaign. So an
    // unscoped id is not an error here; it is what "this came with the game" looks like.
    //
    // DECIDED NOW, AGAINST ONE JSON SOURCE, BECAUSE IT IS FREE NOW. Once campaigns exist in the
    // wild it is a data migration, which is the whole argument `SEAMS.md` section 11 makes for
    // settling it before anything is published.
    public static class ContentId
    {
        public const char Separator = '.';

        // A CAMPAIGN ID IS ONE KEY SEGMENT, because it becomes one: it is the segment right after
        // the namespace in every key the campaign emits. Holding it to the same grammar here means
        // a folder called "My Campaign!" is refused at load with a sentence, rather than producing
        // keys that `check-locale.ps1` will later call malformed with no idea where they came from
        public static bool IsCampaign(string campaign) =>
            !string.IsNullOrEmpty(campaign) && IsSegment(campaign);

        // and a local id is one segment too - `ghoul`, not `ghoul.big`
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

        // which campaign it belongs to, or empty for the engine's own
        public static string CampaignOf(string id)
        {
            if (!IsScoped(id)) return "";

            return id.Substring(0, id.IndexOf(Separator));
        }

        // and what it is called inside that campaign
        public static string LocalOf(string id)
        {
            if (!IsScoped(id)) return id ?? "";

            return id.Substring(id.IndexOf(Separator) + 1);
        }

        // THE KEY THAT FALLS OUT OF IT, and the reason one string was enough. Held here so the
        // grammar check below has something to be about
        public static string ActorName(string id) => KeyConventions.ActorName(id);

        // a scoped id has to survive the grammar, because the keys made from it have to. This is
        // the check the loader runs so a bad id is refused where it was written rather than three
        // layers away in a locale audit
        public static bool NamesSomethingKeyable(string id) =>
            !string.IsNullOrEmpty(id) && KeyConventions.IsWellFormed(KeyConventions.ActorName(id));
    }
}
