using System;
using System.Collections.Generic;
using Core.Localization;

namespace Content.Campaigns
{
    public sealed class Manifest
    {
        public Manifest(string id, PackKind kind, int format, Version engine, string author,
                        IReadOnlyList<string> tags, string preview,
                        IReadOnlyList<string> dependencies,
                        IReadOnlyList<Chapter> chapters, string start)
        {
            Id = id;
            Kind = kind;
            Format = format;
            Engine = engine;
            Author = author ?? "";
            Tags = tags ?? Array.Empty<string>();
            Preview = preview ?? "";
            Dependencies = dependencies ?? Array.Empty<string>();
            Chapters = chapters ?? Array.Empty<Chapter>();
            Start = start ?? "";
        }

        public string Id { get; }

        public PackKind Kind { get; }

        // campaign and mixed have chapters to play; a mini or class pack does not
        public bool IsPlayable => Kind == PackKind.Campaign || Kind == PackKind.Mixed;

        public int Format { get; }

        // oldest engine that can play it
        public Version Engine { get; }

        public string Author { get; }

        public IReadOnlyList<string> Tags { get; }

        public string Preview { get; }

        public IReadOnlyList<string> Dependencies { get; }

        public IReadOnlyList<Chapter> Chapters { get; }

        public string Start { get; }


        public string NameKey => KeyConventions.Key(KeyConventions.CampaignNs, Id, "name");

        public string DescriptionKey => KeyConventions.Key(KeyConventions.CampaignNs, Id, "description");

        public static string ChapterTitle(string campaign, string chapter) =>
            KeyConventions.Key(KeyConventions.QuestNs, campaign, chapter, "title");

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            foreach (Chapter chapter in Chapters) yield return ChapterTitle(Id, chapter.Id);
        }

        public override string ToString() =>
            $"{Id} [{Kind.ToString().ToLowerInvariant()}] format {Format}, engine {Engine}" +
            (IsPlayable
                ? $", {Chapters.Count} chapters, starts at " + (Start.Length > 0 ? Start : "nothing")
                : "") +
            (Dependencies.Count > 0 ? $", needs {string.Join(", ", Dependencies)}" : "");
    }

    public sealed class Chapter
    {
        public Chapter(string id, IReadOnlyList<string> places)
        {
            Id = id;
            Places = places ?? Array.Empty<string>();
        }

        public string Id { get; }

        // the places it is played in, in order. This was Encounters, and the rename is the whole
        // reframe in one word: a chapter is a sequence of somewheres, and a fight is one of the
        // things that can happen in one (PLACES_AND_PERSISTENCE.md section 1).
        public IReadOnlyList<string> Places { get; }

        public override string ToString() => $"{Id}: {string.Join(", ", Places)}";
    }
}
