# Ideas — the wishlist

This is where I park ideas I don't want to lose but haven't thought all the way through. Some
of them are half a sentence. Some might be great; some might be terrible; most I haven't decided
about at all, and that's the point. It's a bucket, not a plan.

**What this is:** a persistent, low-fidelity place for "wouldn't it be cool if…" and for things
deliberately cut from the current scope so I could actually ship. Add freely. Keep entries small.
When an idea gets picked up for real, it graduates into a proper design doc and comes off this list.

**What this is not:** a roadmap, a promise, or a list of committed work. Nothing here is scheduled,
and plenty of it never will be. Anything that's actually being built, or is built-and-waiting on
something specific, lives elsewhere — not here.

The 1.0 rule of thumb these all sit behind: build the smallest thing that makes a real campaign
playable, and don't architect it in a way that makes the fun stuff below impossible later.

---

## A living world (beyond 1.0)

The world layer remembers what the campaign and the player made relevant — it doesn't simulate a
living city. These are the ways it could grow once it exists.

- **Crime and consequences.** Guards who remember a killing for a while; a bounty; being run out of
  a town. Not a full crime sim — just enough that violence in a settlement isn't free.
- **NPC relationships and reputation.** Someone mourns the person you killed. A merchant who's heard
  about you and won't deal. A faction that warms or cools. All author-driven, not emergent AI.
- **Places that stay changed.** The tavern you burned down stays burned. A bridge you dropped stays
  dropped. (The engine already needs to remember the important facts; this is spending them on scenery.)
- **Authored reactivity flourishes.** The "Bob dies → a grave appears in the cemetery" pattern,
  used liberally: changed dialogue, a door that's now barred, a shrine that reacts to what you did.
- **Richer Workshop-authored interactions.** Let campaign authors define more elaborate interactions
  than the built-in verb set, once the data model has proven itself.

## Player verbs (beyond talk / shop / attack)

Authors decide what the player may do; the engine handles the consequences. More verbs it could learn:

- **pickpocket, intimidate, persuade** — author-gated per NPC, same as attack is.
- **Improvised / environmental actions** — the "hit him with the chair" fantasy. Interactable
  scenery as improvised weapons or tools.
- **Emergent / free-text actions** — the big one, and probably far-future or never. The engine is
  built to execute a known set of verbs on purpose; open-ended "type anything" play is a different
  game. Noted here so the door isn't nailed shut, not because it's planned.

## Time, travel, and rest

- **Meaningful campaign time.** A real clock the campaign can care about. Unlocks most of the below.
- **Time-pressured quests.** "The kidnappers are getting away" — push on or rest becomes a real choice.
- **Rest as a decision** rather than a silent transition. Needs the clock above.
- **Weather and seasons** — as flavor, and occasionally as an obstacle (the pass is snowed in).
- **A real regional / overworld travel map** — nodes and roads and a moving marker, instead of
  narrated routes. Wait until a campaign actually wants one before building it.

## The table and the room

The whole point is that it feels like sitting at a table. Ways to lean into that:

- **A second empty chair** at the table. Evocative — or too much. Worth trying.
- **The room reflects real-world time of day** — lamplight and quiet at 2am. Cheap and lovely.
- **Ambient life** — the DM shifting, dice-box sounds, a mug on the table, small idle motion.
- **Seasonal / occasion dressing** for the room.

## Audio and voice

- **Optional human voiceover** as an additive layer over the written lines — post-launch, and the
  text stays the source of truth. (An AI-voiced version, if ever, would be a mod with its own
  disclosure, never the base game.)
- **Richer DM foley** — more of the wordless, behind-the-screen presence.

## Tooling and the Workshop

- **In-app map editor** — when a map is big enough to want a mouse instead of a text file.
- **Author-defined interactions/verbs** in the Workshop (pairs with the player-verbs section).
- **Downloadable content templates in the Workshop** — one-click starter templates for every content
  kind, so an author begins from a valid, commented skeleton. (Local authoring copies already live
  in `templates/`.)
- **A campaign linter surfaced to authors** — the same validation the game runs, plus warnings like
  "this quest can be made impossible," shown while authoring rather than discovered at play.

## Reach

- **Other platforms** — console or elsewhere, only if the game finds an audience. Ship on the one
  platform first.

---

## More (added 2026-09-17)

Parked from a design pass — deliberately future, unless they turn out trivial.

**Saving and permanence**

- **Ironbook mode** — a pure-permanence option: no reload, your choices stick for good. The default lets you reload earlier saves; this is the hardcore opt-in.
- **A cap on how many saves are kept** — undecided, waiting on a look at the performance cost of keeping many snapshots. Cheap to keep them, keep more; expensive, cap them.
- **Manual save** ships as first-class (it's the autosave on a button); this note is only the fallback, in case it turns out non-trivial.

**The room and the world**

- **Seasonal room dressing** — the room and the companion change with the season (a winter coat on the companion). Cheap in logic, but every bit of seasonal art is hand-made, so it waits.
- **Holiday decor** in the room — same story: nice, and a pile of bespoke art.
- **Weather-matched room** — matching real local weather needs a network call, which the game deliberately never makes (it plays fully offline), so this is maybe-someday at best and off by default if ever.
- **More behind-the-screen life** — beyond the random dice rattle: a pencil scratching, pages flipping, the top of a book rising above the screen, the screen tipping toward the DM. Cheap presence flourishes for a later pass.

**Player actions**

- **Swap a weapon by dropping it on your mini** — a future diegetic gesture; for now you pick your weapon on the character sheet.

---

*Add anything. Half-formed is fine — that's what this file is for.*
