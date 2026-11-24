using UnityEngine;

namespace Survivor
{
    // Debug switches. God mode and display options are saved in PlayerPrefs so they survive restarts.
    // The debug panel only exists in the Editor and in Development builds.
    public sealed class DebugSettings
    {
        const string GodModeKey = "debug.godMode";
        const string StatsKey = "debug.showStats";
        const string DamageNumbersKey = "debug.damageNumbers";
        const string PickupRangeKey = "debug.pickupRange";
        const string CompanionRangeKey = "debug.companionRange";
        const string AttackAreaKey = "debug.attackArea";

        bool godMode = PlayerPrefs.GetInt(GodModeKey, 0) == 1;
        bool showStats = PlayerPrefs.GetInt(StatsKey, 1) == 1;
        bool showDamageNumbers = PlayerPrefs.GetInt(DamageNumbersKey, 1) == 1;
        bool showPickupRange = PlayerPrefs.GetInt(PickupRangeKey, 0) == 1;
        bool showCompanionRange = PlayerPrefs.GetInt(CompanionRangeKey, 0) == 1;
        bool showAttackArea = PlayerPrefs.GetInt(AttackAreaKey, 0) == 1;

        public bool ToolsAvailable => Application.isEditor || Debug.isDebugBuild;

        // Player takes no damage and can never die.
        public bool GodMode
        {
            get => godMode;
            set => Save(GodModeKey, godMode = value);
        }

        public bool ShowStats
        {
            get => showStats;
            set => Save(StatsKey, showStats = value);
        }

        public bool ShowDamageNumbers
        {
            get => showDamageNumbers;
            set => Save(DamageNumbersKey, showDamageNumbers = value);
        }

        // Draws the gem pickup (magnet) radius around the player.
        public bool ShowPickupRange
        {
            get => showPickupRange;
            set => Save(PickupRangeKey, showPickupRange = value);
        }

        // Draws how far each companion looks for enemies (one ring per companion, in its color).
        public bool ShowCompanionRange
        {
            get => showCompanionRange;
            set => Save(CompanionRangeKey, showCompanionRange = value);
        }

        // Draws the area a companion attack hits (the sword otter's 120-degree slash).
        public bool ShowAttackArea
        {
            get => showAttackArea;
            set => Save(AttackAreaKey, showAttackArea = value);
        }

        public float GameSpeed { get; set; } = 1f;

        // Not saved: used by automated runs (benchmark, screenshots) so they never change your settings.
        public bool SessionGodMode { get; set; }

        static void Save(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
