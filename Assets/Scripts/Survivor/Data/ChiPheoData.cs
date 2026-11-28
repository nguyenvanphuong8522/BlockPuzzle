using UnityEngine;

namespace Survivor
{
    // Drunk village tough with green fire bottles: lobs a bottle in a high arc onto the nearest enemy; where it
    // shatters the ground burns for a few seconds and every enemy standing in the fire loses health.
    [CreateAssetMenu(menuName = "Survivor/Companion/Chí Phèo", fileName = "ChiPheo")]
    public sealed class ChiPheoData : CompanionData
    {
        [Header("Chỉ số theo cấp")]
        [Tooltip("Damage per second to each enemy standing in the fire, per level.")]
        public float[] FireDps = { 12, 16, 21, 27, 36 };
        [Tooltip("Radius of the burning patch (world units), per level.")]
        public float[] FireRadius = { 1.2f, 1.35f, 1.5f, 1.7f, 2f };
        [Tooltip("How long a patch burns (seconds), per level.")]
        public float[] FireDuration = { 3f, 3f, 3.5f, 3.5f, 4f };
        [Tooltip("Seconds between throws, per level.")]
        public float[] Cooldown = { 2.4f, 2.2f, 2.0f, 1.8f, 1.6f };
        [Tooltip("Bottles per throw (each at a different nearby enemy), per level.")]
        public int[] Bottles = { 1, 1, 2, 2, 3 };

        [Header("Ném chai")]
        [Tooltip("Only enemies within this distance of him are targeted.")]
        [Min(0.5f)] public float SightRange = 7.5f;
        [Tooltip("Flight speed along the ground (world units per second); far throws take longer.")]
        [Min(0.5f)] public float ThrowSpeed = 8f;
        [Tooltip("Shortest / longest flight time (seconds).")]
        public Vector2 FlightTime = new Vector2(0.45f, 0.9f);
        [Tooltip("Height of the arc (world units).")]
        [Min(0f)] public float ArcHeight = 2.2f;
        [Tooltip("Spin of the bottle in the air (degrees per second).")]
        public float BottleSpin = 720f;
        [Tooltip("Drawn size of the bottle (height, world units).")]
        [Min(0.05f)] public float BottleSize = 0.42f;
        [Tooltip("Damage when the bottle hits (to every enemy in the fire radius), as a part of one second of fire. 0 = none.")]
        [Min(0f)] public float ImpactBurstSeconds = 0.5f;

        [Header("Vùng lửa")]
        [Tooltip("Seconds between two damage ticks (damage per tick = Fire Dps × this).")]
        [Min(0.05f)] public float TickInterval = 0.25f;
        [Tooltip("Flames drawn per patch (more = fuller fire).")]
        [Range(1, 30)] public int Flames = 14;
        [Tooltip("Height of one flame (world units), scaled with the patch radius.")]
        [Min(0.05f)] public float FlameSize = 0.75f;
        [Tooltip("Flame tint (white = the art's own orange).")]
        public Color FlameTint = Color.white;
        [Tooltip("Glow on the ground under the fire.")]
        public Color GroundGlow = new Color(1f, 0.45f, 0.1f, 0.35f);
        [Tooltip("Burnt mark under the fire.")]
        public Color Scorch = new Color(0.15f, 0.08f, 0.05f, 0.35f);
        [Tooltip("Time to fade out at the end (seconds).")]
        [Min(0.01f)] public float FadeTime = 0.5f;

        [Header("Khác")]

        public override CompanionKind Kind => CompanionKind.ChiPheo;
        public override Companion Create() => new ChiPheoCompanion(this);
    }
}
