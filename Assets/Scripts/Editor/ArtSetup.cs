using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Survivor;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

// Menu "Survivor/Setup Art & Audio": configures import settings, builds the sprite atlas and fills
// SpriteCatalog / AudioCatalog from file names. Run it again after adding or replacing art.
//
// File naming:
//   Assets/Art/Sprites/player_0.png, player_1.png ...          player animation frames
//   Assets/Art/Sprites/enemy3_starfish_0.png ...               enemy type 3 (GameConfig.Enemies order)
//   Assets/Art/Sprites/weapon_cannonball.png, weapon_anchor.png
//   Assets/Art/Sprites/pickup_pearl / chest / meat / magnet.png
//   Assets/Art/Sprites/deco_*.png                              beach props
//   Assets/Art/Ground/ground_sand.png                          seamless ground tile
//   Assets/Audio/sfx_<name>.wav (sfx_enemy_die.wav = Sfx.EnemyDie), music_*.wav
public static class ArtSetup
{
    const string SpritesDir = "Assets/Art/Sprites";
    const string GroundPath = "Assets/Art/Ground/ground_sand.png";
    const string AudioDir = "Assets/Audio";
    const string AtlasPath = "Assets/Art/GameSprites.spriteatlasv2";
    const string SpriteCatalogPath = "Assets/Resources/SpriteCatalog.asset";
    const string AudioCatalogPath = "Assets/Resources/AudioCatalog.asset";
    const string GemPickupSettingsPath = "Assets/Resources/GemPickupSettings.asset";
    const string PlayerTuningPath = "Assets/Resources/PlayerTuning.asset";
    const string GunSettingsPath = "Assets/Resources/GunSettings.asset";

    static readonly (string name, float height)[] Decorations =
    {
        ("deco_shell", 0.5f), ("deco_shell", 0.5f), ("deco_rock", 1.1f), ("deco_rock", 1.1f), ("deco_palm", 3.4f)
    };

    [MenuItem("Survivor/Setup Art & Audio")]
    public static void Run()
    {
        AssetDatabase.Refresh();
        ConfigureSprites();
        ConfigureGround();
        ConfigureAudio();
        CreateAtlas();
        FillSpriteCatalog();
        FillAudioCatalog();
        LoadOrCreate<GemPickupSettings>(GemPickupSettingsPath); // created once with defaults, never overwritten
        LoadOrCreate<PlayerTuning>(PlayerTuningPath);
        LoadOrCreate<GunSettings>(GunSettingsPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Survivor: art & audio setup done.");
    }

    static IEnumerable<string> Files(string dir, string pattern) =>
        Directory.Exists(dir)
            ? Directory.GetFiles(dir, pattern).Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal)
            : Enumerable.Empty<string>();

    static void ConfigureSprites()
    {
        foreach (var path in Files(SpritesDir, "*.png"))
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }

    static void ConfigureGround()
    {
        if (!(AssetImporter.GetAtPath(GroundPath) is TextureImporter importer)) return;
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }

    static void ConfigureAudio()
    {
        foreach (var path in Files(AudioDir, "*.wav"))
        {
            if (!(AssetImporter.GetAtPath(path) is AudioImporter importer)) continue;
            var settings = importer.defaultSampleSettings;
            var isMusic = Path.GetFileName(path).StartsWith("music", StringComparison.Ordinal);
            settings.loadType = isMusic ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.6f : 0.8f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.SaveAndReimport();
        }
    }

    // One atlas for all sprites: they share one texture, so the renderer keeps them in a few draw calls.
    static void CreateAtlas()
    {
        if (!File.Exists(AtlasPath))
        {
            var atlas = new SpriteAtlasAsset();
            atlas.Add(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<DefaultAsset>(SpritesDir) });
            SpriteAtlasAsset.Save(atlas, AtlasPath);
            AssetDatabase.Refresh();
        }

        if (!(AssetImporter.GetAtPath(AtlasPath) is SpriteAtlasImporter importer)) return;
        importer.packingSettings = new SpriteAtlasPackingSettings
        {
            blockOffset = 1, padding = 4, enableRotation = false, enableTightPacking = false
        };
        importer.textureSettings = new SpriteAtlasTextureSettings
        {
            readable = false, generateMipMaps = false, sRGB = true, filterMode = FilterMode.Bilinear
        };
        importer.includeInBuild = true;
        importer.SaveAndReimport();
    }

    static void FillSpriteCatalog()
    {
        var catalog = LoadOrCreate<SpriteCatalog>(SpriteCatalogPath);
        var enemyCount = new GameConfig().Enemies.Length;

        catalog.Player = new SpriteAnim { Frames = Frames("player_"), Fps = 8f };
        catalog.Enemies = new SpriteAnim[enemyCount];
        for (var i = 0; i < enemyCount; i++)
            catalog.Enemies[i] = new SpriteAnim { Frames = Frames($"enemy{i}_"), Fps = 5f };

        catalog.Gun = Single("weapon_gun");
        catalog.MuzzleFlash = new SpriteAnim { Frames = Frames("fx_muzzle_"), Fps = 30f };
        catalog.MagicBolt = Single("weapon_cannonball");
        catalog.OrbitBlade = Single("weapon_anchor");
        catalog.Gem = Single("pickup_pearl");
        catalog.Chest = Single("pickup_chest");
        catalog.Heal = Single("pickup_meat");
        catalog.Magnet = Single("pickup_magnet");

        catalog.GroundTile = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundPath);
        catalog.GroundTileSize = 6f;
        catalog.Decorations = Decorations.Select(d => Single(d.name)).ToArray();
        catalog.DecorationHeights = Decorations.Select(d => d.height).ToArray();
        catalog.SpriteScale = 1.5f;

        EditorUtility.SetDirty(catalog);
    }

    static void FillAudioCatalog()
    {
        var catalog = LoadOrCreate<AudioCatalog>(AudioCatalogPath);
        var old = (catalog.Sounds ?? new AudioCatalog.Entry[0]).Where(e => e != null).ToDictionary(e => e.Id, e => e.Volume);

        var entries = new List<AudioCatalog.Entry>();
        foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
        {
            var file = $"{AudioDir}/sfx_{SnakeCase(id.ToString())}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file);
            if (clip == null) continue;
            entries.Add(new AudioCatalog.Entry
            {
                Id = id, Clip = clip,
                Volume = old.TryGetValue(id, out var v) ? v : DefaultVolume(id) // keep volumes you tuned
            });
        }
        catalog.Sounds = entries.ToArray();
        catalog.Music = Files(AudioDir, "music_*.wav").Select(AssetDatabase.LoadAssetAtPath<AudioClip>).FirstOrDefault();
        EditorUtility.SetDirty(catalog);
    }

    static float DefaultVolume(Sfx id) => id switch
    {
        Sfx.Hit => 0.45f,
        Sfx.Gem => 0.8f,
        Sfx.EnemyDie => 0.55f,
        Sfx.Shoot => 0.5f,
        _ => 0.8f
    };

    static string SnakeCase(string name) => Regex.Replace(name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();

    static Sprite[] Frames(string prefix) =>
        Files(SpritesDir, prefix + "*.png").Select(AssetDatabase.LoadAssetAtPath<Sprite>).Where(s => s != null).ToArray();

    static Sprite Single(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/{name}.png");

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
