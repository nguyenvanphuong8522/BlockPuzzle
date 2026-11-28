using UnityEngine;

namespace Survivor
{
    // Stats of ONE enemy type (numbers for balancing): Assets/Resources/Enemies/<Id>/<Id>_Stats.asset.
    // How that type behaves is in <Id>_Settings.asset next to it. Live-tunable in Play mode (applies to enemies
    // spawned after the change). Rules shared by every type are in EnemyStats.asset / EnemySettings.asset.
    [CreateAssetMenu(menuName = "Survivor/Enemy Type Stats", fileName = "Enemy_Stats")]
    public sealed class EnemyTypeStats : ScriptableObject
    {
        [Header("Chỉ số")]
        [Tooltip("Health at the start of the run (grows over time: EnemyStats Hp Growth Per Minute).")]
        [Min(1f)] public float Hp = 10f;
        [Tooltip("Walking speed (world units / second).")]
        [Min(0f)] public float Speed = 1.5f;
        [Tooltip("Damage when it touches the hero.")]
        [Min(0f)] public float Damage = 5f;
        [Tooltip("Body size (hit radius, world units). The art is drawn to match.")]
        [Min(0.05f)] public float Radius = 0.35f;

        [Header("Khi là tinh anh (hệ số so với con thường)")]
        [Min(0.1f)] public float EliteHpMultiplier = 15f;
        [Min(0f)] public float EliteDamageMultiplier = 1.5f;
        [Min(0.1f)] public float EliteSpeedMultiplier = 0.9f;
        [Tooltip("Bigger: drawn size and hit size.")]
        [Min(0.5f)] public float EliteSizeMultiplier = 1.8f;
        [Tooltip("Experience × this.")]
        [Min(1f)] public float EliteXpMultiplier = 5f;

        [Header("Bắn xa (chỉ dùng nếu loại này bắn xa)")]
        [Tooltip("Damage of one shot.")]
        [Min(0f)] public float ShotDamage = 8f;

        // Code defaults of a type (used when its asset is missing, and to create it).
        public static EnemyTypeStats CreateDefault(EnemyDefinition def)
        {
            var s = CreateInstance<EnemyTypeStats>();
            s.name = def.Id + "_Stats";
            s.Hp = def.DefaultHp;
            s.Speed = def.DefaultSpeed;
            s.Damage = def.DefaultDamage;
            s.Radius = def.DefaultRadius;
            return s;
        }
    }
}
