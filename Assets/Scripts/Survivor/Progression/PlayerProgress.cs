using System;
using R3;

namespace Survivor
{
    // Level and experience. How much experience each level needs: PlayerTuning, "Kinh nghiệm lên cấp".
    public sealed class PlayerProgress : IDisposable
    {
        readonly PlayerTuning tuning;

        public ReactiveProperty<int> Level { get; } = new ReactiveProperty<int>(1);
        public ReactiveProperty<int> Xp { get; } = new ReactiveProperty<int>(0);
        public ReactiveProperty<int> XpToNext { get; }

        // Level-ups waiting for the player to pick an upgrade.
        public int PendingLevelUps { get; set; }

        // Level-up choices already made this run (decides companion vs item screen, see UpgradeService).
        public int LevelUpsChosen { get; set; }

        // Chests picked up and waiting to be opened.
        public int PendingChests { get; set; }

        public PlayerProgress(PlayerTuning tuning)
        {
            this.tuning = tuning;
            XpToNext = new ReactiveProperty<int>(Required(1));
        }

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

        int Required(int level) => tuning.XpRequired(level);

        public void Dispose()
        {
            Level.Dispose();
            Xp.Dispose();
            XpToNext.Dispose();
        }
    }
}
