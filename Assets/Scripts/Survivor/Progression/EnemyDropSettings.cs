using System;
using UnityEngine;

namespace Survivor
{
    // Every kind of reward an enemy can drop.
    // To add a new one: add a name here, then handle it in RewardDropSystem.Give (one case).
    public enum RewardType
    {
        Gem,     // experience gem; Amount = experience inside it
        Heal,    // heals the hero when walked over
        Magnet,  // pulls every gem on the map to the hero
        Chest,   // opens the reward screen (free level-up choice)
    }

    // What each enemy type drops when it dies. Edit Assets/Resources/EnemyDropSettings.asset in the Inspector:
    // changes apply immediately, even in Play Mode.
    // Each enemy type has a list of rewards; every line is rolled on its own, so one death can drop several things.
    // The list follows GameConfig.Enemies (same order) and re-syncs names and length when enemy types change.
    [CreateAssetMenu(menuName = "Survivor/Enemy Drop Settings", fileName = "EnemyDropSettings")]
    public sealed class EnemyDropSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Reward
        {
            public RewardType Type = RewardType.Gem;
            [Tooltip("Chance in percent: 100 = always, 80 = 8 times out of 10, 0.15 = very rare.")]
            [Range(0f, 100f)] public float ChancePercent = 100f;
            [Tooltip("Gem: experience inside the gem. Other rewards: how many drop.")]
            [Min(1)] public int Amount = 1;
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Enemy type (from GameConfig). Just a label.")]
            public string Name;
            public Reward[] Rewards = new Reward[0];
        }

        public Entry[] Enemies = new Entry[0];

        [Header("Kích thước đồ rơi trên đất")]
        [Tooltip("Height of the drumstick (heal) on the ground, world units. Was 0.6.")]
        [Min(0.1f)] public float HealSize = 0.95f;
        [Tooltip("Height of the magnet on the ground, world units. Was 0.6.")]
        [Min(0.1f)] public float MagnetSize = 0.9f;
        [Tooltip("Height of the chest on the ground, world units.")]
        [Min(0.1f)] public float ChestSize = 0.9f;
        [Tooltip("How far a drop is picked up from (added to the hero's radius).")]
        [Min(0f)] public float PickupRadius = 0.45f;

        public Reward[] RewardsFor(int type) =>
            type >= 0 && type < Enemies.Length && Enemies[type] != null ? Enemies[type].Rewards : Array.Empty<Reward>();

        public static EnemyDropSettings LoadOrDefault()
        {
            var settings = Resources.Load<EnemyDropSettings>("EnemyDropSettings");
            if (settings == null) settings = CreateInstance<EnemyDropSettings>();
            settings.Sync(); // fills types that have no list yet (also in builds, where OnValidate never runs)
            return settings;
        }

        // Matches the list to GameConfig.Enemies, keeping lists already edited. A type with no rewards yet gets
        // the defaults: normal enemies = gem (their xp, 100%), magnet 0.15%, heal 0.4%; bosses = chest + big gem.
        public void Sync()
        {
            var types = new GameConfig().Enemies;
            if (Enemies.Length != types.Length) Array.Resize(ref Enemies, types.Length);
            for (var i = 0; i < types.Length; i++)
            {
                if (Enemies[i] == null) Enemies[i] = new Entry();
                Enemies[i].Name = types[i].Name;
                if (Enemies[i].Rewards == null || Enemies[i].Rewards.Length == 0)
                    Enemies[i].Rewards = Defaults(types[i]);
            }
        }

        static Reward[] Defaults(EnemyDefinition type) => type.IsBoss
            ? new[]
            {
                new Reward { Type = RewardType.Chest, ChancePercent = 100f, Amount = 1 },
                new Reward { Type = RewardType.Gem, ChancePercent = 100f, Amount = type.Xp },
            }
            : new[]
            {
                new Reward { Type = RewardType.Gem, ChancePercent = 100f, Amount = type.Xp },
                new Reward { Type = RewardType.Magnet, ChancePercent = 0.15f, Amount = 1 },
                new Reward { Type = RewardType.Heal, ChancePercent = 0.4f, Amount = 1 },
            };

#if UNITY_EDITOR
        void OnValidate() => Sync();
#endif
    }
}
