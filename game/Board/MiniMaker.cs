using System;
using System.Collections.Generic;
using Godot;
using Content.Campaigns;
using Content.Minis;
using Core.Characters;
using Game.Audio;

namespace Game.Board
{
    // THE TREATMENT, AT LOAD TIME (MINIS_AND_ART.md A0, A1, A2).
    //
    // B5 established that nine CC0 packs are made to look like one game by three steps -
    // `tools/pull-models.ps1` culls, `tools/bake-palette.ps1` recolours, and
    // `shaders/painted_miniature.gdshader` relights. The first two are build-time tools and stay
    // that way; the third is the one that has to run on a model the build has never seen, and this
    // is where it does. A0's whole deliverable is that sentence made true: "a recoloured, resized
    // figure, painted into the look, no new model file and no code change."
    //
    // THE MATHS DOES NOT MOVE, AND THAT IS THE ARCHITECTURE NOTE THIS FILE EXISTS TO HONOUR. A
    // supplied model goes through `PaintedModel` and `Mini.Stand` - the exact code a shipped wall
    // panel and the shipped Barbarian go through. There is deliberately no second sizing path for
    // "mod" models, because a mini is a mini, and a fork here is how a pack ends up looking
    // subtly wrong in a way nobody can point at.
    //
    // EVERY FAILURE IS A BOX, NEVER AN EXCEPTION. `BoardTiles` has said "where there is no model
    // the boxes are still here" since B5, and this is that discipline applied to the thing the
    // player actually looks at: an unresolved id, a model over a cap, a file that will not parse,
    // an importer that throws - each of them is a right-sized box in the right square with a
    // sentence in the log, and the fight is playable. "Never replace the box with a crash."
    //
    // THE CAPS ARE CHECKED BEFORE GODOT IS ASKED TO OPEN ANYTHING. `Content.Models.ModelReader`
    // ran at load (`Package.CrossCheckArt`) and runs again here for the one model about to be
    // instanced, because those are different moments and a file can change between them. Only then
    // does `GltfDocument` see it.
    public sealed class MiniMaker
    {
        readonly MiniScenes _scenes;

        readonly Shelf _shelf;

        // 42 mm of base under a 75 mm figure - mini.tscn's numbers, in one place, because a piece
        // built in code has to stand on the same base as one built in a scene or the board reads
        // as two different games
        public const float BaseRadius = 0.021f;

        public const float BaseHeight = 0.008f;

        public MiniMaker(MiniScenes scenes, Shelf shelf)
        {
            _scenes = scenes;
            _shelf = shelf;
        }

        // the painted-miniature shader, handed in from the scene rather than loaded by path - the
        // same arrangement `BoardTiles.Paint` uses, and for the same reason
        public Shader Painted { get; set; }

        // what a piece built in code stands on. Null is survivable: the base goes unpainted and
        // says so, because a piece with no base is still a piece
        public Material BasePaint { get; set; }

        // how wide a square is, which is what `Fit.Cell` means. Handed in from `BoardMetrics`
        public float CellSize { get; set; } = 0.06f;

        // ---- making one ----

        // <paramref name="pack"/> is the campaign asking, so a bare mini name resolves to its own
        // minis before the shared roster's; <paramref name="named"/> is what its statblock wrote,
        // and may be empty. <paramref name="tier"/> is the fallback - the tiered proxy a foe that
        // named no mini has stood as since Phase C
        public Mini Make(string pack, string named, Tier tier, string name)
        {
            // NOTHING NAMED IS NOT A FAILURE. It is every campaign written before this phase, and
            // the answer is the figure it has always had
            if (string.IsNullOrEmpty(named)) return FromScene(_scenes?.For(tier), null, name);

            string why = "there is no shelf loaded";

            Mounted mounted = _shelf == null ? null : _shelf.Mount(pack, named, out why);

            if (mounted == null)
            {
                // NAMED, AND NAMED WITH THE MINI THAT FAILED. "the shelf tile says which mini
                // failed" (A1) - the log is the shelf until Phase R builds one
                GD.PushError($"mini: '{named}' could not be resolved - {why}. " +
                             $"'{name}' is standing on a placeholder box");

                return Box(name);
            }

            if (mounted.IsShipped)
            {
                PackedScene scene = _scenes?.Of(mounted.Source);

                if (scene != null) return FromScene(scene, mounted, name);

                GD.PushError($"mini: '{mounted.Id}' resolves to the shared mini " +
                             $"'{mounted.Source}', and this scene was not given a figure for it - " +
                             $"'{name}' is standing on a placeholder box");

                return Box(name);
            }

            return FromModel(mounted, name);
        }

        // ---- a shipped figure, optionally re-dressed (A0) ----

        // THE VARIANT PATH, and it is three assignments rather than a pipeline. A0's "the shipped
        // skeleton, but bone-white and a head taller" is exactly a tint on the material and a
        // number in `FigureHeight`, applied BEFORE the node enters the tree - `Mini._Ready` is
        // what sizes and paints the figure, and it has not run yet
        Mini FromScene(PackedScene scene, Mounted mounted, string name)
        {
            if (scene == null) return Box(name);

            Mini mini;

            try
            {
                mini = scene.Instantiate<Mini>();
            }
            catch (Exception could)
            {
                GD.PushError($"mini: '{name}' could not be instanced - {could.Message}");
                return Box(name);
            }

            if (mini == null)
            {
                GD.PushError($"mini: {scene.ResourcePath} is not a Mini scene - '{name}' is a box");
                return Box(name);
            }

            mini.Name = name;

            if (mounted != null) Dress(mini, mounted, Figure(mini));

            return mini;
        }

        Node3D Figure(Mini mini) => mini.GetNodeOrNull<Node3D>(mini.FigurePath);

        // ---- or a supplied model (A2) ----

        Mini FromModel(Mounted mounted, string name)
        {
            Node3D figure = Load(mounted, name);

            if (figure == null) return Box(name);

            Mini mini = Blank(name);

            figure.Name = "Figure";
            mini.AddChild(figure);

            Dress(mini, mounted, figure);

            mini.Clips = MiniClips.Over(figure, mounted);

            return mini;
        }

        // THE ONE PLACE A STRANGER'S FILE IS OPENED, and it is opened only after `ModelReader` has
        // said yes to every cap in it. The re-check is not paranoia about the validator: the load
        // and the validation are different moments, and a subscribed folder is updated by Steam
        // rather than by the game
        Node3D Load(Mounted mounted, string name)
        {
            Content.Schema.Read<Content.Models.ModelFacts> checkd =
                Content.Models.ModelReader.Inspect(mounted.Folder, mounted.Model);

            if (!checkd.Ok)
            {
                foreach (Content.Schema.ContentProblem problem in checkd.Problems)
                    GD.PushError($"mini: '{mounted.Id}' - {problem}");

                GD.PushError($"mini: '{name}' is standing on a placeholder box");

                return null;
            }

            try
            {
                var document = new GltfDocument();
                var state = new GltfState();

                Error opened = document.AppendFromFile(mounted.File, state);

                if (opened != Error.Ok)
                {
                    GD.PushError($"mini: '{mounted.Id}' would not import - {opened}. " +
                                 $"'{name}' is standing on a placeholder box");
                    return null;
                }

                if (document.GenerateScene(state) is not Node3D figure)
                {
                    GD.PushError($"mini: '{mounted.Id}' imported to nothing standable. " +
                                 $"'{name}' is standing on a placeholder box");
                    return null;
                }

                return figure;
            }
            catch (Exception could)
            {
                // THE ISOLATION BOUNDARY AT ITS SHARPEST. An importer throwing on a stranger's
                // file is exactly the case A2 is written around, and the answer is one box and a
                // sentence - never the fight, never the pack, never the game
                GD.PushError($"mini: '{mounted.Id}' threw while importing - {could.Message}. " +
                             $"'{name}' is standing on a placeholder box");
                return null;
            }
        }

        // ---- the treatment itself ----

        // SIZE, FOOT AND COLOUR, and nothing else a pack may touch. `brush_strength`, `varnish`
        // and the banding are the game's signature rather than a pack's decision (`Tint`)
        void Dress(Mini mini, Mounted mounted, Node3D figure)
        {
            mini.FigureHeight = Tall(mounted, figure, mini.FigureHeight);

            // `Mini.Stand` computes the foot correction and SUBTRACTS it from whatever Y the
            // figure already had, so seeding that Y is how an extra nudge is expressed - no second
            // correction, and no arithmetic re-typed here
            if (figure != null && mounted.Foot != 0f)
                figure.Position = new Vector3(figure.Position.X,
                                              figure.Position.Y + mounted.Foot,
                                              figure.Position.Z);

            mini.Paint = Tinted(mini.Paint, mounted.Tint);

            // A0 AND A3 MEET HERE, and it is one assignment because they are the same idea: a
            // variant re-dresses a shipped figure, and a variant may re-dress what it SOUNDS like
            // as easily as what it looks like
            mini.Foley = PackVoice.Over(mounted);
        }

        // `Fit.Cell` EXPRESSED AS A HEIGHT, which is how the sizing path stays single. `Mini.Stand`
        // fits by height because a miniature is a height; a model fitted to the CELL is the same
        // arithmetic done the other way round, so it is turned into the height that produces it
        // rather than given a second code path to travel down
        float Tall(Mounted mounted, Node3D figure, float already)
        {
            if (mounted.Fit == Fit.Height)
                return mounted.Height > 0f ? mounted.Height : already;

            if (figure == null) return already;

            Aabb bounds = PaintedModel.Bounds(figure);
            float widest = Mathf.Max(bounds.Size.X, bounds.Size.Z);

            if (widest <= 0f || bounds.Size.Y <= 0f) return already;

            return CellSize * (bounds.Size.Y / widest);
        }

        // THE SHADER, WEARING THIS MINI'S COLOUR. Duplicated rather than edited, because a
        // material out of a `.tres` is SHARED - tinting it in place would repaint every other
        // piece in the game that happens to use the same file, which is a bug that looks like a
        // rendering fault
        Material Tinted(Material already, Tint tint)
        {
            if (!tint.IsSomething) return already ?? Fresh();

            if ((already ?? Fresh()) is not ShaderMaterial shader)
            {
                GD.PushError("mini: this piece's paint is not the painted-miniature shader, so a " +
                             "tint cannot be applied to it - it will wear the colour it came with");
                return already;
            }

            var mine = (ShaderMaterial)shader.Duplicate();

            mine.SetShaderParameter("tint", new Color(tint.R, tint.G, tint.B));

            return mine;
        }

        ShaderMaterial Fresh() =>
            Painted != null ? new ShaderMaterial { Shader = Painted } : null;

        // ---- and what stands there when none of that worked ----

        // A RIGHT-SIZED BOX IN THE RIGHT SQUARE. Not an apology: it is how every square and line
        // on this board was verified before B5 put art on it, and it is what keeps a half-broken
        // pack PLAYABLE while the log says exactly what to fix
        public Mini Box(string name)
        {
            Mini mini = Blank(name);

            var box = new MeshInstance3D
            {
                Name = "Figure",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(CellSize * 0.5f, CellSize * 0.8f, CellSize * 0.5f),
                },
            };

            if (BasePaint != null) box.MaterialOverride = BasePaint;

            mini.AddChild(box);

            return mini;
        }

        // A PIECE WITH A BASE AND A VOICE AND NOTHING ON IT, built in code rather than out of a
        // scene. There is no `pack_mini.tscn`, deliberately: a scene whose only content is a
        // cylinder and an audio player would be a fourth copy of `mini.tscn`'s base to keep in
        // step with the other three, and `BoardTiles` already builds its boxes this way
        Mini Blank(string name)
        {
            var mini = new Mini { Name = name };

            mini.AddChild(new MeshInstance3D
            {
                Name = "Base",
                Mesh = new CylinderMesh
                {
                    TopRadius = BaseRadius,
                    BottomRadius = BaseRadius * 1.14f,
                    Height = BaseHeight,
                    RadialSegments = 24,
                    Rings = 1,
                },
                MaterialOverride = BasePaint,
                Position = new Vector3(0f, BaseHeight * 0.5f, 0f),
            });

            // matched to mini.tscn's player, so a piece built in code and one built in a scene are
            // heard at the same distance and the same width (DieAudio.Take)
            mini.AddChild(new AudioStreamPlayer3D
            {
                Name = "Voice",
                UnitSize = 0.9f,
                MaxDb = 4f,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance,
                PanningStrength = 1.2f,
            });

            return mini;
        }

        // DEVELOPER ONLY - not localized, never reaches a player
        public override string ToString() =>
            $"{_scenes}, {(_shelf == null ? "no shelf" : _shelf.ToString())}";
    }
}
