using Core.Dice;

namespace Game.Tray
{
    // one entry in the pool as handed to the resolver, kept in throw order
    public readonly struct TraySlot
    {
        public readonly string LabelKey;
        public readonly Die Die;
        public readonly int Value;

        public TraySlot(string labelKey, Die die, int value)
        {
            LabelKey = labelKey;
            Die = die;
            Value = value;
        }
    }
}
