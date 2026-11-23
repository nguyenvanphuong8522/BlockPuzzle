using UnityEditor;
using UnityEngine;

// Makes new art and sounds work as soon as they land in the project, no menu needed:
//   - PNGs in Assets/Art/Sprites import as single sprites (Unity's 2D default is "Multiple", which has no sprite
//     until you slice it), the ground tile imports as a repeating texture.
//   - After art/audio files are added, changed or deleted, SpriteCatalog and AudioCatalog are re-linked by file name.
// "Survivor/Setup Art & Audio" still exists for a full refresh (atlas, audio compression...).
public sealed class ArtAutoSetup : AssetPostprocessor
{
    const string SpritesDir = "Assets/Art/Sprites/";
    const string GroundDir = "Assets/Art/Ground/";
    static bool refreshQueued;

    void OnPreprocessTexture()
    {
        var importer = (TextureImporter)assetImporter;
        if (assetPath.StartsWith(SpritesDir))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
        else if (assetPath == "Assets/Resources/DigitFont.png")
        {
            // Damage number font: crisp, no compression artifacts on the outline.
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
        else if (assetPath.StartsWith(GroundDir))
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
        }
    }

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (refreshQueued || Application.isBatchMode) return;
        if (!Touches(imported) && !Touches(deleted) && !Touches(moved)) return;

        // Wait until the import finished, then re-link the catalogs once.
        refreshQueued = true;
        EditorApplication.delayCall += () =>
        {
            refreshQueued = false;
            ArtSetup.RefreshCatalogs();
            Debug.Log("Survivor: art / audio changed, catalogs refreshed.");
        };
    }

    static bool Touches(string[] paths)
    {
        foreach (var p in paths)
            if (p.StartsWith("Assets/Art/") || p.StartsWith("Assets/Audio/")) return true;
        return false;
    }
}
