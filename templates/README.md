# Content templates

Copy-ready, commented skeletons for hand-authoring content. Each folder here is a whole
**pack** — a campaign, a class pack, or a mini pack — you duplicate and fill in.

These live in `templates/` **on purpose**: the game loads *every* folder under `campaigns/`,
so a half-finished draft there would show up as a broken campaign in `check-campaign.ps1`.
Draft here; move to `campaigns/` when it's ready to play.

| Template | What it is | Copy it to |
|---|---|---|
| `my_campaign/` | a playable campaign (chapters, places, maps, monsters, items, people, quests, roads) | `campaigns/<your-id>/` |
| `my_classes/`  | a class pack (playable hero classes + their abilities) | `campaigns/<your-id>/` |
| `my_minis/`    | a mini pack (figures other packs can stand on the board) | `campaigns/<your-id>/` |

## How to start a new campaign

1. **Copy** `templates/my_campaign` to `campaigns/my_campaign`.
2. **Rename** the folder to your campaign's id (lowercase `a-z 0-9 _`, no dots) — say `campaigns/ashfall`.
3. In `campaign.json`, set `"id"` to that same name. **The id and the folder name must match.**
4. Rename `locale/my_campaign.csv` to `locale/<your-id>.csv`, and in it replace the
   `my_campaign` segment of every key with your id (find-and-replace `my_campaign.` → `ashfall.`).
5. Fill in the rest and **validate** (see below). Everything is marked `[REQUIRED]` / `[OPTIONAL]`.

> Campaign JSON files may keep `//` comments and trailing commas — the game strips them, so you can
> leave the guidance in place while you work. **The `.csv` locale files are plain CSV: no comments.**

## A campaign folder

```
campaigns/<id>/
  campaign.json          the manifest (id, engine, chapters). REQUIRED.
  locale/<id>.csv        every player-facing string, by key. REQUIRED.
  places/*.json          one per place (a map + who is standing in it + the ways out).
  maps/*.map             one per place (ASCII grid). Named by a place's "map".
  monsters/*.json        one per foe (statblock). Named by a standing's "monster".
  entities/*.json        one per person or thing you can deal with, and what you may do (optional).
  quests/*.json          one per quest - a named view over facts (optional).
  roads/*.json           one per road between two places, and what can happen on it (optional).
  items/*.json           one per weapon/armor (optional).
  dialogue/*.yarn        conversations, and camp scenes (optional).
  beats/*.json           the shared spine: intents every companion must deliver (optional).
  hints/*.json           three-rung hint ladders, built out of beats (optional).
```

A **class pack** may also ship the voices and the blanks a sheet is filled from:

```
campaigns/<id>/
  companions/*.json      the creature a class comes with, and where on the table it sits.
  barks/*.json           one voice's short reactions - COUNTS, not lines.
  sheet/races/*.json     what the race blank on the character sheet offers.
  sheet/backgrounds/*.json   and the background blank.
```

A **mini pack** also has a `models/` folder for the `.glb` files themselves — a figure, a prop or a
building is a model in there plus a manifest in `minis/` (see `my_minis/models/README.md`). A mini
that only re-dresses a figure the game already ships needs nothing in it.

A **pack** (`my_classes`, `my_minis`) uses `pack.json` instead of `campaign.json`, sets a `kind`,
and has **no** chapters. A campaign can depend on a pack via `dependencies`.

## Ids, and where names live

- **Ids** are lowercase `a-z`, `0-9`, `_`. No dots, no spaces.
- Inside a pack you name your own things **bare** (`ghoul`), never `mycampaign.ghoul` — the pack
  name is added for you. Reaching *another* pack's thing uses the dotted form `<pack>.<thing>`
  (and that pack goes in `dependencies`).
- **Gear/item ids are one global namespace** shared with the engine (so your `axe` and the base
  game's `axe` are the same key) — every other id is scoped to your pack.
- **A VOICE is the other unscoped thing.** `dialogue.wolf.*` belongs to no pack, so a campaign can
  write lines and barks for a companion somebody else ships, and they go in the same bank. What your
  campaign cannot do is speak as a creature nothing installed has ever heard of — ship a companion
  or a bark bank for it, or name the pack that does in `dependencies`.

## Writing dialogue

Conversations are Yarn Spinner `.yarn` files and the game compiles them when it loads your
campaign, so there is **no tool to run and no build step** — write in a text editor and press play.

Two rules, both enforced at load, and both the same rule really:

1. **Every line needs an explicit `#line:` tag.** Yarn will invent one from a hash of the text
   otherwise, and then editing the words orphans every translation of that line. The tag becomes
   the last segment of the key.
2. **The text in the `.yarn` is your working copy and is never shown.** The words a player reads
   come out of `locale/<your-id>.csv`. Press **L** in the dice tray: every line should mangle. One
   that does not is being read out of your dialogue folder, which is English hardcoded in a data
   file.

`.\check-dialogue.ps1` plays every conversation you ship, down every branch, in both locales, and
tells you which line is missing which word.
- **No display text lives in the JSON.** Names and descriptions are *keys*, resolved from the
  locale CSV, so the game can be translated. Putting a `"name"` in a JSON file is an error with a
  message telling you the key to use instead.

### Locale keys a campaign emits

| Key | For |
|---|---|
| `campaign.<id>.name` / `.description` | the storefront card |
| `quest.<id>.<chapter>.title` | a chapter title |
| `actor.<id>.<monster>.name` / `.name_numbered` | a monster (numbered handles two of the same, `"Ghoul {0}"`) |
| `gear.<gearid>.name` | a weapon or armor (gear ids are global — no pack segment) |
| `dialogue.dm.narration.<id>.<cue>` | a cue's line (only cues whose gesture carries words) |
| `quest.<id>.<place>.name` | a place's name |
| `quest.<id>.<road>.name` | a road's name |
| `quest.<id>.<quest>.title` / `.description` | a quest |
| `actor.<id>.<entity>.name` | a person or thing standing in a place |
| `dialogue.dm.narration.<id>.<line>` | an entity's `examine` line, and a road event's line |

A campaign's **chapters, places, quests and roads all name themselves under `quest.<id>.*`**, and
its **monsters and entities both under `actor.<id>.*`** — so two of them sharing an id is refused
at load, because one of the two names would be written, translated and never seen.

Class packs also use `class.<pack>.<class>.name` / `.description` and
`ability.<pack>.<ability>.name` / `.description`; mini packs use `mini.<pack>.<mini>.name`.

## Facts, and what they are for

A **fact** is one thing that is true and stays true: `bob.dead`, `chest.looted`, `door.open`. It is
the only thing the game remembers about your world. Everything else — where the minis were
standing, the body on the floor, the dice on the felt — is rebuilt from the place and thrown away,
which is why a save is small and why a corpse does not follow the player around for six months.

**A place, entered, is base map + facts applied.** Bob stands where your place puts him *unless*
`bob.dead`. The grave is not there *until* it. You write that with `when` / `unless` on a standing,
on an exit, or in a quest clause.

**The engine writes its own facts and derives their names**, so you never spell one it set:

| Fact | Written when |
|---|---|
| `<entity>.dead` | it was attacked and lost |
| `<entity>.spoken` | you talked to it |
| `<entity>.looted` | you searched it |
| `<entity>.open` | you opened it |
| `<place>.visited` | you walked in, the first time |
| `<place>.cleared` | a fight there ended with the foes down |
| `<quest>.accepted` | the quest was accepted |
| `<event>.happened` | a `"once": true` road event fired |

Anything else is yours, written by a trigger (`"then": "set"`) or a road event (`"sets"`). A fact
name is lowercase `a-z 0-9 _` in up to four dotted parts.

**If nothing in your campaign can ever make a fact true, saying so is a load error** — a standing
or a quest waiting on a fact nobody writes waits forever, and that is the mistake that compiles
perfectly and is invisible at the table.

## Valid values (the closed vocabularies)

| Field | Allowed |
|---|---|
| dice | `d4` `d6` `d8` `d10` `d12` |
| attributes | `might` `grace` `wits` `heart` |
| skills | `blades` `marksman` `brawl` `stealth` `larceny` `lore` `survival` `insight` `sway` `channeling` |
| conditions | `winded` (might) · `reeling` (grace) · `rattled` (wits) · `shaken` (heart) |
| monster tier | `rabble` · `rival` · `dread` |
| behaviour | `rabble_first` · `strongest_first` |
| trigger `when` | `entered` · `cleared` · `fact` (with `"fact"`) |
| trigger `then` | `next` · `ends` · `goto` (with `"place"`) · `set` / `clear` (with `"sets"`) |
| cue `when` | `entered` · `cleared` · `fact` (with `"fact"`) |
| entity `can` | `talk` · `attack` · `examine` · `open` · `search` |
| cue gesture | `place` `slide` `push` `tap` `reachbehind` `rest` `withdraw` `tack` `turnpage` `write` `idle` |
| pack `kind` | `campaign` · `minis` · `classes` · `mixed` |
| ability `primitive` | `check` · `channel` |
| check effects (`pass`/`fail`) | `open` `rough` `recoil` `condition` `shove` `steady` |
| channel effects (`onHit`/`onMiss`) | `damage` `condition` `rough` `shove` |
| ability `cost` | `none` · `strain` · `nerve` |
| ability `target` | `reach` · `sight` · `self` |
| mini `fit` | `cell` · `height` |
| mini `clips`/`foley` motions | `placed` `move` `strike` `wobble` `topple` |

## The map format

Maps are ASCII, at "double resolution": a **square** sits on an odd row and odd column, and the
**line** between two squares sits on the positions in between. Corners are `+`. Comment lines
start with `;`. Width and height (in characters) are both **odd**, minimum 3. Exactly one `@`.

```
squares:  .  floor    ~  difficult ground    #  rock (blocks)    @  hero start (exactly one)
          1-9  a numbered spawn slot (a place's standings say who or what is on each,
               and its exits say which one is the way out)
lines:    (space) open    |  wall up a line    -  wall along a line    x  shut door    +  corner
```

A wall is a feature of the **line** between two squares, not a square of its own; the squares on
both sides of a wall are ordinary floor. See `my_campaign/maps/example_room.map`.

## Validate what you wrote

Build first (`dotnet build game`, or `dotnet test`), then from the repo root:

```
.\check-campaign.ps1     # every campaign/pack folder: schema + cross-references, each problem named
.\check-locale.ps1       # every key has text, every string has a key
.\check-maps.ps1         # every shipped map parses
.\check-world.ps1        # walk the whole world: places, verbs, a fight in and out, roads, a save
```

`check-campaign.ps1` runs the same loader the game uses, so it can't pass something the game then
refuses — and on a good pack it prints back what it read (ids, chapters, who stands on which slot),
which is the quickest way to see the game agree with you.

The example content in these templates is written to load clean as-is, so you can copy a folder,
run the checks, and start editing against a green baseline.
