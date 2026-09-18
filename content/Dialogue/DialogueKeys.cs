using System;
using System.Collections.Generic;
using System.Linq;
using Core.Localization;

namespace Content.Dialogue
{
    // every key a voice at the table can emit, built rather than written out, so the grammar owns the shape
    public static class DialogueKeys
    {
        // a line of a conversation: dialogue.wolf.line.greyhollow.the_hinges_are_new
        public const string LineAspect = "line";

        // a beat of the shared spine: dialogue.wolf.beat.greyhollow.warn_bridge_trapped
        public const string BeatAspect = "beat";

        // the throw, read back in a voice: dialogue.wolf.readout.hit
        public const string ReadoutAspect = "readout";

        // Yarn's own line ids are prefixed; the segment after it is what the author wrote
        public const string YarnPrefix = "line:";


        public static string Line(string speaker, string campaign, string id) =>
            KeyConventions.Key(KeyConventions.DialogueNs, speaker, LineAspect, campaign, id);

        public static string Beat(string speaker, string campaign, string id) =>
            KeyConventions.Key(KeyConventions.DialogueNs, speaker, BeatAspect, campaign, id);

        public static string Readout(string speaker, Readout which) =>
            KeyConventions.Key(KeyConventions.DialogueNs, speaker, ReadoutAspect, which.Word());

        // the bark bank is the engine's own shape; this only names it beside the rest
        public static string Bark(string speaker, Bark situation, int index) =>
            KeyConventions.Bark(speaker, situation.Word(), index);

        // 1-based, because a bank of forty barks is numbered one to forty on paper
        public static IEnumerable<string> Barks(string speaker, Bark situation, int count)
        {
            for (int index = 1; index <= count; index++) yield return Bark(speaker, situation, index);
        }

        // every readout a speaker can be asked for; all of them format numbers in
        public static IEnumerable<string> ReadoutKeys(string speaker) =>
            Enum.GetValues<Readout>().Select(which => Readout(speaker, which));


        // the local id a Yarn line id carries, or null when it is not one of ours
        public static string LocalOf(string yarnId) =>
            yarnId != null && yarnId.StartsWith(YarnPrefix, StringComparison.Ordinal)
                ? yarnId.Substring(YarnPrefix.Length)
                : null;

        public static string YarnIdFor(string local) => YarnPrefix + local;
    }

    // What a voice can read back off the felt; closed, because each one takes its own numbers.
    //
    // A third member used to say a foe's Defence aloud the first time you beat it. Hearing the
    // same number every swing wore thin, so the known Defence is printed on the foe's initiative
    // card instead and the voice keeps the part that is atmosphere.
    public enum Readout
    {
        // "best two, eleven - through its guard, and it bites for three"
        Hit,

        Miss,
    }

    public static class Readouts
    {
        public static string Word(this Readout which) => which.ToString().ToLowerInvariant();

        public static int Numbers(this Readout which) => which switch
        {
            Readout.Hit => 3,
            _ => 2,
        };
    }
}
