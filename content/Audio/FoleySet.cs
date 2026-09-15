using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Content.Schema;

namespace Content.Audio
{
    public sealed class FoleySet
    {
        FoleySet(string folder, IReadOnlyList<WaveFacts> samples, IReadOnlyList<ContentProblem> problems)
        {
            Folder = folder ?? "";
            Samples = samples ?? Array.Empty<WaveFacts>();
            Problems = problems ?? Array.Empty<ContentProblem>();
        }

        public string Folder { get; }

        public IReadOnlyList<WaveFacts> Samples { get; }

        public IReadOnlyList<ContentProblem> Problems { get; }

        public int Count => Samples.Count;

        public bool IsEmpty => Samples.Count == 0;

        // named is the author's spelling of the folder; the absolute path is this machine's business
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
                    // re-named so the author sees their folder, not this machine's absolute path
                    foreach (ContentProblem problem in read.Problems)
                        problems.Add(new ContentProblem(name, problem.Where, problem.What, problem.Line));

                    continue;
                }

                samples.Add(read.Value);
            }

            return new FoleySet(folder, samples, problems);
        }

        public override string ToString() =>
            IsEmpty
                ? $"no samples in {Folder}" + (Problems.Count > 0 ? $", {Problems.Count} problems" : "")
                : $"{Samples.Count} samples in {Folder}" +
                  (Problems.Count > 0 ? $", {Problems.Count} problems" : "");
    }
}
