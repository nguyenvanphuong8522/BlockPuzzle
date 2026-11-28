using UnityEngine;

namespace Survivor
{
    // Stats shared by every normal enemy type: Assets/Resources/EnemyStats.asset.
    // Each type has its own stats in Assets/Resources/Enemies/<Id>/<Id>_Stats.asset (HP, speed, damage, size,
    // elite multipliers, shot damage). Shared behaviour rules: EnemySettings.asset. The boss: BossStats.asset.
    [CreateAssetMenu(menuName = "Survivor/Enemy Stats (shared)", fileName = "EnemyStats")]
    public sealed class EnemyStats : ScriptableObject
    {
        [Header("Chung")]
        [Tooltip("Enemy health grows over the run: +35% per minute survived at 0.35.")]
        [Min(0f)] public float HpGrowthPerMinute = 0.35f;

        [Header("Đàn quái bay ngang")]
        [Tooltip("Health multiplier of swarm enemies (on top of the normal growth over time).")]
        [Min(0.01f)] public float SwarmHpMultiplier = 1f;

        public static EnemyStats LoadOrDefault()
        {
            var stats = Resources.Load<EnemyStats>("EnemyStats");
            return stats != null ? stats : CreateInstance<EnemyStats>();
        }
    }
}
