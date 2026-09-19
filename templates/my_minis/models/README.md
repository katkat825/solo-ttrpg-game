# models/ — the figure and prop files themselves

Drop a `.glb` in here and point a mini at it with `"model": "models/<name>.glb"` (see
`../minis/example_mini.json`). The path is **inside this pack**: no `..`, no drive letter.

A mini that re-dresses a figure the game already ships uses `"variant"` instead, and needs
nothing in this folder — which is why the folder is empty and still worth having.

## A building, or any prop that is not a figure

A building is a mini like any other. It is not a separate kind of thing and there is no
`buildings/` folder: it is a `.glb` in here plus a manifest in `../minis/` that says how big
it is and how it sits on the board.

The only fields that matter differently for something that is not a figure:

```jsonc
{
  "id": "gate_house",
  "model": "models/gate_house.glb",
  "fit": "height",     // 'cell' makes it exactly one square wide; a building usually is not
  "height": 0.14,      // metres. A square is 0.06 and a figure is about 0.075
  "foot": 0.0          // metres to lift it, if the model's origin is not at its base
}
```

Leave `clips` and `foley` out — a building does not walk, strike, wobble or topple.

## What the loader will refuse

Everything `MINIS_AND_ART` asks for, and it refuses by name rather than crashing:

- **glTF only** (`.glb` / `.gltf`), with hard caps on size and triangle count.
- **No embedded scripts and no external references.** A model is data, never code; on a
  storefront that is a security boundary rather than a preference.
- **No path escaping the pack.** `"../../secrets.glb"` is refused, not resolved.

A model that fails any of these falls back to a placeholder box, the same way the board already
does where a tile model is missing — where there is no model the boxes are still here.

## Where the art comes from

Every model that ships goes through the three-step pipeline (`tools/pull-models.ps1`,
`tools/bake-palette.ps1`, and the `painted_miniature` shader). A model that skipped a step is a
model that will look like somebody else's game. Your own pack is yours and is not held to it —
but the fourteen colours in `tools/palette.ps1` are why the base game reads as one thing, and a
pack baked to them will sit on the table beside it rather than on top of it.
