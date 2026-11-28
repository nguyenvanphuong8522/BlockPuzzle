using UnityEngine;

namespace Survivor
{
    // How ONE enemy type behaves: Assets/Resources/Enemies/<Id>/<Id>_Settings.asset (its numbers for balancing are in
    // <Id>_Stats.asset next to it). Live-tunable in Play mode. Rules shared by every type (spawn rate, elite schedule,
    // swarm, knockback...) are in EnemySettings.asset.
    [CreateAssetMenu(menuName = "Survivor/Enemy Type Settings", fileName = "Enemy_Settings")]
    public sealed class EnemyTypeSettings : ScriptableObject
    {
        [Header("Xuất hiện")]
        [Tooltip("Seconds into the run before this type starts appearing.")]
        [Min(0f)] public float UnlockTime;
        [Tooltip("Relative spawn chance among the unlocked types (0 = never spawned randomly; scripted events still can).")]
        [Min(0f)] public float Weight = 1f;
        [Tooltip("Can come as an elite (big tough copy).")]
        public bool CanBeElite = true;

        [Header("Phản ứng khi trúng đòn")]
        [Tooltip("How much it gets pushed back by hits, compared to other enemies (0 = not at all, 1 = normal).")]
        [Min(0f)] public float KnockbackScale = 1f;

        [Header("Bắn xa (chỉ dùng nếu loại này bắn xa)")]
        [Tooltip("Distance it tries to keep from the hero (world units): walks in when farther, backs off when closer.")]
        [Min(0.5f)] public float KeepDistance = 5f;
        [Tooltip("Seconds between two shots (each one ± 15% so they don't shoot in sync).")]
        [Min(0.1f)] public float ShotInterval = 2.6f;
        [Tooltip("It only shoots when the hero is within this distance.")]
        [Min(0.5f)] public float ShootRange = 8f;
        [Tooltip("How long it puffs up before shooting (a warning you can see), seconds.")]
        [Min(0f)] public float Windup = 0.35f;
        [Tooltip("Shot: speed, hit radius, lifetime.")]
        [Min(0.5f)] public float ShotSpeed = 5.5f;
        [Min(0.02f)] public float ShotRadius = 0.2f;
        [Min(0.1f)] public float ShotLifetime = 3f;
        [Tooltip("Drawn size of the shot (world units).")]
        [Min(0.05f)] public float ShotSize = 0.45f;
        [Tooltip("Color of the shot (and its splash).")]
        public Color ShotColor = new Color(1f, 0.12f, 0.08f, 1f);
        [Tooltip("Glow around the shot (alpha = how strong). It pulses a little.")]
        public Color ShotGlowColor = new Color(1f, 0.2f, 0.1f, 0.4f);
        [Tooltip("Size of the glow compared to the shot.")]
        [Min(1f)] public float ShotGlowSize = 2.2f;
        [Tooltip("Bright center of the shot.")]
        public Color ShotCoreColor = new Color(1f, 0.92f, 0.75f, 1f);

        public static EnemyTypeSettings CreateDefault(EnemyDefinition def)
        {
            var s = CreateInstance<EnemyTypeSettings>();
            s.name = def.Id + "_Settings";
            s.UnlockTime = def.DefaultUnlockTime;
            s.Weight = def.DefaultWeight;
            return s;
        }
    }
}
