using System.Collections.Generic;
using Survivor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Menu "Survivor/Setup Level-Up Card Stars": adds to every card of LevelUpPopup.prefab
//  - a row of 5 level stars under the picture,
//  - the fusion recipe strip at the bottom (picture + 5 stars) + (picture + 5 stars) = fusion picture,
// and makes the description fill the space between them. Cards that already have them are left alone,
// so after moving things around in the prefab it is safe to run again.
public static class LevelUpCardSetup
{
    const string PrefabPath = "Assets/Prefabs/UI/LevelUpPopup.prefab";
    const int StarCount = 5;

    [MenuItem("Survivor/Setup Level-Up Card Stars")]
    public static void Run()
    {
        var sprites = Resources.Load<SpriteCatalog>("SpriteCatalog");
        var star = sprites ? sprites.Star : null;
        var ring = sprites ? sprites.IconRing : null;
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var done = 0;
        foreach (var card in root.GetComponentsInChildren<UpgradeCardView>(true))
            if (Setup(card, star, ring)) done++;
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"Level-up cards: added stars / recipe to {done} card(s).");
    }

    static bool Setup(UpgradeCardView card, Sprite star, Sprite ring)
    {
        var t = card.transform;
        if (t.Find("Stars") != null) return false;
        var so = new SerializedObject(card);
        var title = (TextMeshProUGUI)so.FindProperty("title").objectReferenceValue;
        var description = (TextMeshProUGUI)so.FindProperty("description").objectReferenceValue;

        // ---- Level stars under the picture ----
        var row = Rect("Stars", t, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -345f), new Vector2(200f, 34f));
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var starImages = new List<Image>();
        for (var i = 0; i < StarCount; i++)
            starImages.Add(ImageAt("Star" + (i + 1), row, Vector2.zero, new Vector2(34f, 34f), star));
        SetArray(so.FindProperty("stars"), starImages);

        // ---- Description: stretch between the stars and the bottom (the card moves its bottom edge) ----
        if (description != null)
        {
            var d = description.rectTransform;
            d.anchorMin = new Vector2(0f, 0f);
            d.anchorMax = new Vector2(1f, 1f);
            d.pivot = new Vector2(0.5f, 0.5f);
            d.offsetMax = new Vector2(-15f, -368f);
            d.offsetMin = new Vector2(15f, 10f);
            description.verticalAlignment = VerticalAlignmentOptions.Middle;
        }

        // ---- Fusion recipe strip at the bottom ----
        var recipe = Rect("Recipe", t, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(262f, 96f));
        recipe.pivot = new Vector2(0.5f, 0f);
        recipe.anchoredPosition = new Vector2(0f, 10f);
        var bg = recipe.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.28f);
        bg.raycastTarget = false;

        var parts = new List<Image>();
        var smallStars = new List<Image>();
        float[] partX = { -92f, -2f };
        for (var p = 0; p < 2; p++)
        {
            parts.Add(ImageAt("Part" + (p + 1), recipe, new Vector2(partX[p], 14f), new Vector2(58f, 58f), null));
            for (var s = 0; s < StarCount; s++)
                smallStars.Add(ImageAt($"Part{p + 1}Star{s + 1}", recipe,
                    new Vector2(partX[p] + (s - 2) * 13f, -31f), new Vector2(13f, 13f), star));
        }
        Label("Plus", recipe, new Vector2(-47f, 8f), "+", title);
        Label("Equals", recipe, new Vector2(42f, 8f), "=", title);
        var glow = ImageAt("ResultRing", recipe, new Vector2(92f, 0f), new Vector2(84f, 84f), ring);
        glow.color = new Color(1f, 0.78f, 0.25f);
        var result = ImageAt("Result", recipe, new Vector2(92f, 0f), new Vector2(72f, 72f), null);

        so.FindProperty("recipeRoot").objectReferenceValue = recipe.gameObject;
        SetArray(so.FindProperty("recipeParts"), parts);
        SetArray(so.FindProperty("recipeStars"), smallStars);
        so.FindProperty("recipeResult").objectReferenceValue = result;
        so.ApplyModifiedPropertiesWithoutUndo();
        recipe.gameObject.SetActive(false);
        return true;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static Image ImageAt(string name, Transform parent, Vector2 pos, Vector2 size, Sprite sprite)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    static void Label(string name, Transform parent, Vector2 pos, string text, TextMeshProUGUI styleFrom)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(30f, 40f));
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (styleFrom != null)
        {
            label.font = styleFrom.font;
            label.fontSharedMaterial = styleFrom.fontSharedMaterial;
        }
        label.text = text;
        label.fontSize = 34f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;
    }

    static void SetArray<T>(SerializedProperty prop, List<T> items) where T : Object
    {
        prop.arraySize = items.Count;
        for (var i = 0; i < items.Count; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }
}
