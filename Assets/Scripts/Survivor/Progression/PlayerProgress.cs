using System;
using R3;

namespace Survivor
{
    public sealed class PlayerProgress : IDisposable
    {
        public ReactiveProperty<int> Level { get; } = new ReactiveProperty<int>(1);
        public ReactiveProperty<int> Xp { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> XpToNext { get; } = new ReactiveProperty<int>(Required(1));

        // Level-ups waiting for the player to pick an upgrade.
        public int PendingLevelUps { get; set; }

        // Chests picked up and waiting to be opened.
        public int PendingChests { get; set; }

        public void AddXp(int amount)
        {
            var xp = Xp.Value + amount;
            while (xp >= XpToNext.Value)
            {
                xp -= XpToNext.Value;
                Level.Value++;
                XpToNext.Value = Required(Level.Value);
                PendingLevelUps++;
            }
            Xp.Value = xp;
        }

        static int Required(int level) => 5 + (level - 1) * 6 + Math.Max(0, level - 20) * 10;

        public void Dispose()
        {
            Level.Dispose();
            Xp.Dispose();
            XpToNext.Dispose();
        }
    }
}
