using UnityEngine;

namespace Survivor
{
    [CreateAssetMenu(menuName = "Survivor/Companion/Lợn Cầm Trùy", fileName = "PigMace")]
    public sealed class PigMaceData : MeleeCompanionData
    {
        [Header("Buff cho hero")]
        [Tooltip("Every this many seconds the pig gives the hero a random bonus (+ATK or +Max HP).")]
        [Min(0.5f)] public float BuffInterval = 18f;
        [Tooltip("It goes back next to the hero and raises its club for this long (seconds) before the gift. No attacking meanwhile.")]
        [Min(0f)] public float BuffCastTime = 0.8f;
        [Tooltip("Chance (%) that the gift is ATK; otherwise it is Max HP.")]
        [Range(0f, 100f)] public float AtkChancePercent = 50f;
        [Tooltip("ATK per gift, per level. 0.05 = +5% damage for the hero AND all companions.")]
        public float[] AtkBonus = { 0.04f, 0.05f, 0.06f, 0.07f, 0.08f };
        [Tooltip("Max HP per gift, per level (the hero is also healed that much).")]
        public float[] MaxHpBonus = { 8f, 10f, 12f, 14f, 16f };
        public Color AtkColor = new Color(1f, 0.45f, 0.2f);
        public Color HpColor = new Color(0.35f, 0.95f, 0.45f);
        [Tooltip("Text over the hero's head for the ATK gift. {0} = the number.")]
        public string AtkText = "buff.atk";
        [Tooltip("Text over the hero's head for the Max HP gift. {0} = the number.")]
        public string HpText = "buff.hp";

        [Header("Thanh đếm giờ buff")]
        [Tooltip("Countdown bar under the pig (world units). Width 0 = hidden.")]
        [Min(0f)] public float BarWidth = 0.75f;
        [Min(0.02f)] public float BarHeight = 0.09f;
        [Tooltip("Height of the bar relative to the pig's feet (negative = below the feet).")]
        public float BarOffsetY = -0.12f;
        [Min(0f)] public float BarOutline = 0.02f;
        public Color BarColor = new Color(1f, 0.8f, 0.25f);
        public Color BarBackground = new Color(0.15f, 0.15f, 0.15f, 0.85f);

        public override CompanionKind Kind => CompanionKind.PigMace;
        public override Companion Create() => new PigMaceCompanion(this);
    }
}
