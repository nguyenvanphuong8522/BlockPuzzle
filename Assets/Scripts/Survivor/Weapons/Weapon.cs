using UnityEngine;

namespace Survivor
{
    // Everything a weapon may touch. Registered in VContainer, passed to weapons each frame.
    public sealed class WeaponContext
    {
        public readonly Player Player;
        public readonly EnemySystem Enemies;
        public readonly ProjectileSystem Projectiles;
        public readonly EffectSystem Effects;
        public readonly SpriteCatalog Sprites;
        public readonly AudioService Audio;

        public WeaponContext(Player player, EnemySystem enemies, ProjectileSystem projectiles, EffectSystem effects,
            SpriteCatalog sprites, AudioService audio)
        {
            Player = player;
            Enemies = enemies;
            Projectiles = projectiles;
            Effects = effects;
            Sprites = sprites;
            Audio = audio;
        }

        public float Might => Player.Stats.Might;
        public float Haste => Player.Stats.Haste;
        public float Area => Player.Stats.Area;
    }

    public abstract class Weapon
    {
        public const int MaxLevel = 5;

        public int Level { get; private set; } = 1;
        public bool IsMaxLevel => Level >= MaxLevel;

        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract Color Tint { get; }

        // Text shown on the upgrade card for the next level.
        public abstract string NextLevelDescription { get; }

        public void LevelUp()
        {
            if (!IsMaxLevel) Level++;
        }

        public abstract void Tick(float dt, WeaponContext ctx);

        public virtual void Draw(InstancedRenderer renderer, WeaponContext ctx) { }

        // Value for the current level from a per-level table.
        protected T At<T>(T[] table) => table[Mathf.Clamp(Level - 1, 0, table.Length - 1)];

        protected T Next<T>(T[] table) => table[Mathf.Clamp(Level, 0, table.Length - 1)];
    }
}
