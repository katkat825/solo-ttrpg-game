using System;
using System.Collections.Generic;
using Godot;
using Content.Campaigns;
using Content.Minis;
using Core.Characters;
using Game.Audio;

namespace Game.Board
{
    public sealed class MiniMaker
    {
        readonly MiniScenes _scenes;

        readonly Shelf _shelf;

        // mini.tscn's base numbers, in one place, so a code-built piece matches a scene-built one
        public const float BaseRadius = 0.021f;

        public const float BaseHeight = 0.008f;

        public MiniMaker(MiniScenes scenes, Shelf shelf)
        {
            _scenes = scenes;
            _shelf = shelf;
        }

        public Shader Painted { get; set; }

        // null is survivable: the base just goes unpainted
        public Material BasePaint { get; set; }

        public float CellSize { get; set; } = 0.06f;

        // the pack resolves its own minis before the shared roster; tier is the fallback
        public Mini Make(string pack, string named, Tier tier, string name)
        {
            // nothing named is not a failure: the tiered figure is what every older campaign already used
            if (string.IsNullOrEmpty(named)) return FromScene(_scenes?.For(tier), null, name);

            string why = "there is no shelf loaded";

            Mounted mounted = _shelf == null ? null : _shelf.Mount(pack, named, out why);

            if (mounted == null)
            {
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

        // re-dress before the node enters the tree, because Mini._Ready sizes and paints and has not run yet
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

        // re-check the caps here: load and validation are different moments, and a subscribed folder can change between them
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
                // an importer throwing on a stranger's file becomes one box and a sentence, never a crash
                GD.PushError($"mini: '{mounted.Id}' threw while importing - {could.Message}. " +
                             $"'{name}' is standing on a placeholder box");
                return null;
            }
        }

        // size, foot and colour only; brush_strength, varnish and banding are the game's signature, not a pack's
        void Dress(Mini mini, Mounted mounted, Node3D figure)
        {
            mini.FigureHeight = Tall(mounted, figure, mini.FigureHeight);

            // Mini.Stand subtracts the foot correction from the figure's Y, so seeding Y here is the extra nudge
            if (figure != null && mounted.Foot != 0f)
                figure.Position = new Vector3(figure.Position.X,
                                              figure.Position.Y + mounted.Foot,
                                              figure.Position.Z);

            mini.Paint = Tinted(mini.Paint, mounted.Tint);

            mini.Foley = PackVoice.Over(mounted);
        }

        // Fit.Cell turned into the height that produces it, so there is one sizing path, not two
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

        // duplicate, don't edit: a .tres material is shared, so tinting in place would repaint every piece using it
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

        // a right-sized box keeps a half-broken pack playable while the log says what to fix
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

        // built in code, not a scene: a pack_mini.tscn would be a fourth copy of mini.tscn's base to keep in step
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

            // audio params matched to mini.tscn, so code-built and scene-built pieces sound the same
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

        // developer only, not localized, never reaches a player
        public override string ToString() =>
            $"{_scenes}, {(_shelf == null ? "no shelf" : _shelf.ToString())}";
    }
}
