# Untitled tabletop RPG

Working name: Maps & Math Rocks

A single-player RPG that looks like an actual table. Grid map, a tray of physics dice, painted minis, and a DM behind a screen: you see his hands, you don't see his dice. One hero, no party.

Original rules. Original dice system. Inspired by the experience of tabletop RPGs, not any specific ruleset.

## Why this exists

For years I'd been looking for a single-player RPG that gave me the feeling of a tabletop campaign without micromanaging a party or relying on companion AI that I often found lacking.

While helping redesign the localization architecture for [Portalborn](https://store.steampowered.com/app/4815720/Portalborn/), I enjoyed the game enough to start writing my own campaign content for it. That was fun, and it clarified that what I actually wanted was something else.

Then the obvious caught up with me. I write software for a living. If the game I want doesn't exist, I can build it.

That localization work is also why this project handles text the way it does from the start. The engine emits keys and never player-facing strings, English is a locale file like every other language, and the key grammar is enforced by tests rather than by good intentions. Retrofitting internationalization into a codebase that grew without it is expensive, and I wasn't interested in doing it twice.

So this is my attempt at the feeling of sitting at a table with a DM, rolling real dice, and working through a handcrafted adventure without running a party of six.

## Building

```
dotnet test                     # rules test suite, plus game.tests
dotnet run --project sim        # balance tables
dotnet run --project sim 50000  # more trials
```

To run the game, open `game/` in Godot 4.7 (.NET build) and press F5. It doesn't run from the CLI. The main scene is `room.tscn` — the room, with the table inside it.

The PowerShell checks run headless and exit non-zero on failure. Each runs the real thing rather than a copy of it, so none of them can pass something the game then refuses. Build first; `dotnet test` does.

```
.\check-fairness.ps1 [-Dice 3] [-Shape N] [-Tray name]   # chi-squared, are the dice uniform
.\check-locale.ps1                                       # every key has text, every string has a key
.\check-maps.ps1                                         # every shipped and campaign map parses
.\check-campaign.ps1 [-ExpectSome]                       # every campaign folder validates
.\check-fight.ps1                                        # whole fights, played on the real table
.\check-dialogue.ps1 [-ExpectSome]                       # every conversation plays, in both locales
.\check-room.ps1                                         # the room boots, and has no menus in it
.\check-world.ps1                                        # a whole world walked: places, verbs, a fight, a save
.\check-voice.ps1                                        # the base game speaks to you, and never genders you
```

## Layout

```
core/           pure C# rules engine, never references Godot
  Dice/         die sizes, stepping, IRng
  Localization/ ILocalizer, key conventions
  Statistics/   FaceTally, chi-squared fairness testing
  Resolution/   Pool, PoolResult, IResolver, Difficulty
  Characters/   Traits, Actor, IArchetypeSource
  Combat/       CombatEngine, ITargetSelector, ICombatObserver
core.tests/     xUnit, one file per concern
content/        campaign content read off disk, also never references Godot
  Campaigns/    a folder becomes a campaign here: ContentId, Manifest, Package, Shelf
  Monsters/ Items/ Kits/ Classes/ Minis/ Models/ Audio/   what a pack can ship
  Dialogue/     the Yarn integration, bark banks, the shared spine, hint ladders, camp
  Companions/   the creature on the table, and which of five places it sits in
  Sheet/        the character sheet, and the cards that fill its blanks
  Saves/        SaveGame, its reader and writer, and the shelf of boxes they make
  Schema/       ContentProblem, Read<T>, ContentFormat, Vocabulary
content.tests/  xUnit over the readers
sim/            headless balance harness
campaigns/      the packs that ship; templates/ has a starting folder for each kind
game/           the Godot project
  Dice/         DieBody, DieSolid, DieFaceTable, the die itself
  Tray/         DiceTray, TrayResolution, skins, the Snag cue
  Board/ Fight/ the map, and the fight on it
  Dm/           the screen, the hands, the secret roll
  Companion/    the creature beside the map: idles, moods, bubbles, the hint cord
  Dialogue/ Camp/   a conversation at the table, and the fire it happens by
  Sheet/ Room/  the paper, and the room that has no menus in it
  Campaigns/ Saves/ the shelf at runtime, and a fight turned into a save
  audio/        DieAudio, SurfaceVoice, ImpactPool, the samples
  Diagnostics/  one scene per check: fairness, locale, maps, campaigns, fight, dialogue, room
  Localization/ GodotLocalizer, the only place a key becomes text
  locale/       game.csv, one column per language
  table.tscn    the table; room.tscn is the main scene and has the table inside it
game.tests/     xUnit over the Godot-free helpers in game/
```

Namespaces match folders throughout: `Core.*` in `core/`, `Game.*` in `game/`.

`SoloTabletopRpg.slnx` at the root covers core, content, their test projects, game, game.tests and sim, so `dotnet test` builds the Godot project too — that needs `Godot.NET.Sdk` from nuget.org and no Godot install. Godot generates its own `.sln` inside `game/`, which is gitignored. `core` and `game` target net8.0 because Godot 4.7 does; `sim` and the three test projects are on net10.0.

See [`core/README.md`](core/README.md) for the layer order and the substitution seams.

## How it plays

Build a pool of up to three dice (attribute + skill + gear), throw it, add the best two, beat the difficulty. The die you didn't count becomes the Impact die and gets rolled for damage, so one throw covers both whether it worked and how well.

Damage steps your dice down a size. You watch a character get worse instead of watching a number drop. The hero gets two actions a round and ordinary enemies get one; without that a solo fight measures at about a 4% win rate.

Exactly one 1 in the pool is a **Snag**, which is cosmetic and just cues the companion to say something. It comes up on about 33% of early rolls. Two or more 1s is **Trouble** and actually costs you, at about 6%. Some 1 or other turns up 39% of the time, which is those two added — an easy pair of numbers to confuse, so `Core.Resolution.PoolOdds` gives the exact odds for any pool and a test holds the resolver to them.

**Vigor** is the hit-point-ish stat. Conditions are the other half of taking damage: each one shrinks an attribute die by a size, which compounds without needing a separate death-spiral rule.

## Rules I hold myself to

`core/` never references Godot. Headless tests and overnight balance runs both depend on it.

Rules in code, world in data. The dice system stays hard-coded. Monsters, items, maps, places, the people standing in them, quests and dialogue are content and live in data files. If adding a campaign would mean touching it, it's content.

Anything that's a policy decision goes behind an interface with a default implementation. `core.tests/SeamTests.cs` substitutes each seam from outside the library, so a test that stops compiling means I've welded one shut.

I've stopped there. `Actor`, `Pool`, `PoolResult` and the dice system are concrete on purpose.

No player-facing text in `core/`. The engine emits localization keys and the presentation layer resolves them. English is a locale file like any other language, so adding a language is a content task. Key grammar is `namespace.subject.aspect[.qualifier]*[.index]`, enforced by `KeyConventions.IsWellFormed` and checked end to end by `check-locale.ps1`.

Change a balance number, run the sim. Those figures were verified against exact enumeration, so drift means something broke.

When two implementations both work, I pick whichever feels more like sitting at a real table. That's settled most of the hard calls: hands instead of a UI, physics dice instead of a random number with an animation over it. Cosmetics never affect mechanics, or every roll turns into a question about your inventory. Text before voice, at least for now, because writing is cheap to revise and voice acting isn't.

## Where it is

The whole spine is built and checked headless: the dice tray, the pure rules core, the grid board with edge-based walls, the combat loop, the campaign package format and its loader, minis and classes as content, the DM behind his screen, the companion on the table, the room-with-no-menus and the character sheet, and the World layer on top — places that own maps, a durable fact store, author-permitted interactions, quests as a view over facts, roads, and exploration as a second mode. Each of those ends in a check script that runs the real thing, not a copy of it.

The part I actually cared about worked: a campaign is a folder and nothing else. Three of them now ship — greyhollow, ashfall and saltmarch — built entirely in data, with zero new engine code between them. That was the test the whole plan rested on, and passing it is what makes the next decade of campaigns cheap.

The most recent work put the combat knowns on the table where a real DM keeps them: your own health as a number on your initiative card, a foe's as a single word — unharmed, wounded, badly wounded — and never a gauge, its Defence appearing on the card once you've earned it instead of being said aloud. Saving became event-based and reloadable, the door writes on the way out, and there's now a check that the game only ever speaks to *you* and never hands you a gender.

What's left is the table itself — the presentation of the World layer: the map as a swappable mat, discrete props you can upgrade, a camera that leans in and picks things up, the campaign as a book you open on a bookcase, and the note-with-checkboxes you answer the DM with — plus a first-class accessibility pass (screen reader, keyboard navigation, high contrast, resizable text, player-set speech speed), and then the first full campaign.

That "campaign is a folder, no new code" goal is also why I'm building this to sell: a commercial release on Steam with Steam Workshop support, so other people can build and share campaigns the same way I add them. The architecture doesn't change for that — the same engine/content boundary that lets me add campaigns for a decade is the one that lets players make their own — so the ambition grew and the plan didn't.

## Still undecided

- Vigor plus Conditions, or Conditions only. The build keeps both; the pure-Conditions version is still worth prototyping
- Gear die scaling: rarity, quality, or both
- Whether a d20 exists at all as a collectible. It never joins the pool either way, since it would break the difficulty ladder — only its existence outside the pool is open

Settled since this list was first written: the form factor (diegetic tabletop), skill advancement (authored into the campaign, not XP and not use-based), text before voice — the DM never speaks aloud — and the Impact die exploding on a max roll (it does; pacing, not power).

## Assets

Everything borrowed is CC0 and listed in [`THIRD_PARTY.md`](THIRD_PARTY.md) with its source, licence and download date. Raw downloads live in `assets/`, which is gitignored. Only the processed maps in `game/textures/` are committed.
