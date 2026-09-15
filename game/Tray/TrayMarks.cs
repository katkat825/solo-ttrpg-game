using System.Collections.Generic;
using Godot;
using Core.Localization;
using Game.Dice;

namespace Game.Tray
{
    public partial class TrayMarks : Node3D
    {
        public ILocalizer Text { get; set; }

        public TrayBounds Bounds { get; set; } = TrayBounds.Shipped;

        readonly List<DieMark> _marks = new();

        // marks place themselves in tray coordinates, true only while this node sits on the tray's origin - pinned here so they needn't convert to world space and back
        public override void _Ready() => Transform = Transform3D.Identity;

        // dice must be in throw order, the same order given to Resolve, or indexing by slot is wrong
        public void Show(TrayThrow thrown, IReadOnlyList<DieBody> dice)
        {
            Clear();

            if (thrown == null) return;

            if (dice.Count != thrown.Slots.Count)
            {
                GD.PushError($"tray marks: {dice.Count} dice but {thrown.Slots.Count} slots - they pair by index");
                return;
            }

            for (int i = 0; i < dice.Count; i++)
            {
                var mark = new DieMark
                {
                    Name = $"{dice[i].Name}Mark",
                    Die = dice[i],
                    LabelKey = thrown.Slots[i].LabelKey,
                    Role = thrown.Roles[i],
                    Text = Text,
                    Bounds = Bounds,

                    // this node's own frame, which is the tray's - see _Ready
                    TraySpace = this,
                };

                _marks.Add(mark);
                AddChild(mark);
            }

            // a second node, not a fourth DieRole: a snag is orthogonal to whether the die counted, so it doesn't cost that
            if (thrown.SnaggedSlot >= 0)
            {
                int s = thrown.SnaggedSlot;

                AddChild(new SnagFlash
                {
                    Name = $"{dice[s].Name}Snag",
                    Die = dice[s],
                    Bounds = Bounds,
                    TraySpace = this,

                    // asked, not measured, so the flash can't land under the ring it's leaving
                    InnerRadius = DieMark.OuterRadiusFor(dice[s].Solid, thrown.Roles[s]),
                });
            }
        }

        // clear at throw start so nothing stale draws over live dice; frees children directly rather than a tracked list that could fall out of sync
        public void Clear()
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            _marks.Clear();
        }

        // belt-and-braces to each mark's own translation-changed handler, called from the tray's locale switch
        public void Retranslate()
        {
            foreach (DieMark mark in _marks) mark.Retranslate();
        }
    }
}
