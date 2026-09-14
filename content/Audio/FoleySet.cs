using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Audio
{
    // ONE FOLDER OF SAMPLES A PACK SUPPLIED, READ AND MEASURED (MINIS_AND_ART.md A3).
    //
    // THE FOLDER IS THE LIST, which is not a new rule - `Game.Audio.ImpactPool` has worked this
    // way since the tray, and its reason still holds: "a hard-coded list is a second description
    // of the folder, and when the two drift the failure is SILENCE, which looks exactly like a die
    // that never hit anything." A pack author adds a variation by copying a wav in, and nothing
    // anywhere names a file.
    //
    // SO WHAT A MANIFEST NAMES IS A FOLDER AND NEVER A SAMPLE, and this is what is behind that
    // name: every wav in it, each one held to `FoleyCaps`, with the bad ones named and the good
    // ones still playable. A set with one clipping sample in it is a set minus one sample, not a
    // silent mini - the same shape of isolation the rest of the phase uses.
    //
    // IT IS EMPTY-SAFE ON PURPOSE. A mini with no foley of its own uses the shared pool, which is
    // what every mini in the game did before this milestone and what most will keep doing; a
    // manifest naming a folder that is not there is a problem, but an empty `FoleySet` is not a
    // crash anywhere downstream.
    public sealed class FoleySet
    {
        FoleySet(string folder, IReadOnlyList<WaveFacts> samples, IReadOnlyList<ContentProblem> problems)
        {
            Folder = folder ?? "";
            Samples = samples ?? Array.Empty<WaveFacts>();
            Problems = problems ?? Array.Empty<ContentProblem>();
        }

        // the folder on disk this came out of
        public string Folder { get; }

        // every sample that passed, in file-name order
        public IReadOnlyList<WaveFacts> Samples { get; }

        public IReadOnlyList<ContentProblem> Problems { get; }

        public int Count => Samples.Count;

        public bool IsEmpty => Samples.Count == 0;

        // <paramref name="named"/> is how the manifest wrote the folder, which is what an author
        // gets told about - the absolute path is this machine's business and not theirs
        public static FoleySet Read(string folder, string named)
        {
            var problems = new List<ContentProblem>();
            var samples = new List<WaveFacts>();

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                problems.Add(new ContentProblem(
                    named ?? "", "",
                    "there is no such folder in this pack - a mini's foley is a FOLDER of wavs " +
                    "rather than one file, because the folder is the list"));

                return new FoleySet(folder, samples, problems);
            }

            string[] files;

            try
            {
                files = Directory
                    .EnumerateFiles(folder, "*" + FoleyCaps.Extension, SearchOption.TopDirectoryOnly)
                    .OrderBy(Path.GetFileName, StringComparer.Ordinal)
                    .ToArray();
            }
            catch (Exception could)
            {
                problems.Add(new ContentProblem(named ?? "", "", "could not be read - " + could.Message));
                return new FoleySet(folder, samples, problems);
            }

            // AN EMPTY FOLDER IS A PROBLEM AND A MISSING ONE IS TOO, and they get different
            // sentences because they are different mistakes: one is a path typed wrong and the
            // other is samples that did not get copied. Both end in a mini that lands in silence,
            // which `THE_TABLE.md` section 7 calls half a mini
            if (files.Length == 0)
                problems.Add(new ContentProblem(
                    named ?? "", "",
                    $"has no {FoleyCaps.Extension} files in it, so this mini would be set down in " +
                    "silence - put the samples in, or take the line out and use the shared pool"));

            if (files.Length > FoleyCaps.Samples)
                problems.Add(new ContentProblem(
                    named ?? "", "",
                    $"has {files.Length} samples in it and a set may have {FoleyCaps.Samples} - " +
                    "the folder is the list and variation is the point, but every one of them is " +
                    "memory a player is holding open"));

            foreach (string path in files)
            {
                string name = (named ?? "") + "/" + Path.GetFileName(path);

                Read<WaveFacts> read = WaveReader.Inspect(folder, Path.GetFileName(path));

                if (!read.Ok)
                {
                    // re-named so the author sees the folder they wrote and not this machine's
                    // absolute path
                    foreach (ContentProblem problem in read.Problems)
                        problems.Add(new ContentProblem(name, problem.Where, problem.What, problem.Line));

                    continue;
                }

                samples.Add(read.Value);
            }

            return new FoleySet(folder, samples, problems);
        }

        // DEVELOPER AND AUTHOR FACING, not localized
        public override string ToString() =>
            IsEmpty
                ? $"no samples in {Folder}" + (Problems.Count > 0 ? $", {Problems.Count} problems" : "")
                : $"{Samples.Count} samples in {Folder}" +
                  (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
    }
}
