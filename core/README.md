# core/

Pure C#, and it never references Godot.

## Layers

Dependencies point one way. Nothing lower reaches up.

```
Core.Dice   Core.Localization   Core.Statistics
        ↑
Core.Resolution    pools, results, the resolver seam, the difficulty ladder
        ↑
Core.Characters    traits, actors, conditions, the content seam
        ↑
Core.Combat        the fight loop, targeting, the observer seam
```

The three at the bottom don't know about each other either.

Namespaces match folders, so if this ever needs to become several assemblies it's a project-file change rather than a rename. `Localization` and `Statistics` aren't game rules, and they belong here anyway: the membership test is *pure, headless and Godot-free*, not *is it literally a rule*. That's why this folder is `core/` and not `rules/`.

## Seams

Six places something can be swapped without editing anything in here.

| Seam | Interface | Ships with |
|---|---|---|
| Randomness | `IRng` | `SeededRng`, `ScriptedRng`, `RecordingRng` |
| Resolution | `IResolver` | `StandardResolver`, `LoggingResolver` |
| Targeting | `ITargetSelector` | `RabbleFirstSelector`, `StrongestFirstSelector` |
| Watching | `ICombatObserver` | `NullCombatObserver`, `RecordingCombatObserver`, `CompositeCombatObserver` |
| Content | `IArchetypeSource` | `BuiltInArchetypes` |
| Text | `ILocalizer` | `KeyEchoLocalizer`, `DictionaryLocalizer` |

`ICombatObserver` is the one that matters most. It's the boundary between rules and presentation: Godot implements it and animates whatever it's told, which is how the engine can run a fight without any reference to the view layer. The same interface covers debugging and replay capture.

Everything's constructor-injected; there are no statics or globals to reach for.

## Localization

Nothing in here returns a string a player will read. `Actor.NameKey` is `"actor.barbarian.name"`, `PoolDie.LabelKey` is `"attr.might.name"`, and the presentation layer resolves them. English lives in a locale file like every other language.

Grammar is `namespace.subject.aspect[.qualifier]*[.index]`: lowercase, snake_case inside a segment, singular namespaces, at least three segments, index last and three digits if present, and the namespace set is closed. `KeyConventions.IsWellFormed` enforces it and the tests run every key the engine can emit through it. Build keys with the `KeyConventions` helpers and they come out correct.

One key per whole sentence, never assembled from fragments, because word order differs by language. Numbers go in as `{0}`.

`DebugName` and the `ToString()` overrides are developer-only and deliberately unlocalized. They must never reach the screen, which is what the comments on them say.

## Debugging

Everything is injected, so wrapping a layer to watch it is one line.

```csharp
var rng      = new RecordingRng(new SeededRng(4242));
var resolver = new LoggingResolver(new StandardResolver(rng), Console.WriteLine);
var engine   = new CombatEngine(resolver, observer: new RecordingCombatObserver(Console.WriteLine));
```

Seeded runs are reproducible and `SeamTests.SeededRun_IsReproducible` asserts it. When a fight gives a result I don't believe, I re-run it with the same seed and a recorder attached instead of adding print statements.

## Deliberately concrete

`Actor` is a class, not an interface. Everything in the game is an actor and there's no second implementation waiting to happen. `Pool` and `PoolResult` are data. `Difficulty` is a static class of `const int` thresholds; a campaign needing different numbers passes them in rather than subclassing.

The dice system stays hard-coded. Letting a campaign redefine how the Impact die works would cost a lot of complexity for flexibility nobody will use, and it would stop the rules being simulatable, which is how every balance number in the project got checked.

## Known violation

`Characters/BuiltInArchetypes.cs` holds statblocks, which is content living in code. It's a placeholder so the sim and tests have something to run against until the content pipeline exists. `IArchetypeSource` is already in place, so replacing it shouldn't touch any consumer.
