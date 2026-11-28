using UnityEngine;

namespace Survivor
{
    // Monk sitting cross-legged, tapping a wooden fish drum: every tap sends a ring wave out from the hero.
    // Enemies the edge of the ring sweeps over take damage (once per ring).
    [CreateAssetMenu(menuName = "Survivor/Companion/Sư Thầy Gõ Mõ", fileName = "Monk")]
    public sealed class MonkData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        [Tooltip("Damage of one ring, per level.")]
        public float[] Damage = { 10, 13, 17, 22, 30 };
        [Tooltip("How big a ring grows before it fades (radius, world units), per level.")]
        public float[] MaxRadius = { 3.5f, 4f, 4.5f, 5f, 6f };

        [Header("Vòng sóng")]
        [Tooltip("Seconds between two rings (one tap of the wooden fish).")]
        [Min(0.1f)] public float PulseInterval = 1f;
        [Tooltip("How fast a ring grows (world units per second).")]
        [Min(0.1f)] public float ExpandSpeed = 5f;
        [Tooltip("Radius a ring starts from.")]
        [Min(0f)] public float StartRadius = 0.4f;
        [Tooltip("Enemies this close to the ring's edge (inside or outside) are hit.")]
        [Min(0.05f)] public float HitWidth = 0.35f;
        [Tooltip("How hard a ring pushes enemies outward.")]
        [Min(0f)] public float Knockback = 1.5f;
        [Tooltip("On = rings stay centered on the hero as he moves. Off = each ring stays where it started.")]
        public bool FollowHero = true;
        [Tooltip("Only tap (send rings) when an enemy is within Max Radius of the hero.")]
        public bool OnlyWhenEnemies = true;

        [Header("Hình vòng")]
        public Color RingColor = new Color(1f, 0.85f, 0.35f, 0.95f);
        [Tooltip("Thickness of the bright edge (world units).")]
        [Min(0.01f)] public float RingThickness = 0.1f;
        [Tooltip("Soft glow drawn around the edge. Alpha 0 = no glow.")]
        public Color GlowColor = new Color(1f, 0.75f, 0.25f, 0.25f);
        [Min(0f)] public float GlowThickness = 0.45f;
        [Tooltip("Fade out over this last part of the growth (0..1). 0.4 = fades in the last 40%.")]
        [Range(0.05f, 1f)] public float FadePart = 0.45f;

        [Header("Khác")]
        [Tooltip("Pitch of the wooden fish \"tok\".")]
        [Range(0.3f, 3f)] public float TapPitch = 1.6f;
        [Range(0f, 1f)] public float TapVolume = 0.6f;

        public override CompanionKind Kind => CompanionKind.Monk;
        public override Companion Create() => new MonkCompanion(this);
    }
}
