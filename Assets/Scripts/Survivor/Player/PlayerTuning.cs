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

        public static PlayerTuning LoadOrDefault()
        {
            var tuning = Resources.Load<PlayerTuning>("PlayerTuning");
            return tuning != null ? tuning : CreateInstance<PlayerTuning>();
        }
    }
}
