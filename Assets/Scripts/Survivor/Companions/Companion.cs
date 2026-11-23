using UnityEngine;

namespace Survivor
{
    // Everything a companion may use. Registered in VContainer.
    public sealed class CompanionContext
    {
        public readonly Player Player;
        public readonly EnemySystem Enemies;
        public readonly BoomerangSystem Boomerangs;
        public readonly EffectSystem Effects;
        public readonly AudioService Audio;
        public readonly SpriteCatalog Sprites;

        public CompanionContext(Player player, EnemySystem enemies, BoomerangSystem boomerangs, EffectSystem effects,
            AudioService audio, SpriteCatalog sprites)
        {
            Player = player;
            Enemies = enemies;
            Boomerangs = boomerangs;
            Effects = effects;
            Audio = audio;
            Sprites = sprites;
        }
    }

    // A small hero that stands in one of the 6 slots around the player and attacks on its own.
    // Level-ups recruit new companions or level up the ones you have.
    public abstract class Companion
    {
        public const int MaxLevel = 5;

        public int Level { get; private set; } = 1;
        public bool IsMaxLevel => Level >= MaxLevel;
        public int Slot { get; internal set; }
        public Vector2 Position { get; internal set; }

        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract Color Tint { get; }

        // Text on the upgrade card for the next level.
        public abstract string NextLevelDescription { get; }

        public void LevelUp()
        {
            if (!IsMaxLevel) Level++;
        }

        public abstract void Tick(float dt, CompanionContext ctx);

        public abstract void Draw(InstancedRenderer renderer, CompanionContext ctx);

        // Value for the current level from a per-level table.
        protected T At<T>(T[] table) => table[Mathf.Clamp(Level - 1, 0, table.Length - 1)];
    }
}
