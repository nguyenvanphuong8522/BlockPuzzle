using UnityEngine;

namespace Survivor
{
    // One enemy type (GameConfig.Enemies). The numbers below are the defaults written in code; in the game every
    // normal type reads them live from its own two assets (GameConfig links them):
    //   Assets/Resources/Enemies/<Id>/<Id>_Stats.asset    - Hp, Speed, Damage, Radius, elite multipliers, shot damage
    //   Assets/Resources/Enemies/<Id>/<Id>_Settings.asset - when / how often it appears, knockback, ranged behaviour
    // The boss has BossStats / BossSettings instead.
    public sealed class EnemyDefinition
    {
        public readonly string Id;       // file / folder name of its assets
        public readonly string Name;
        public readonly int Xp;
        public readonly Color Color;
        public readonly bool IsBoss;
        public readonly bool IsRanged;   // keeps its distance and shoots (its Settings "Bắn xa")

        readonly float hp, speed, damage, radius, unlockTime, weight;
        EnemyTypeStats stats;
        EnemyTypeSettings settings;

        // Always available: the linked asset, or the code defaults.
        public EnemyTypeStats Stats => stats != null ? stats : stats = EnemyTypeStats.CreateDefault(this);
        public EnemyTypeSettings Settings => settings != null ? settings : settings = EnemyTypeSettings.CreateDefault(this);

        public float Hp => stats != null ? stats.Hp : hp;
        public float Speed => stats != null ? stats.Speed : speed;
        public float Damage => stats != null ? stats.Damage : damage;
        public float Radius => stats != null ? stats.Radius : radius;
        public float UnlockTime => settings != null ? settings.UnlockTime : unlockTime; // seconds before it appears
        public float Weight => settings != null ? settings.Weight : weight;             // relative spawn chance

        // Code defaults, used to create the assets.
        public float DefaultHp => hp;
        public float DefaultSpeed => speed;
        public float DefaultDamage => damage;
        public float DefaultRadius => radius;
        public float DefaultUnlockTime => unlockTime;
        public float DefaultWeight => weight;

        public EnemyDefinition(string id, string name, float hp, float speed, float damage, float radius, int xp,
            Color color, float unlockTime, float weight, bool isBoss = false, bool ranged = false)
        {
            Id = id;
            Name = name;
            this.hp = hp;
            this.speed = speed;
            this.damage = damage;
            this.radius = radius;
            Xp = xp;
            Color = color;
            this.unlockTime = unlockTime;
            this.weight = weight;
            IsBoss = isBoss;
            IsRanged = ranged;
        }

        public static string StatsPath(string id) => $"Enemies/{id}/{id}_Stats";
        public static string SettingsPath(string id) => $"Enemies/{id}/{id}_Settings";

        // Loads this type's two assets from Resources (missing ones fall back to the code defaults).
        public void LoadAssets()
        {
            stats = Resources.Load<EnemyTypeStats>(StatsPath(Id));
            settings = Resources.Load<EnemyTypeSettings>(SettingsPath(Id));
        }
    }
}
