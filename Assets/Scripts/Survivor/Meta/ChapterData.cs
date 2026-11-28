using UnityEngine;

namespace Survivor
{
    // One chapter of the Home screen (Assets/Resources/Chapters/Chapter<N>.asset, listed in HomeSettings.Chapters).
    // A chapter is the normal run made harder by its numbers, with its own boss order. Chapter N opens once
    // chapter N-1 has been survived. Live-tunable.
    [CreateAssetMenu(menuName = "Survivor/Chapter", fileName = "Chapter")]
    public sealed class ChapterData : ScriptableObject
    {
        [Header("Hiển thị")]
        [Tooltip("Localization key of its name (Localization.csv), e.g. chapter.1.name.")]
        public string NameKey = "chapter.1.name";
        [Tooltip("Picture on the chapter card (e.g. its first boss).")]
        public Sprite Preview;
        [Tooltip("Tint of the chapter card.")]
        public Color CardColor = new Color(0.16f, 0.36f, 0.55f, 1f);

        [Header("Lượt chơi")]
        [Tooltip("Seconds to survive to clear it (a boss due at that moment must be beaten too).")]
        [Min(10f)] public float SurviveSeconds = 600f;
        [Tooltip("Boss order of this chapter. Empty = Resources/Bosses/BossSchedule.asset.")]
        public BossSchedule Bosses;
        [Tooltip("Energy a run costs. 0 = Play Cost in HomeSettings.")]
        [Min(0)] public int EnergyCost;

        [Header("Độ khó")]
        [Tooltip("Every enemy's HP (bosses too), times this.")]
        [Min(0.1f)] public float EnemyHpScale = 1f;
        [Tooltip("Damage the hero takes from anything, times this.")]
        [Min(0.1f)] public float DamageTakenScale = 1f;
        [Tooltip("How many enemies spawn per second, times this.")]
        [Min(0.1f)] public float SpawnRateScale = 1f;

        public string DisplayName => Loc.Get(NameKey, name);
    }
}
