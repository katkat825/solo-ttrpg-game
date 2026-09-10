# minis/

Single recordings of a weighted figure being **set down** on the mat — the sound B1 asks for
(`THE_BOARD.md`), and one of `THE_TABLE.md` §7's first-order table sounds.

**Placeholder pool, in place.** Seven `.wav` samples are here now, so `MiniVoice` plays them
directly (it no longer borrows `impacts/wood/`; the borrowing path still exists as a fallback if
the folder is ever emptied). **The folder is the list** — drop a better recording in and it is
used with no code change, exactly as for impacts. The real replacement is a proper foley recording
of a mini on a wet-erase mat; these stand in until then.

## What these are, and how they were made

Sourced from `649210__johanvanvuren__salt-or-pepper-on-counter-or-table-impact.wav` (in `assets/`,
provenance in `THIRD_PARTY.md`) — a shaker set down on a table, which is the same gesture as a
mini being placed. The source holds seven events: **three clean set-downs** (an impact and a quick
decaying ring) and **four rejects** — three of them the shaker being *picked up* and scraped
across the table (a sustained ~50 ms friction tail, not a set-down), and one a quiet, noisy
multi-contact.

The pool is rebuilt from the **three clean set-downs only**. To fill seven slots without the
rejects and without bit-identical copies, the three are used at slight pitch variants (±6–10 %),
which also reads as figures of slightly different weight. Each sample is then maximised to ~−17 dB
RMS at a −1 dBFS ceiling, because the raw set-down is one sharp spike in a lot of quiet air
(~25 dB crest) and was near-inaudible in game until the body was brought up. See `MiniVoice.cs`.

The scrape/reject slices are **not** kept here — they would just be re-derivable noise. If you want
them back, they are events 4–6 (and the quiet event 3) of the source recording in `assets/`.

Naming follows the impacts rule — **named for what is struck, not for what strikes it**. These are
figures on a mat, so the eventual sibling folders are by surface, not by miniature.

Sourcing and slicing: same route as the impacts, `tools/slice_impacts.py`, provenance in
`THIRD_PARTY.md`.
