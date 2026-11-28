using UnityEngine;

namespace Survivor
{
    // Blood splashes (BloodSystem): Assets/Resources/BloodSettings.asset, live-tunable in Play mode.
    // Enemies bleed when hit (their color: their type's Blood Color, or their body color), the hero bleeds when he
    // gets hurt (enemy touch or shot). Droplets fly out, fall to the ground, and some leave a stain that fades.
    [CreateAssetMenu(menuName = "Survivor/Blood Settings", fileName = "BloodSettings")]
    public sealed class BloodSettings : ScriptableObject
    {
        [Header("Bật / tắt")]
        public bool EnemyBlood = true;
        public bool HeroBlood = true;

        [Header("Kẻ địch trúng đòn")]
        [Tooltip("Droplets per hit (a bigger enemy throws a bit more and bigger ones).")]
        [Range(0, 20)] public int EnemyDroplets = 4;
        [Tooltip("Blood color of every enemy (a type can have its own: <Id>_Settings, Own Blood Color).")]
        public Color EnemyColor = new Color(0.8f, 0.04f, 0.06f, 1f);
        [Tooltip("On = enemies bleed their body color instead (darkened by Enemy Color Darken).")]
        public bool UseBodyColor;
        [Range(0f, 1f)] public float EnemyColorDarken = 0.25f;
        [Tooltip("Cone (degrees) the droplets fly in, centered on the push direction of the hit. 360 = all around.")]
        [Range(0f, 360f)] public float EnemySprayAngle = 110f;

        [Header("Hero bị thương")]
        [Range(0, 40)] public int HeroDroplets = 12;
        public Color HeroColor = new Color(0.85f, 0.05f, 0.08f, 1f);
        [Tooltip("Size of the hero's droplets compared to an enemy's.")]
        [Min(0.1f)] public float HeroSizeScale = 1.2f;

        [Header("Giọt máu")]
        [Tooltip("Droplet radius (world units), each one ± 40%.")]
        [Min(0.01f)] public float DropletSize = 0.07f;
        [Tooltip("Speed they fly out at (world units per second), each one 40-100% of it.")]
        [Min(0f)] public float Speed = 4.5f;
        [Tooltip("Upward kick (they arc up, then fall back to the ground) and how fast they fall.")]
        [Min(0f)] public float Lift = 3f;
        [Min(0.1f)] public float Gravity = 18f;
        [Tooltip("Height above the ground they start at (world units, times the body size).")]
        [Min(0f)] public float StartHeight = 0.25f;
        [Tooltip("Slows them down in the air (0 = no drag).")]
        [Min(0f)] public float Drag = 2f;

        [Header("Vết máu trên đất")]
        [Tooltip("Chance a landing droplet leaves a stain (0..1).")]
        [Range(0f, 1f)] public float StainChance = 0.5f;
        [Tooltip("Stain size compared to the droplet.")]
        [Min(0.1f)] public float StainScale = 1.8f;
        [Tooltip("Seconds a stain stays (it fades out over the last half).")]
        [Min(0.1f)] public float StainLifetime = 2.5f;
        [Tooltip("Stain opacity at first.")]
        [Range(0f, 1f)] public float StainAlpha = 0.7f;

        [Header("Giới hạn")]
        [Tooltip("Most droplets / stains at once (new ones are skipped when full).")]
        [Min(16)] public int MaxDroplets = 700;
        [Min(16)] public int MaxStains = 400;

        public static BloodSettings LoadOrDefault()
        {
            var settings = Resources.Load<BloodSettings>("BloodSettings");
            return settings != null ? settings : CreateInstance<BloodSettings>();
        }
    }
}
