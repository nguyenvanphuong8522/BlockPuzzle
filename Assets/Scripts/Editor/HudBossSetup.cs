using Survivor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Menu "Survivor/Setup Hud Boss UI": adds to Hud.prefab
//  - BossCountdown: boss icon + time left, centered under the XP bar,
//  - BossWarning: the red "boss coming" banner,
//  - a boss icon on the boss health bar, and moves the boss bar onto the XP bar's place (it replaces it in a fight).
// Parts that already exist are left alone, so it is safe to run again after moving things in the prefab.
public static class HudBossSetup
{
    const string PrefabPath = "Assets/Prefabs/UI/Hud.prefab";

    [MenuItem("Survivor/Setup Hud Boss UI")]
    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var hud = root.GetComponent<HudView>();
        var so = new SerializedObject(hud);
        var t = root.transform;
        var timeText = t.Find("Time")?.GetComponent<TextMeshProUGUI>();
        var square = t.Find("XpBar")?.GetComponent<Image>()?.sprite;
        var circle = t.Find("PauseButton")?.GetComponent<Image>()?.sprite;

        so.FindProperty("xpBar").objectReferenceValue = t.Find("XpBar")?.gameObject;

        // ---- Boss bar on the XP bar's place, with the boss picture at its left end ----
        var bossBar = (RectTransform)t.Find("BossBar");
        if (bossBar != null && bossBar.Find("Icon") == null)
        {
            bossBar.anchorMin = new Vector2(0.5f, 1f);
            bossBar.anchorMax = new Vector2(0.5f, 1f);
            bossBar.anchoredPosition = new Vector2(30f, -136f);
            bossBar.sizeDelta = new Vector2(940f, 44f);
            var ring = MakeImage("IconRing", bossBar, new Vector2(0f, 0.5f), new Vector2(-16f, 0f), new Vector2(92f, 92f), circle);
            ring.color = new Color(0.55f, 0.05f, 0.1f, 1f);
            var icon = MakeImage("Icon", bossBar, new Vector2(0f, 0.5f), new Vector2(-16f, 2f), new Vector2(80f, 80f), null);
            so.FindProperty("bossBarIcon").objectReferenceValue = icon;
        }

        // ---- Countdown under the XP bar ----
        if (t.Find("BossCountdown") == null)
        {
            var box = Rect("BossCountdown", t, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(230f, 64f));
            var bg = box.gameObject.AddComponent<Image>();
            bg.sprite = square;
            bg.color = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;
            var ring = MakeImage("IconRing", box, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(78f, 78f), circle);
            ring.color = new Color(0.75f, 0.1f, 0.15f, 1f);
            var icon = MakeImage("Icon", box, new Vector2(0f, 0.5f), new Vector2(10f, 2f), new Vector2(68f, 68f), null);
            var text = MakeText("Time", box, new Vector2(30f, 0f), new Vector2(160f, 64f), "03:00", 40f, timeText);
            text.color = new Color(1f, 0.85f, 0.85f);
            so.FindProperty("bossCountdown").objectReferenceValue = box.gameObject;
            so.FindProperty("bossCountdownIcon").objectReferenceValue = icon;
            so.FindProperty("bossCountdownText").objectReferenceValue = text;
        }

        // ---- Warning banner ----
        if (t.Find("BossWarning") == null)
        {
            var banner = Rect("BossWarning", t, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 330f), new Vector2(0f, 210f));
            var group = banner.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var bg = banner.gameObject.AddComponent<Image>();
            bg.sprite = square;
            bg.color = new Color(0.6f, 0.02f, 0.08f, 0.78f);
            bg.raycastTarget = false;
            foreach (var y in new[] { 1f, 0f })
            {
                var line = Rect(y > 0.5f ? "LineTop" : "LineBottom", banner, new Vector2(0f, y), new Vector2(1f, y),
                    Vector2.zero, new Vector2(0f, 10f));
                var img = line.gameObject.AddComponent<Image>();
                img.sprite = square;
                img.color = new Color(1f, 0.82f, 0.2f);
                img.raycastTarget = false;
            }
            var title = MakeText("Title", banner, new Vector2(0f, 30f), new Vector2(760f, 100f), "BOSS SẮP XUẤT HIỆN!", 72f, timeText);
            title.color = new Color(1f, 0.88f, 0.25f);
            title.fontStyle = FontStyles.Bold;
            var sub = MakeText("Sub", banner, new Vector2(0f, -50f), new Vector2(760f, 60f), "Boss xuất hiện sau 5 giây!", 40f, timeText);
            sub.color = Color.white;
            var left = MakeImage("IconLeft", banner, new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(150f, 150f), null);
            var right = MakeImage("IconRight", banner, new Vector2(1f, 0.5f), new Vector2(-110f, 0f), new Vector2(150f, 150f), null);
            right.transform.localScale = new Vector3(-1f, 1f, 1f);
            so.FindProperty("bossWarning").objectReferenceValue = group;
            so.FindProperty("bossWarningText").objectReferenceValue = sub;
            var icons = so.FindProperty("bossWarningIcons");
            icons.arraySize = 2;
            icons.GetArrayElementAtIndex(0).objectReferenceValue = left;
            icons.GetArrayElementAtIndex(1).objectReferenceValue = right;
            banner.gameObject.SetActive(false);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("Hud boss UI set up.");
    }

    // Menu "Survivor/Setup Hud Swarm Warning": the edge warning for swarms (red "!" badge + arrow + label).
    // The code moves it to the screen edge the swarm comes from and turns the arrow. Left alone if it exists.
    [MenuItem("Survivor/Setup Hud Swarm Warning")]
    public static void SetupSwarmWarning()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        var t = root.transform;
        if (t.Find("SwarmWarning") == null)
        {
            var hud = root.GetComponent<HudView>();
            var so = new SerializedObject(hud);
            var timeText = t.Find("Time")?.GetComponent<TextMeshProUGUI>();
            var circle = t.Find("PauseButton")?.GetComponent<Image>()?.sprite;
            var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/ui_arrow.png");

            var box = Rect("SwarmWarning", t, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 200f));
            var group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var glow = MakeImage("Glow", box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f), circle);
            glow.color = new Color(1f, 0.15f, 0.1f, 0.3f);
            // The arrow turns around the badge's center: it sits on the right of a pivot that the code rotates.
            var pivot = Rect("ArrowPivot", box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            var arrow = MakeImage("Arrow", pivot, new Vector2(0.5f, 0.5f), new Vector2(82f, 0f), new Vector2(86f, 86f), arrowSprite);
            arrow.color = new Color(1f, 0.25f, 0.2f);
            var badge = MakeImage("Badge", box, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112f, 112f), circle);
            badge.color = new Color(0.9f, 0.12f, 0.12f, 1f);
            var mark = MakeText("Mark", box, new Vector2(0f, 2f), new Vector2(100f, 100f), "!", 84f, timeText);
            mark.color = Color.white;
            mark.fontStyle = FontStyles.Bold;
            var label = MakeText("Label", box, new Vector2(0f, -92f), new Vector2(320f, 50f), "ĐÀN QUÁI BAY TỚI!", 32f, timeText);
            label.color = new Color(1f, 0.9f, 0.3f);
            label.fontStyle = FontStyles.Bold;

            so.FindProperty("swarmWarning").objectReferenceValue = box;
            so.FindProperty("swarmArrow").objectReferenceValue = pivot;
            so.ApplyModifiedPropertiesWithoutUndo();
            box.gameObject.SetActive(false);
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("Hud swarm warning set up.");
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

    static Image MakeImage(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite)
    {
        var image = Rect(name, parent, anchor, anchor, pos, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI MakeText(string name, Transform parent, Vector2 pos, Vector2 size, string text, float fontSize,
        TextMeshProUGUI styleFrom)
    {
        var rt = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (styleFrom != null)
        {
            label.font = styleFrom.font;
            label.fontSharedMaterial = styleFrom.fontSharedMaterial;
        }
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }
}
