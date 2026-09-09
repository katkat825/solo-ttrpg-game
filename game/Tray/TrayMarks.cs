using System.Collections.Generic;
using Godot;
using Core.Localization;
using Game.Dice;

namespace Game.Tray
{
    // every mark on the felt for one throw - cleared when the dice go up, drawn when they stop
    // reads TrayThrow.Roles and TrayThrow.Slots straight across, both in throw order
    // no branch in here on values or totals - core/ already decided all of that
    // marks are built and freed each throw rather than pooled, so nothing survives a throw
    public partial class TrayMarks : Node3D
    {
        public ILocalizer Text { get; set; }

        // how big the tray is and where its felt sits, handed down to every mark
        // one measurement reaches the whole felt from here, rather than each mark carrying a copy
        public TrayBounds Bounds { get; set; } = TrayBounds.Shipped;

        readonly List<DieMark> _marks = new();

        // marks place themselves in TRAY coordinates, which is only true while this node sits
        // exactly on the tray's origin. held here rather than assumed - it costs one line, and
        // the alternative is every mark converting to world space and back for the same answer
        public override void _Ready() => Transform = Transform3D.Identity;

        // dice must be in throw order - the same order given to TrayResolution.Resolve
        // that is what makes indexing them by slot correct
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

            // the snagged die already has a mark saying what it is - this says what just happened
            // to it, which is a different question, so it is a second node rather than a fourth
            // DieRole value that would cost us knowing whether it counted
            if (thrown.SnaggedSlot >= 0)
            {
                int s = thrown.SnaggedSlot;

                AddChild(new SnagFlash
                {
                    Name = $"{dice[s].Name}Snag",
                    Die = dice[s],
                    Bounds = Bounds,
                    TraySpace = this,

                    // asked rather than measured, so the flash cannot end up under the ring it
                    // is supposed to be leaving
                    InnerRadius = DieMark.OuterRadiusFor(dice[s].Solid, thrown.Roles[s]),
                });
            }
        }

        // called the moment a throw starts, so nothing stale is drawn over live dice
        // everything under here belongs to one throw, so it frees children rather than a list -
        // a list is a second description of the same thing, and the day a third kind of mark is
        // added the list is the copy that gets forgotten
        public void Clear()
        {
            foreach (Node child in GetChildren())
            {
                RemoveChild(child);
                child.QueueFree();
            }

            _marks.Clear();
        }

        // each mark already re-reads on Godot's translation-changed notification
        // this is the belt to that pair of braces, called from the tray's locale switch
        // a label left in the old language is exactly the bug the pseudolocale hunts
        public void Retranslate()
        {
            foreach (DieMark mark in _marks) mark.Retranslate();
        }
    }
}
