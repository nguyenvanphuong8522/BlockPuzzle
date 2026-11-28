using UnityEngine;

namespace Survivor
{
    // What an item raises.
    public enum ItemStat
    {
        [InspectorName("Tốc độ chạy (%)")] MoveSpeed,
        [InspectorName("Tầm hút kinh nghiệm (%)")] MagnetRange,
        [InspectorName("Máu tối đa (+ hồi ngay)")] MaxHp,
        [InspectorName("Sát thương ATK (%)")] Attack,
        [InspectorName("Hồi máu mỗi giây")] HpRegen,
        [InspectorName("Giảm sát thương nhận vào (%)")] DamageReduction,
    }

    // One asset per item (Assets/Resources/Upgrades/Items). Duplicate one to make a new item with the same kind
    // of bonus (e.g. a stronger boot). Changes apply immediately, even in Play Mode.
    [CreateAssetMenu(menuName = "Survivor/Item", fileName = "Item")]
    public sealed class ItemData : ScriptableObject
    {
        [Header("Thẻ chọn")]
        [Tooltip("Off = never offered on the item cards.")]
        public bool Enabled = true;
        public string DisplayName = "Đồ";
        [Tooltip("Card text. {0} = the amount per pick (shown as a percent for % stats), {1} = the cap (Giảm sát thương only).")]
        [TextArea(2, 4)] public string Description = "";
        public Sprite Icon;
        public Color Tint = Color.white;
        [Tooltip("Order on the list (lower first). Only matters for debug tools; the cards are shuffled.")]
        public int SortOrder;

        [Header("Hiệu quả")]
        public ItemStat Stat;
        [Tooltip("Bonus per pick. % stats use fractions: 0.08 = +8%. Máu tối đa / Hồi máu use plain numbers.")]
        public float AmountPerPick = 0.1f;
        [Tooltip("How many times it can be picked.")]
        [Range(1, 20)] public int MaxPicks = 5;
        [Tooltip("Giảm sát thương only: the total reduction never goes above this (0.3 = at most -30%).")]
        [Range(0f, 0.95f)] public float Cap = 0.3f;

        public bool IsPercent => Stat == ItemStat.MoveSpeed || Stat == ItemStat.MagnetRange ||
                                 Stat == ItemStat.Attack || Stat == ItemStat.DamageReduction;

        // Translated texts: Localization.csv "item.<asset name>.name / .desc"; the fields above when missing.
        public string LocalizedName => Loc.Get($"item.{name}.name", DisplayName);
        string LocalizedDescriptionPattern => Loc.Get($"item.{name}.desc", Description);

        public string FormatDescription()
        {
            var amount = IsPercent ? (AmountPerPick * 100f).ToString("0.#") : AmountPerPick.ToString("0.##");
            var cap = (Cap * 100f).ToString("0");
            var pattern = LocalizedDescriptionPattern;
            try { return string.Format(pattern ?? "", amount, cap); }
            catch (System.FormatException) { return pattern; }
        }

        // Gives the bonus to the hero.
        public void Apply(Player player)
        {
            var stats = player.Stats;
            switch (Stat)
            {
                case ItemStat.MoveSpeed: stats.MoveSpeedMultiplier += AmountPerPick; break;
                case ItemStat.MagnetRange: stats.MagnetMultiplier += AmountPerPick; break;
                case ItemStat.MaxHp: player.IncreaseMaxHp(AmountPerPick); break;
                case ItemStat.Attack: stats.BonusMight += AmountPerPick; break;
                case ItemStat.HpRegen: stats.HpRegen += AmountPerPick; break;
                case ItemStat.DamageReduction:
                    stats.DamageReduction += AmountPerPick;
                    stats.DamageReductionCap = Mathf.Max(stats.DamageReductionCap, Cap);
                    break;
            }
        }
    }
}
