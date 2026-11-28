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
        public readonly CameraShake Shake;
        public readonly ProjectileSystem Projectiles;
        public readonly PlayerTuning Tuning; // live-tunable values (Assets/Resources/PlayerTuning.asset)
        public readonly FloatingTextSystem FloatingText; // texts over the hero's head

        public CompanionContext(Player player, EnemySystem enemies, BoomerangSystem boomerangs, EffectSystem effects,
            AudioService audio, SpriteCatalog sprites, CameraShake shake, ProjectileSystem projectiles,
            PlayerTuning tuning, FloatingTextSystem floatingText)
        {
            Tuning = tuning;
            FloatingText = floatingText;
            Projectiles = projectiles;
            Shake = shake;
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
        public bool IsMaxLevel => Level >= Mathf.Clamp(Data.MaxLevel, 1, MaxLevel); // per companion, set in its asset
        public int Slot { get; internal set; }
        public Vector2 Position { get; protected internal set; }

        // Its place next to the hero, updated every frame by CompanionSystem.
        public Vector2 Home { get; internal set; }

        // True = glued to Home (default). A companion that runs off to attack returns false while it is away.
        public virtual bool StaysHome => true;

        // Card text, color and every number come from this asset (Assets/Resources/Upgrades/Companions).
        public CompanionData Data { get; }

        protected Companion(CompanionData data) => Data = data;

        public string Name => Data.LocalizedName;
        public string Description => Data.LocalizedDescription;
        public Color Tint => Data.Tint;

        // Text on the upgrade card for the next level.
        public string NextLevelDescription => Data.LevelText(Level + 1);

        public void LevelUp()
        {
            if (!IsMaxLevel) Level++;
        }

        public abstract void Tick(float dt, CompanionContext ctx);

        public abstract void Draw(InstancedRenderer renderer, CompanionContext ctx);

        // How far it looks for enemies, and from where. Drawn by the debug option "Hiện tầm nhìn đệ tử" (F3).
        public abstract float SightRadius(CompanionContext ctx);
        public virtual Vector2 SightCenter(CompanionContext ctx) => Position;

        // Debug option "Hiện vùng chém rái cá" (F3): outline of the area its attack hits. Nothing by default.
        public virtual void DrawAttackArea(InstancedRenderer renderer, CompanionContext ctx) { }

        // Value for the current level from a per-level table.
        protected T At<T>(T[] table) => table[Mathf.Clamp(Level - 1, 0, table.Length - 1)];
    }
}
