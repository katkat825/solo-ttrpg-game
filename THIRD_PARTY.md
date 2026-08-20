# Third-party assets

Where each asset came from and what licence it's under.

The raw downloads aren't in git. `assets/` is ignored because 1.2 GB of compressed archives would sit in history forever and git can't delta-compress them. If those downloads are ever lost, this file is how they get replaced, so it has to stay current.

Standing rule: check the licence on the specific asset page at download time and record it here. "The site is CC0" isn't good enough, because sites host guest submissions and terms change.

## Audio

Freesound licences are per clip, not per site. ambientCG, Poly Haven and ShareTextures publish everything under CC0; Freesound doesn't. Each upload carries whatever its author chose, usually one of:

| Licence | Means |
|---|---|
| CC0 | Free to use, modify, ship commercially. No attribution needed. |
| CC-BY | Usable, but the author must be credited. |
| CC-BY-NC | Non-commercial only. Unusable if this ever sells. |

A clip whose licence I can't prove has to come out later, after it's already all over the mix.

| Clip | Source | Licence | Use |
|---|---|---|---|
| `558204__jakubpjp__gamemisc_dice-roll-on-wood_jaku5.wav` | [freesound.org/s/558204](https://freesound.org/s/558204/) by jakubp.jp, 2021 | CC0, verified on the page 2026-08-04 | 5 impacts. Single d10 on wood, Sony PCM M10 internal mics, 96 kHz 24-bit stereo, 1.34 s. |
| `485946__aunrea__10-sided-die-rolled-on-wood-table.wav` | [freesound.org/s/485946](https://freesound.org/people/aunrea/sounds/485946/) by aunrea, 2019 | CC0, verified on the page 2026-08-04 | 25 impacts. Tascam DR-100MKIII with an AT875R shotgun, 24/48 mono. |
| `545489__wardoctor17__d20-rolls.wav` | [freesound.org/s/545489](https://freesound.org/people/wardoctor17/sounds/545489/) by wardoctor17, 2020 | CC0, verified on the page 2026-08-04 | 25 impacts. d20 on a wooden table. |
| `441841__seanmporio__d20-rolling.wav` | [freesound.org/s/441841](https://freesound.org/people/SeanMPorio/sounds/441841/) by SeanMPorio, 2020 | CC0, verified on the page 2026-08-04 | 22 impacts, 3 quarantined. Single d20 on wood. The uploader notes the die hits an Altoids can on the fifth roll. |

### Slicing

Every dice recording you can download is a whole roll, not an impact: one die tumbling for a second or more, containing five to sixty bounces. Played as one-shots they drift: the recording's bounces land at fixed times while the physics dice land whenever they land, and the two come apart within about half a second. What's needed is one short sample per actual collision, with volume and pitch driven by impact force.

`tools/slice_impacts.py` does that. Point it at `assets/`, it finds the transients and writes a bag of impacts for `AudioStreamRandomizer`:

```
python tools/slice_impacts.py                    # every wav in assets/
python tools/slice_impacts.py assets/foo.wav     # just one
python tools/slice_impacts.py --out game/audio/dice_stone
```

Current pool is 77 samples, 1.1 MB, in `game/audio/samples/impacts/wood/`. Filenames keep the Freesound id as a prefix (`545489_07.wav`) so provenance survives in the file itself.

Each sample comes out mono, because `AudioStreamPlayer3D` can't spatialise stereo and will silently do nothing if you feed it any. 48 kHz 16-bit, since Godot resamples anyway. Trimmed to the transient with 3 ms pre-roll, 1 ms fade-in and 10 ms fade-out; the fade-in is there because a cut landing mid-waveform clicks. Normalised to −3 dBFS so loudness comes from impact force at runtime. Onsets are rejected unless preceded by real quiet, otherwise you get samples starting halfway through the previous bounce.

It parses RIFF directly rather than using Python's `wave` module, which refuses 32-bit float. `545489` is exactly that, and a fair share of Freesound uploads are.

### The Altoids can

`441841`'s description warns about the metallic clang on the fifth roll, so I measured the samples for spectral tonality and decay length rather than trusting my ears. All eight of the most metallic samples came from that clip. Three were 3–4 standard deviations out, with `441841_18` ringing for 284 ms against a pool average of 77 ms. Those three are excluded from the pool — 22 of 25 from that clip ship. They are not kept on disk: `slice_impacts.py` is deterministic, so re-running it against the source clip in `assets/` reproduces all 25 and you can listen to the rejects then. The other five had long tails but normal tonality, which is just a die rolling to a stop, so they stayed.

The measurement is only a proxy, so I should sit down and listen to those three properly at some point.

## Textures

Downloaded 2026-08-04. All three sources publish under CC0 1.0: commercial use fine, modification fine, no attribution required. Everything from ShareTextures was checked individually.

### ambientCG — https://ambientcg.com

2K JPG sets (albedo, normal, roughness, displacement, AO).

| File | Use |
|---|---|
| `Fabric081A`, blue fine weave | Tray lining, tablecloth |
| `Leather033A`, mild wear dark | DM screen, rulebook cover, tray rim |
| `Marble012`, white marble | Die material |
| `Marble016`, black marble | Die material |
| `Metal048A`, shiny gold | Die material |
| `Metal057B`, copper | Die material |
| `Onyx011`, blue/tan swirls | Die material |
| `Onyx013`, dark swirly | Die material, obsidian |
| `Onyx015`, white faint swirls | Die material |
| `Wood067`, 1K | Tray frame. In the project at `game/textures/tray_wood/` |
| `Fabric034`, 1K felt | Tray floor. `game/textures/tray_felt/`, tinted green and teal |
| `Onyx011`, 1K | Dice. `game/textures/die_onyx/`, triplanar |

### Poly Haven — https://polyhaven.com

4K `.blend` scenes, much larger than needed. Source material only.

| File | Use |
|---|---|
| `crepe_satin` | Tablecloth |
| `quatrefoil_jacquard_fabric` | Tablecloth, richer |
| `terlenka` | Fabric |
| `dark_wood` | Table, tray walls |
| `rosewood_veneer1` | Table, the good-table look |
| `wooden_panels` | Room walls |
| `wood_table`, `wood_table_001`, `wood_table_worn` | The table |
| `leather_red_03` | DM screen, chair |
| `rock_01` | Die material, stone |

### ShareTextures — https://sharetextures.com

1K JPG sets. One item so far.

| File | Use |
|---|---|
| `amethyst_texture_1` | Dice. Parked, see below |

### Coverage

The collectible die materials are nearly all there: wood (`dark_wood`, `rosewood_veneer1`), marble (`Marble012`, `Marble016`), obsidian (`Onyx013`), brass (`Metal048A`), copper (`Metal057B`), stone (`rock_01`), gemstone (`Onyx011`, `Onyx015`). Resin is the one real gap. Steel would be `Metal048A` recoloured. Felt, wood and leather for table dressing are covered several times over.

## Resolution

A die is 50 mm on a 640 mm tray. If the tray fills about 1200 px of a 1080p screen, a die occupies roughly 90 px. A 4K texture on that is around 45× more texels than pixels, costing VRAM, load time and repo space for detail nobody can see.

| Surface | On screen | Texture |
|---|---|---|
| Dice | ~90 px | 512 is generous |
| Tray, felt | ~1200 px | 1K |
| Table top | fills the frame | 2K |
| Room walls | soft focus anyway | 1K |

The ambientCG 2K sets only need downscaling for dice. The Poly Haven 4K `.blend` files are source material: extract the maps, downscale, discard the rest. A 200 MB `.blend` has no business near `game/`.

## Workflow

```
assets/            raw downloads, gitignored
game/textures/     processed maps at shipping resolution, committed
THIRD_PARTY.md     this file
```

Download to `assets/`, take only the maps you need (albedo, normal, roughness, sometimes AO), downscale, save into `game/textures/<material>/`, commit those, add a row here. Godot re-compresses on import, so what's committed is the source of truth for the look rather than the final bytes.

Every future asset gets its row at download time rather than later, for the reason above.

## Notes from doing this

### Unpacking an ambientCG zip

`Wood067` was the worked example: 5.7 MB in, 3.3 MB out. Keep `_Color`, `_NormalGL`, `_Roughness`, and `_Displacement` if you want parallax. Discard the `.blend`, `.usdc` and `.mtlx` authoring formats, the bare `.png` web thumbnail, and `_NormalDX`.

Take NormalGL, never NormalDX. They differ only in the sign of the green channel; DX is for DirectX-convention engines and Godot uses OpenGL's. Pick wrong and the lighting inverts along one axis, surfaces look subtly off, and you lose an afternoon to it.

The bundled `StandardMaterial3D` is nearly usable and does correctly point at NormalGL, but needs two fixes. Its `ext_resource` paths are relative and its UIDs are ambientCG placeholders no Godot project has generated, so rewrite the paths as `res://…` and drop the `uid=` attributes. And it ships `heightmap_scale = 1.0`; full-strength parallax on a tray frame at a shallow angle swims as the camera moves, so it's down to `0.03` here. The normal map does nearly all the work.

`uv1_scale` is 4 so the grain tiles across the frame instead of stretching one board over the whole thing.

### Tint in the material, not the image

`Fabric034` is the only felt on ambientCG and it's white. Its albedo averages `rgb(161,159,165)` with a colour spread of 6 across 255, near-perfectly neutral, so it multiplies to any colour cleanly. The colour lives in `albedo_color`:

| Material | `albedo_color` | Result |
|---|---|---|
| `felt_green.tres` | `(0.16, 0.50, 0.30)` | ~`rgb(26,80,49)`, poker-table green |
| `felt_teal.tres` | `(0.13, 0.50, 0.50)` | ~`rgb(21,80,82)`, teal mat |

Both land within a few points of real gaming felt. One texture serves any number of mats, each a 600-byte `.tres` sharing the same 3.9 MB of maps, and changing my mind is one value in the inspector with the result live on screen.

Heightmap is off for felt because cloth is flat and the tray is seen from above. `metallic_specular` is 0.15 since felt is matte, and `uv1_scale` is 3 so the weave reads at the right physical size on a 640 mm tray.

### Generated dice need triplanar

`DieParts.BuildMesh` generates the solids from `DieSolid` and never emits texture coordinates, so a normal material samples one corner pixel and the die comes out a flat colour. Nothing errors, which makes it easy to blame on the texture.

```
uv1_triplanar = true
uv1_world_triplanar = false
uv1_scale = Vector3(12, 12, 12)
```

Triplanar projects down all three axes and blends by surface normal, so it needs no UVs. It suits stone: the veining flows around the die as one continuous block instead of being cut into a patch per face.

`uv1_world_triplanar = false` matters. Local-space projection fixes the pattern to the die so it tumbles with it. World-space would make the veining swim across the surface as the die rolls, and you'd immediately see the geometry was generated.

Cost is three texture samples per pixel instead of one, which is irrelevant at 90 px.

It's all in `uv1_scale`. At 50 mm, `uv1_scale = 12` puts about half a repeat across the die, giving the large sweeping veins that make gemstone dice look good. Raise it for busier stone.

Watch the numerals. `Onyx011` averages `rgb(97,102,95)`, and `DieBody.Ink` defaults to near-black `(0.12, 0.10, 0.09)`, which is close to invisible on it. Overridden to a warm off-white in `die.tscn`. Pale gold `(0.85, 0.72, 0.38)` looks nicer on stone but needs a readability check first.

### Other sites don't follow ambientCG's conventions

The ShareTextures amethyst needed two conversions.

Its normal map is DirectX and nothing says so. Rather than guess, it's measurable: take the vertical gradient of the displacement map and correlate against the normal's green channel. Positive is GL, negative is DX.

```
normal.jpg     r = -0.881   DX
normalgl.jpg   r = +0.884   GL, correct for Godot
```

Fix is inverting the green channel. Worth measuring rather than eyeballing, since the failure mode is that everything just lights slightly wrong forever.

It also ships `specular` rather than `roughness`, which is the older specular/glossiness workflow. The specular map averaged 26/255, spiking to 140 on crystal facets. Converted by inverting and remapping into a polished-gemstone band:

```
rough = 0.10 + (1 - normalised_specular) * 0.35     ->  mean 0.38, range 0.10..0.45
```

Facets come out glossy, the body stays satin, nothing goes mirror-like. Using the specular map raw would have made the die uniformly dull. It ships an AO map too, wired at `ao_light_affect = 0.4`.

Every source names things differently and some name them wrongly. `diffuse` is albedo, `specular` needs converting, and you have to measure a normal map's convention instead of trusting the filename.

### Amethyst is parked

It reads blurry on the dice and the onyx is back in `die.tscn`. The processed maps stay in the repo since swapping is one line.

Turned out not to be the texture. Measured against the onyx it's sharper on every axis: contrast 35.9 vs 25.4, fine detail 11.39 vs 8.61, strong edges 33.8 vs 21.4.

The cause is `uv1_scale`, and it's a mistake about what kind of stone this is. Onyx is broad banding, so magnifying it gives sweeping veins that read well. Amethyst is fine crystal structure, and magnified the same amount that becomes large soft blobs.

When I come back to it: raise `uv1_scale` a long way, try 25 to 40. Turn off `ao_enabled`, since at high magnification a 1K AO map adds mushy dark patches. Then raise `normal_scale` back toward 1.0 if the facets still look soft. Maybe half an hour of work, and not urgent.

## Credits

CC0 requires nothing, but three lines on a credits screen are cheap:

> Textures from ambientCG.com, PolyHaven.com and ShareTextures.com.
> Sound from Freesound.org.
> All assets used under CC0.
