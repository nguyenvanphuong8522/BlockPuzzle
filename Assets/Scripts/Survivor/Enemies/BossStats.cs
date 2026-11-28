using UnityEngine;

namespace Survivor
{
    // Stats of ONE boss (numbers for balancing): Assets/Resources/Bosses/<Id>/<Id>_Stats.asset - health, speed, damage.
    // How it behaves (when it uses which skill, cast times, sizes, colors) is in <Id>_Settings.asset next to it.
    [CreateAssetMenu(menuName = "Survivor/Boss Stats", fileName = "BossStats")]
    public sealed class BossStats : ScriptableObject
    {
        [Header("Máu")]
        [Tooltip("HP of the first boss.")]
        [Min(1f)] public float Hp = 10000f;
        [Tooltip("Extra HP for each later boss: 0.6 = +60% per appearance (2nd boss = 1.6x, 3rd = 2.2x).")]
        [Min(0f)] public float HpGrowthPerAppearance = 0.6f;

        [Header("Di chuyển")]
        [Tooltip("Walking speed (world units / second).")]
        [Min(0f)] public float MoveSpeed = 2.1f;

        [Header("Sát thương")]
        [Tooltip("Damage when the hero touches the boss.")]
        [Min(0f)] public float ContactDamage = 25f;
        [Tooltip("Lao tới: damage when the dashing boss runs into the hero.")]
        [Min(0f)] public float DashDamage = 25f;
        [Tooltip("Quẹt càng: damage of one swing.")]
        [Min(0f)] public float SwipeDamage = 20f;
        [Tooltip("Đạn tỏa: damage of one bullet.")]
        [Min(0f)] public float BulletDamage = 15f;
        [Tooltip("Damage when the hero touches the arena wall.")]
        [Min(0f)] public float ArenaWallDamage = 12f;

        public static BossStats LoadOrDefault()
        {
            var stats = Resources.Load<BossStats>("BossStats");
            return stats != null ? stats : CreateInstance<BossStats>();
        }
    }
}
