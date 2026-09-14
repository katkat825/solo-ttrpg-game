using System;
using System.Collections.Generic;
using Core.Localization;

namespace Content.Campaigns
{
    // `campaign.json`, PARSED (CONTENT_PIPELINE.md P4).
    //
    //     {
    //       "id": "ashfall",
    //       "format": 1,
    //       "engine": "0.4",
    //       "author": "the house",
    //       "tags": [ "undead", "short" ],
    //       "preview": "preview.png",
    //       "dependencies": [],
    //       "chapters": [ { "id": "the_yard", "encounters": [ "ash_yard" ] } ],
    //       "start": "the_yard"
    //     }
    //
    // THE TITLE IS NOT IN HERE, AND THAT IS THE POINT. `CONTENT_PIPELINE.md` asks this file for
    // "title, chapters, chapter order, starting state, shelf-box art" and a title in a JSON field
    // is a player-visible string in a data file, which `CONVENTIONS.md` forbids everywhere else in
    // the project for the reason that it cannot be translated. So the title is a KEY, and the key
    // is DERIVED FROM THE ID rather than listed - `campaign.ashfall.name`, the same rule
    // `EngineKeys` follows and the same rule the monsters already follow. A campaign's own
    // `locale/` carries it, `check-locale.ps1` demands it, and a Norwegian shelf reads Norwegian.
    //
    // WHAT IS NOT A KEY: the author, the tags and the preview image. An author's name is a proper
    // noun and translating it would be wrong; tags are a machine vocabulary a storefront filters
    // on, not prose; a preview image is a file name. Those are Workshop metadata and they stay
    // literal.
    //
    // `dependencies` WAS RESERVED AND IS NOW LIVE (MINIS_AND_ART.md A4). `CONTENT_PIPELINE.md`
    // asked for it "even if unused at launch - it is what lets a campaign depend on a standalone
    // mini pack or class pack, and reserving it now is free where retrofitting it later is not".
    // This is that use: a campaign that wants somebody's minis names their pack here, the loader
    // resolves it across every root, and a missing one is reported BY NAME rather than the
    // campaign half-loading with boxes where the figures should be.
    //
    // AND `campaign.json` IS NOW ALSO `pack.json`. `MODDING.md` section 2: "a pack is a folder,
    // and a campaign is a pack" - `pack.json` IS `campaign.json` with a `kind` field. Both names
    // are read, which is the "keys are stable identifiers, prefer adding and deprecating over
    // renaming" rule applied to a file name: every campaign already written keeps working, and a
    // folder that is not a campaign gets a file name that does not lie about it.
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

        // the namespace everything in the folder registers under (P0)
        public string Id { get; }

        // what sort of folder this is, and therefore which absences inside it are worth a sentence
        public PackKind Kind { get; }

        // A CAMPAIGN IS THE KIND THAT HAS SOMETHING TO PLAY, which is the only question anything
        // downstream actually asks - a mini pack has no chapters and is not missing any
        public bool IsPlayable => Kind == PackKind.Campaign || Kind == PackKind.Mixed;

        // which version of THIS schema the file was written against
        public int Format { get; }

        // and the oldest engine that can play it
        public Version Engine { get; }

        public string Author { get; }

        public IReadOnlyList<string> Tags { get; }

        // a file name inside the campaign folder. Workshop requires one; the shelf (Phase R) uses
        // the same image, so there is one picture and not two
        public string Preview { get; }

        // THE PACKS THIS ONE NEEDS, by their ids (A4). A campaign that stands its ghouls on
        // somebody's skeleton minis says so here; the loader resolves it across every root and
        // names the missing one rather than letting the campaign load with holes in it
        public IReadOnlyList<string> Dependencies { get; }

        // IN ORDER. A JSON array is ordered and a chapter list is an order, so "chapters" and
        // "chapter order" are one field rather than two that can disagree
        public IReadOnlyList<Chapter> Chapters { get; }

        // THE STARTING STATE, which at this stage of the game is exactly "which chapter you are in
        // when you press play". It grows the day a campaign has anything else to remember, and
        // P6's save file is where that lands
        public string Start { get; }

        // ---- the keys that fall out of it, derived and never listed ----

        public string NameKey => KeyConventions.Key(KeyConventions.CampaignNs, Id, "name");

        public string DescriptionKey => KeyConventions.Key(KeyConventions.CampaignNs, Id, "description");

        // `quest.ashfall.the_yard.title` - the shape `KeyConventions` gives as its own example
        public static string ChapterTitle(string campaign, string chapter) =>
            KeyConventions.Key(KeyConventions.QuestNs, campaign, chapter, "title");

        public IEnumerable<string> Keys()
        {
            yield return NameKey;
            yield return DescriptionKey;

            foreach (Chapter chapter in Chapters) yield return ChapterTitle(Id, chapter.Id);
        }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{Id} [{Kind.ToString().ToLowerInvariant()}] format {Format}, engine {Engine}" +
            (IsPlayable
                ? $", {Chapters.Count} chapters, starts at " + (Start.Length > 0 ? Start : "nothing")
                : "") +
            (Dependencies.Count > 0 ? $", needs {string.Join(", ", Dependencies)}" : "");
    }

    // ONE CHAPTER: a name and the encounters in it, in order. `CORE_RULES.md` section 8 puts one
    // Dread at the end of a chapter, which is the only rule that currently cares that chapters
    // exist - everything else about them is Phase R's shell and Phase W's story
    public sealed class Chapter
    {
        public Chapter(string id, IReadOnlyList<string> encounters)
        {
            Id = id;
            Encounters = encounters ?? Array.Empty<string>();
        }

        public string Id { get; }

        // by encounter id, in the order they are played
        public IReadOnlyList<string> Encounters { get; }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() => $"{Id}: {string.Join(", ", Encounters)}";
    }
}
