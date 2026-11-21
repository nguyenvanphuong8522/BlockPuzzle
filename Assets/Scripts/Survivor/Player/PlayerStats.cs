namespace Survivor
{
    // Final stat = base value from PlayerTuning (editable live) combined with bonuses from upgrades this run.
    public sealed class PlayerStats
    {
        readonly PlayerTuning baseStats;

        public PlayerStats(PlayerTuning baseStats) => this.baseStats = baseStats;

        // Bonuses collected from upgrades during the run.
        public float BonusMaxHp;
        public float MoveSpeedMultiplier = 1f;
        public float MagnetMultiplier = 1f;
        public float BonusMight;
        public float HasteMultiplier = 1f;
        public float BonusArea;

        public float MaxHp => baseStats.MaxHp + BonusMaxHp;
        public float MoveSpeed => baseStats.MoveSpeed * MoveSpeedMultiplier;
        public float MagnetRadius => baseStats.MagnetRadius * MagnetMultiplier;
        public float Might => baseStats.Might + BonusMight;   // damage multiplier
        public float Haste => baseStats.Haste * HasteMultiplier; // cooldown multiplier (lower = faster)
        public float Area => baseStats.Area + BonusArea;      // size multiplier for area weapons
    }
}
