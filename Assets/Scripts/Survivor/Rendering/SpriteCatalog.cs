using System;
using UnityEngine;

namespace Survivor
{
    // A looping frame animation. One frame = a still sprite.
    [Serializable]
    public sealed class SpriteAnim
    {
        public Sprite[] Frames = new Sprite[0];
        public float Fps = 6f;

        public bool IsEmpty => Frames == null || Frames.Length == 0 || Frames[0] == null;

        // offset desyncs units of the same type so a crowd doesn't move in lockstep.
        public Sprite Frame(float time, int offset = 0)
        {
            var i = (int)(time * Fps) + offset;
            return Frames[(i % Frames.Length + Frames.Length) % Frames.Length];
        }
    }

    // Where the art is plugged in. Edit Assets/Resources/SpriteCatalog.asset in the Inspector,
    // or run the menu "Survivor/Setup Art & Audio" to fill it from Assets/Art by file name.
    // Any empty slot falls back to the built-in colored shape.
    [CreateAssetMenu(menuName = "Survivor/Sprite Catalog", fileName = "SpriteCatalog")]
    public sealed class SpriteCatalog : ScriptableObject
    {
        [Header("Characters")]
        public SpriteAnim Player = new SpriteAnim();
        [Tooltip("Blue upside-down triangle floating over the hero, so you can tell them apart from companions")]
        public Sprite HeroMarker;
        [Tooltip("Standing still: frames 0-3 loop (breathing), last frame = blink")]
        public SpriteAnim PlayerIdle = new SpriteAnim();
        [Tooltip("Same order as GameConfig.Enemies")]
        public SpriteAnim[] Enemies = new SpriteAnim[0];

        [Header("Weapons")]
        public Sprite Gun;
        public SpriteAnim MuzzleFlash = new SpriteAnim();
        public Sprite MagicBolt;
        public Sprite OrbitBlade;

        [Header("Companions")]
        public SpriteAnim Monkey = new SpriteAnim();
        public Sprite Banana;
        [Tooltip("Frames 0-3 idle, 4 dash, 5 slash, 6 blink")]
        public SpriteAnim Otter = new SpriteAnim();
        public SpriteAnim Pig = new SpriteAnim();
        [Tooltip("Monkey king: 0-3 idle, 4 throw, 5 blink, 6 waiting for the staff")]
        public SpriteAnim Wukong = new SpriteAnim();
        [Tooltip("Pheasant plume on the monkey king's crown (base at the left edge), swayed by the game")]
        public Sprite Plume;
        public Sprite Staff;
        [Tooltip("Level stars on the upgrade cards (white, tinted by the UI)")]
        public Sprite Star;
        [Tooltip("Bear hunter: 0-3 idle, 4 blink, 5 reloading (the rifle is drawn separately)")]
        public SpriteAnim Bear = new SpriteAnim();
        public Sprite Rifle;
        [Tooltip("Cowboy: 0-3 idle, 4 blink (the gatling is drawn separately)")]
        public SpriteAnim Cowboy = new SpriteAnim();
        [Tooltip("Six-barrel gatling, frames cycled to spin the barrels")]
        public SpriteAnim Gatling = new SpriteAnim();
        [Tooltip("Monk: 0 chanting, 1 mouth closed, 2 striking the wooden fish")]
        public SpriteAnim Monk = new SpriteAnim();
        [Tooltip("Drunkard: 0-3 drunken wobble, 4 throw, 5 hiccup")]
        public SpriteAnim Drunk = new SpriteAnim();
        public Sprite FireBottle;
        [Tooltip("Flame (base at the bottom), frames cycled to flicker")]
        public SpriteAnim Fire = new SpriteAnim();
        [Tooltip("120-degree slash arc pointing right: swing start, full arc, fading")]
        public SpriteAnim Slash = new SpriteAnim();
        [Tooltip("Frames 0-3 idle, 4 cast, 5 blink")]
        public SpriteAnim Turtle = new SpriteAnim();
        public SpriteAnim MagicOrb = new SpriteAnim();
        [Tooltip("Magic explosion: flash, burst, puffs, fading ring")]
        public SpriteAnim Blast = new SpriteAnim();
        [Tooltip("Frames 0-3 idle, 4 blink (the gun is drawn separately)")]
        public SpriteAnim Gunner = new SpriteAnim();
        public Sprite Smg;
        [Tooltip("Frames 0-3 idle, 4 shot, 5 blink")]
        public SpriteAnim Frog = new SpriteAnim();
        public Sprite Pellet;
        [Tooltip("Boss arena wall: one barbed-wire fence segment (post on the left), repeated along each side")]
        public Sprite BarbedFence;
        [Tooltip("Heal effect: plus sign that floats up around the hero")]
        public Sprite HealPlus;
        // Level-up card icons (items) and the round backdrop behind every card icon.
        public Sprite ItemShoes, ItemMagnet, ItemHeart, ItemSword, ItemRegen, ItemArmor, IconRing;

        [Header("Pickups")]
        public Sprite Gem;
        public Sprite Chest;
        public Sprite Heal;
        public Sprite Magnet;

        [Header("World")]
        public Texture2D GroundTile;
        public float GroundTileSize = 6f;
        public Sprite[] Decorations = new Sprite[0];
        [Tooltip("World height of each decoration, same order as Decorations")]
        public float[] DecorationHeights = new float[0];

        [Header("Size")]
        [Tooltip("Sprite height relative to the unit's collision diameter.")]
        public float SpriteScale = 1.5f;

        public SpriteAnim Enemy(int type) =>
            Enemies != null && type < Enemies.Length && Enemies[type] != null && !Enemies[type].IsEmpty ? Enemies[type] : null;

        public static SpriteCatalog LoadOrEmpty()
        {
            var catalog = Resources.Load<SpriteCatalog>("SpriteCatalog");
            return catalog != null ? catalog : CreateInstance<SpriteCatalog>();
        }
    }
}
