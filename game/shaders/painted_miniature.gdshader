shader_type spatial;
render_mode cull_back, specular_schlick_ggx;

// THE PAINTED MINIATURE. One shader, and it is the game's whole visual signature.
//
// ART_DIRECTION.md section 9.2 asks for five things and this is all five: flat banded lighting, a
// subtle dark rim, visible brushstroke noise in the albedo, a hint of primer grey in the crevices,
// and a slight gloss where a real painter would have varnished. Written once, it goes on every
// mini, tile and prop in the game.
//
// WHY IT IS WORTH A SHADER RATHER THAN BETTER TEXTURES. Nine CC0 packs drawn by four different
// people cannot be made to look like one game by texture work - there is too much of it, and the
// differences are in the shading rather than in the colours. One palette (tools/bake-palette.ps1)
// fixes the colours; this fixes the shading, because it throws the packs' own lighting away and
// relights everything the same way. And it is a look almost nobody else has, for the reason
// ART_DIRECTION gives: no one else's game is about miniatures.
//
// EVERY NUMBER HERE IS A DIAL AND THEY ARE MEANT TO BE TURNED. Nothing in this file can be judged
// by reading it - THE_BOARD.md B5 says so twice: "this is in-editor, see-it-to-do-it work" and
// "do not author them blind". The defaults below are a starting point chosen from the physical
// model, not a finished look. Open table.tscn, select the material, and move them while watching.

// ---- the paint ----

uniform sampler2D atlas : source_color, filter_linear_mipmap, hint_default_white;

uniform vec4 tint : source_color = vec4(1.0, 1.0, 1.0, 1.0);

// ---- flat banded lighting ----

// how many steps of light there are between shadow and full. THREE IS THE WHOLE LOOK: a painter
// does base, shade and highlight, and a fourth band starts reading as a gradient again
uniform int bands : hint_range(2, 6) = 3;

// how far each band bleeds into the next. zero is a hard cel edge, which reads as cartoon rather
// than as paint; a little softness reads as a brush
uniform float band_blend : hint_range(0.0, 1.0) = 0.14;

// the darkest a surface gets from this light. a mini in shadow is not black - it is the same
// paint, darker - and the room's ambient adds to this rather than replacing it
uniform float shade_floor : hint_range(0.0, 1.0) = 0.28;

// GODOT APPLIES ALBEDO TO DIFFUSE_LIGHT AFTER THIS SHADER'S light() RUNS, so this stays at 0 and
// the light function returns light rather than lit colour. It is a dial rather than a constant
// for one reason: if a build ever composites the other way round, every surface in the game goes
// flat white and this is the one line that fixes it. Put it to 1 and the look comes back.
uniform float albedo_in_light : hint_range(0.0, 1.0) = 0.0;

// ---- the liner: a dark edge, not a bright one ----

// a painted figure is lined and washed, so its edges go DARK where a shiny one would go bright.
// this is the single cheapest thing that makes a model read as painted rather than as plastic
uniform vec4 liner : source_color = vec4(0.086, 0.075, 0.102, 1.0);

uniform float liner_width : hint_range(0.0, 1.0) = 0.45;

uniform float liner_strength : hint_range(0.0, 1.0) = 0.5;

// ---- primer in the crevices ----

// what the model was undercoated with, showing through where a brush could not reach. downward
// faces and undercuts, which is a decent approximation of a crevice without baked occlusion -
// B5 has no AO maps and KayKit models carry no vertex colours to fake one from
uniform vec4 primer : source_color = vec4(0.42, 0.41, 0.40, 1.0);

uniform float primer_depth : hint_range(0.0, 1.0) = 0.28;

// ---- brushwork ----

// visible strokes in the albedo. IN WORLD SPACE, not UV space, because every tile in the dungeon
// pack shares one atlas: in UV space a wall of twelve blocks would wear twelve identical
// brushstrokes, which reads as a texture bug rather than as paint
uniform float brush_strength : hint_range(0.0, 0.5) = 0.08;

uniform float brush_scale : hint_range(1.0, 200.0) = 55.0;

// strokes are long and thin, the way a brush is
uniform float brush_stretch : hint_range(1.0, 12.0) = 4.0;

// ---- varnish ----

// a hint of gloss, tight and small. a matte figure looks unfinished and a shiny one looks like a
// toy, so this wants to be barely there. STARTED AT 0.16 and read as a hard white spot on the mini
// under the sun - the highlight below is tight (varnish_tightness) so at 0.16 it blooms rather than
// glints. Dropped to 0.06; set it to 0.0 to confirm the white spot is this and not the atlas, then
// dial back up until it just catches the light
uniform float varnish : hint_range(0.0, 1.0) = 0.06;

uniform float varnish_tightness : hint_range(1.0, 64.0) = 22.0;

varying vec3 world_normal;
varying vec3 world_position;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// value noise, smoothed. cheap on purpose: this is a wobble in the paint, not a material
float paint_noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 into = fract(p);

    into = into * into * (3.0 - 2.0 * into);

    float a = hash(cell);
    float b = hash(cell + vec2(1.0, 0.0));
    float c = hash(cell + vec2(0.0, 1.0));
    float d = hash(cell + vec2(1.0, 1.0));

    return mix(mix(a, b, into.x), mix(c, d, into.x), into.y);
}

void vertex() {
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec4 texel = texture(atlas, UV) * tint * COLOR;
    vec3 paint = texel.rgb;

    // strokes, laid across the surface rather than along its UVs
    vec2 across = vec2(world_position.x + world_position.z, world_position.y) * brush_scale;
    float stroke = paint_noise(vec2(across.x / brush_stretch, across.y));

    paint *= 1.0 + (stroke - 0.5) * brush_strength * 2.0;

    // primer, under the paint, where the brush did not reach
    float underneath = clamp(-dot(world_normal, vec3(0.0, 1.0, 0.0)), 0.0, 1.0);

    paint = mix(paint, primer.rgb, underneath * primer_depth);

    // the liner round the silhouette. mix rather than multiply, so it lands on the liner's own
    // colour rather than on black however dark the paint under it is
    float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
    float edge = pow(1.0 - facing, mix(7.0, 1.6, liner_width));

    paint = mix(paint, liner.rgb, edge * liner_strength);

    ALBEDO = paint;
    ALPHA = texel.a;

    METALLIC = 0.0;
    SPECULAR = varnish * 0.5;
    ROUGHNESS = 1.0 - varnish * 0.75;
}

void light() {
    float lambert = clamp(dot(normalize(NORMAL), normalize(LIGHT)), 0.0, 1.0);

    // rounded to the nearest band rather than floored, so the middle band sits where a painter
    // would put it rather than everything sliding one step dark
    float steps = max(float(bands) - 1.0, 1.0);
    float stepped = clamp(floor(lambert * steps + 0.5) / steps, 0.0, 1.0);

    stepped = mix(stepped, lambert, band_blend);

    DIFFUSE_LIGHT += mix(shade_floor, 1.0, stepped) * ATTENUATION * LIGHT_COLOR
                     * mix(vec3(1.0), ALBEDO, albedo_in_light);

    // the varnish: one small tight highlight, banded like everything else rather than a smooth
    // falloff - a gloss coat catches the light in a shape, not in a gradient
    vec3 halfway = normalize(normalize(LIGHT) + normalize(VIEW));
    float gloss = pow(clamp(dot(normalize(NORMAL), halfway), 0.0, 1.0), varnish_tightness);

    SPECULAR_LIGHT += LIGHT_COLOR * ATTENUATION * varnish * smoothstep(0.25, 0.45, gloss);
}
