using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Dialogue
{
    // The shared spine (W5, CORE_RULES.md section 12). A beat is an INTENT, written once - "warn
    // the player the bridge is trapped" - and every companion supplies its own phrasing of it.
    //
    // This is the whole of what makes five companions affordable. Without it the writer has five
    // parallel scripts and no way to know which of them quietly dropped the warning; with it there
    // is one list of intents and a check that says which voice is missing which.
    public sealed class Beat
    {
        public Beat(string id, BeatKind kind, string note = null)
        {
            Id = id;
            Kind = kind;
            Note = note ?? "";
        }

        // local to the campaign; the campaign id goes into the key
        public string Id { get; }

        public BeatKind Kind { get; }

        // for the writer, never for a player. Not localized and it must never reach the screen -
        // it is the brief ("tell them the bridge is trapped"), not the line.
        public string Note { get; }

        // a beat the player may need is one no companion is allowed to be missing
        public bool MustBeDelivered => Kind != BeatKind.Colour;

        public string KeyFor(string speaker, string campaign) =>
            DialogueKeys.Beat(speaker, campaign, Id);

        // THE FALLBACK, AND THE REASON THE SPINE SURVIVES CONTACT WITH A STOREFRONT.
        //
        // W5's rule is that no companion may WITHHOLD a beat the player needs. The obvious reading
        // - every voice must phrase every beat - cannot hold on Workshop: a campaign published in
        // March cannot write lines for a class pack published in June, and the moment somebody
        // installs one, every campaign on the shelf is suddenly missing phrasings.
        //
        // So the DM is the floor. The DM is always at the table, is never a companion, and already
        // narrates. A beat with no phrasing in the voice of the companion you actually brought is
        // delivered by the DM instead, which means the INFORMATION is never withheld - which is
        // what the rule is about. What a campaign earns by writing the wolf's version is that the
        // wolf says it, which is the flavour and is worth writing and is not load-bearing.
        public const string TheDm = "dm";

        public string DmKey(string campaign) => DialogueKeys.Beat(TheDm, campaign, Id);

        public override string ToString() =>
            $"{Id} [{Kind.ToString().ToLowerInvariant()}]" + (Note.Length > 0 ? $" - {Note}" : "");
    }

    public enum BeatKind
    {
        // the player needs this. Every companion delivers it; none may withhold it
        Plot,

        // a rung of a hint ladder. Also required of everyone - a hint that only one voice can give
        // is a hint gated on which class you picked
        Hint,

        // texture. The Goose Incident lives here, and a voice with no opinion about it is fine
        Colour,
    }

    public static class BeatKinds
    {
        public static string Word(this BeatKind kind) => kind.ToString().ToLowerInvariant();

        public static IReadOnlyList<string> Words => Enum.GetValues<BeatKind>().Select(Word).ToArray();

        public static bool TryWord(string word, out BeatKind kind)
        {
            kind = default;

            if (string.IsNullOrWhiteSpace(word)) return false;

            string trimmed = word.Trim();

            if (trimmed.Any(char.IsDigit)) return false;

            return Enum.TryParse(trimmed, ignoreCase: true, out kind)
                && Enum.IsDefined(typeof(BeatKind), kind);
        }
    }
}
