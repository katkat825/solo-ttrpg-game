# Untitled tabletop RPG

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
dotnet test                     # rules test suite
dotnet run --project sim        # balance tables
dotnet run --project sim 50000  # more trials
```

To run the game, open `game/` in Godot 4.7 (.NET build) and press F5. It doesn't run from the CLI.

Two PowerShell checks run headless and exit non-zero on failure:

```
.\check-fairness.ps1 [-Dice 3] [-Shape N] [-Tray name]   # chi-squared, are the dice uniform
.\check-locale.ps1                                       # every key has text, every string has a key
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
sim/            headless balance harness
game/           the Godot project
  Dice/         DieBody, DieSolid, DieFaceTable, the die itself
  Tray/         DiceTray, TrayResolution, skins, the Snag cue
  audio/        DieAudio, SurfaceVoice, ImpactPool, the samples
  Diagnostics/  fairness sweep, locale audit
  Localization/ GodotLocalizer, the only place a key becomes text
  locale/       game.csv, one column per language
```

`SoloTabletopRpg.slnx` at the root covers core, core.tests and sim. Godot generates its own `.sln` inside `game/`, which is gitignored. `core` targets net8.0 because Godot 4.7 does; `sim` and `core.tests` are on net10.0.

See [`core/README.md`](core/README.md) for the layer order and the substitution seams.

## How it plays

Build a pool of up to three dice (attribute + skill + gear), throw it, add the best two, beat the difficulty. The die you didn't count becomes the Impact die and gets rolled for damage, so one throw covers both whether it worked and how well.

Damage steps your dice down a size. You watch a character get worse instead of watching a number drop. The hero gets two actions a round and ordinary enemies get one; without that a solo fight measures at about a 4% win rate.

Exactly one 1 in the pool is a **Snag**, which is cosmetic and just cues the companion to say something. It comes up on about 33% of early rolls. Two or more 1s is **Trouble** and actually costs you, at about 6%. Some 1 or other turns up 39% of the time, which is those two added — an easy pair of numbers to confuse, so `Core.Resolution.PoolOdds` gives the exact odds for any pool and a test holds the resolver to them.

**Vigor** is the hit-point-ish stat. Conditions are the other half of taking damage: each one shrinks an attribute die by a size, which compounds without needing a separate death-spiral rule.

## Rules I hold myself to

`core/` never references Godot. Headless tests and overnight balance runs both depend on it.

Rules in code, world in data. The dice system stays hard-coded. Monsters, items, maps, encounters and dialogue are content and live in data files. If adding a campaign would mean touching it, it's content.

Anything that's a policy decision goes behind an interface with a default implementation. `core.tests/SeamTests.cs` substitutes each seam from outside the library, so a test that stops compiling means I've welded one shut.

I've stopped there. `Actor`, `Pool`, `PoolResult` and the dice system are concrete on purpose.

No player-facing text in `core/`. The engine emits localization keys and the presentation layer resolves them. English is a locale file like any other language, so adding a language is a content task. Key grammar is `namespace.subject.aspect[.qualifier]*[.index]`, enforced by `KeyConventions.IsWellFormed` and checked end to end by `check-locale.ps1`.

Change a balance number, run the sim. Those figures were verified against exact enumeration, so drift means something broke.

When two implementations both work, I pick whichever feels more like sitting at a real table. That's settled most of the hard calls: hands instead of a UI, physics dice instead of a random number with an animation over it. Cosmetics never affect mechanics, or every roll turns into a question about your inventory. Text before voice, at least for now, because writing is cheap to revise and voice acting isn't.

## Where it is

The dice tray works. Three dice of mixed shapes thrown together, real collisions, face reading off the resting orientation, per-skin tray physics, force-driven audio, and the outcome readable off the felt without a UI panel. Fairness is swept per shape and per tray skin, since colliding dice are a different physical system from solo throws and I didn't want to assume.

Next is the Snag cue, then the grid and tiles, one mini moving, and the combat loop. After that comes the part I actually care about, which is writing a short campaign entirely in data with no new code. If that doesn't work then neither does the rest of the plan, so it's better to find out now than in year three.

## Still undecided

- Vigor plus Conditions, or Conditions only
- Gear die scaling: rarity, quality, or both
- Skill advancement: XP or use-based
- Whether the DM ever speaks aloud
- Whether a d20 exists at all as a collectible. It never joins the pool either way, since it would break the difficulty ladder

## Assets

Everything borrowed is CC0 and listed in [`THIRD_PARTY.md`](THIRD_PARTY.md) with its source, licence and download date. Raw downloads live in `assets/`, which is gitignored. Only the processed maps in `game/textures/` are committed.
