using UnityEngine;

namespace Survivor
{
    // The player's starting stats. Edit Assets/Resources/PlayerTuning.asset in the Inspector:
    // changes apply immediately, even in Play Mode (upgrades are applied on top of these values).
    [CreateAssetMenu(menuName = "Survivor/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [Min(1f)] public float MaxHp = 100f;
        [Min(0f)] public float MoveSpeed = 4.5f;
        [Tooltip("Gems inside this radius start flying to the player. Turn on \"Hiện tầm hút\" in the debug panel (F3) to see it.")]
        [Min(0f)] public float MagnetRadius = 1.6f;
        [Tooltip("Damage multiplier")]
        [Min(0f)] public float Might = 1f;
        [Tooltip("Cooldown multiplier (lower = faster)")]
        [Min(0.05f)] public float Haste = 1f;
        [Tooltip("Size multiplier for area weapons")]
        [Min(0.1f)] public float Area = 1f;

        [Header("Đệ tử (companions)")]
        [Tooltip("Distance from the hero to each companion. They share a circle: 1 = above the head, 2 = above + below, " +
                 "3 = every 120 degrees, and so on.")]
        [Min(0f)] public float CompanionDistance = 1.8f;
        [Tooltip("Squashes the circle vertically for a top-down look (1 = perfect circle).")]
        [Range(0.3f, 1f)] public float CompanionCircleSquash = 0.85f;
        [Tooltip("How fast companions slide to their new places when one joins (higher = snappier).")]
        [Min(0.1f)] public float CompanionRegroupSpeed = 8f;

        [Header("Dấu hiệu hero")]
        [Tooltip("Size of the blue triangle over the hero's head (world units).")]
        [Min(0f)] public float MarkerSize = 0.3f;
        [Tooltip("Height of the triangle above the hero's center (the head top is at ~0.85).")]
        public float MarkerHeight = 1.02f;
        [Tooltip("Tint for the triangle. White = the original blue art.")]
        public Color MarkerTint = Color.white;

        [Header("Đạn Ếch Ná Thun")]
        [Tooltip("Size of the rubber pellet (world units).")]
        [Min(0.05f)] public float PelletSize = 0.34f;
        [Tooltip("Color of the trail behind the pellet.")]
        public Color PelletTrailColor = new Color(1f, 0.15f, 0.1f, 0.8f);
        [Tooltip("Trail length (world units). 0 = no trail.")]
        [Min(0f)] public float PelletTrailLength = 0.8f;
        [Tooltip("Trail thickness (world units).")]
        [Min(0f)] public float PelletTrailWidth = 0.2f;
        [Tooltip("Bounce radius per level (element 0 = level 1 ... element 4 = level 5): after hitting an enemy, the pellet " +
                 "jumps to the nearest enemy within this distance. Nobody that close around the first target = double damage.")]
        public float[] FrogBounceRadius = { 2.5f, 2.7f, 3.0f, 3.2f, 3.5f };

        [Header("Đẩy lùi quái (knockback)")]
        [Tooltip("Only when the hit does NOT kill. Multiplies every weapon's push (0 = no knockback).")]
        [Min(0f)] public float KnockbackStrength = 2.5f;
        [Tooltip("How fast the push dies out. Lower = enemies slide back further.")]
        [Min(0.5f)] public float KnockbackDecay = 7f;
        [Tooltip("Cap on the push speed, so fast-firing weapons can't launch enemies across the screen.")]
        [Min(0f)] public float KnockbackMaxSpeed = 9f;
        [Tooltip("While pushed faster than this, the enemy stops walking toward the hero (0 = it never stops).")]
        [Min(0f)] public float KnockbackStunSpeed = 2f;

        [Header("Số sát thương")]
        [Tooltip("Height of one digit at scale 1 (world units). Every scale below multiplies this.")]
        [Min(0.05f)] public float NumberBaseHeight = 0.32f;
        [Tooltip("Color of the numbers when you hit a normal enemy.")]
        public Color EnemyNumberColor = Color.white;
        [Min(0.1f)] public float EnemyNumberScale = 1f;
        [Tooltip("Color of the numbers when you hit a boss.")]
        public Color BossNumberColor = new Color(1f, 0.85f, 0.3f);
        [Min(0.1f)] public float BossNumberScale = 1.6f;
        [Tooltip("Color of the number shown when the hero loses HP.")]
        public Color HurtNumberColor = new Color(1f, 0.22f, 0.2f);
        [Min(0.1f)] public float HurtNumberScale = 2f;

        public static PlayerTuning LoadOrDefault()
        {
            var tuning = Resources.Load<PlayerTuning>("PlayerTuning");
            return tuning != null ? tuning : CreateInstance<PlayerTuning>();
        }
    }
}
