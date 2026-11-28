using System.IO;
using Survivor;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

// Menu "Survivor/Build UI Prefabs": creates the UI prefabs once (Assets/Prefabs/UI), marks the popups as
// Addressables, fills Assets/Resources/PopupCatalog.asset and assigns the HUD prefab to GameLifetimeScope.
// Existing prefabs are NOT overwritten, so your edits are safe. Use "Rebuild UI Prefabs (overwrite)" to start over.
public static class UiPrefabBuilder
{
    const string PrefabDir = "Assets/Prefabs/UI";
    const string SpriteDir = "Assets/Art/UI";
    const string CatalogPath = "Assets/Resources/PopupCatalog.asset";
    const string ScenePath = "Assets/Scenes/Main.unity";
    static readonly Vector2 Reference = new Vector2(1080, 1920); // portrait, scaled by width

    static Sprite circle;
    static Sprite square;

    [MenuItem("Survivor/Build UI Prefabs")]
    public static void Build() => Run(false);

    [MenuItem("Survivor/Rebuild UI Prefabs (overwrite)")]
    public static void Rebuild()
    {
        if (Application.isBatchMode || EditorUtility.DisplayDialog("Rebuild UI prefabs",
                "This overwrites Hud, LevelUpPopup, PausePopup and ResultPopup prefabs. Your edits to them will be lost.",
                "Overwrite", "Cancel"))
            Run(true);
    }

    static void Run(bool overwrite)
    {
        Directory.CreateDirectory(PrefabDir);
        CreateUiSprites();

        var hud = Save("Hud", BuildHud, overwrite);
        var levelUp = Save("LevelUpPopup", BuildLevelUpPopup, overwrite);
        var pause = Save("PausePopup", BuildPausePopup, overwrite);
        var result = Save("ResultPopup", BuildResultPopup, overwrite);

        FillCatalog(levelUp, pause, result);
        AssignHud(hud);
        AssetDatabase.SaveAssets();
        LocalizationSetup.Setup(); // keys + fonts on the new labels, language row in the pause menu
        Debug.Log("Survivor: UI prefabs ready in " + PrefabDir);
    }

    // ---------------- Shared helpers ----------------

    static GameObject Save(string name, System.Func<GameObject> build, bool overwrite)
    {
        var path = $"{PrefabDir}/{name}.prefab";
        if (!overwrite && File.Exists(path)) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = build();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // Real sprite files (prefabs cannot keep sprites created at runtime).
    static void CreateUiSprites()
    {
        Directory.CreateDirectory(SpriteDir);
        circle = SpriteFile("ui_circle", 256, true);
        square = SpriteFile("ui_square", 8, false);
    }

    static Sprite SpriteFile(string name, int size, bool round)
    {
        var path = $"{SpriteDir}/{name}.png";
        if (!File.Exists(path))
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var r = size / 2f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var a = round ? Mathf.Clamp01(r - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r))) : 1f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static GameObject CanvasRoot(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = Reference;
        scaler.matchWidthOrHeight = 0f;
        return go;
    }

    // Popup prefab skeleton: root (Canvas, CanvasGroup, popup script) > Dim + AnimatedGroup.
    static (GameObject root, RectTransform group) PopupRoot<T>(string name, float dim) where T : BasePopup
    {
        var root = CanvasRoot(name, 200);
        root.AddComponent<CanvasGroup>();
        var popup = root.AddComponent<T>();
        var rt = (RectTransform)root.transform;
        Img(rt, "Dim", square, new Color(0f, 0f, 0f, dim)).rectTransform.Stretch();
        var group = Rect(rt, "AnimatedGroup");
        group.Stretch();
        Wire(popup, "animatedGroup", group);
        return (root, group);
    }

    static RectTransform Rect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image Img(Transform parent, string name, Sprite sprite, Color color)
    {
        var rt = Rect(parent, name);
        var image = rt.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color? color = null)
    {
        var rt = Rect(parent, name);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color ?? Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }

    static Outline Highlight(GameObject go, float distance)
    {
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.85f, 0.3f);
        outline.effectDistance = new Vector2(distance, -distance);
        outline.enabled = false;
        return outline;
    }

    // Sets a [SerializeField] (also private ones) on a component.
    static void Wire(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        prop.arraySize = values.Length;
        for (var i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------- HUD ----------------

    static GameObject BuildHud()
    {
        var root = CanvasRoot("Hud", 0);
        var hud = root.AddComponent<HudView>();
        var rt = (RectTransform)root.transform;

        // Floating joystick: invisible full-screen touch area, drawn first so everything else sits on top.
        var area = Img(rt, "JoystickArea", square, new Color(0, 0, 0, 0));
        area.rectTransform.Stretch();
        var background = Img(area.transform, "Background", circle, new Color(1, 1, 1, 0.15f));
        background.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
        background.raycastTarget = false;
        var knob = Img(background.transform, "Knob", circle, new Color(1, 1, 1, 0.5f));
        knob.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
        knob.raycastTarget = false;
        var joystick = area.gameObject.AddComponent<VirtualJoystick>();
        joystick.Background = background.rectTransform;
        joystick.Knob = knob.rectTransform;
        background.gameObject.SetActive(false);

        // Dark strip so white text stays readable on the bright beach.
        var strip = Img(rt, "TopStrip", square, new Color(0f, 0f, 0f, 0.35f));
        strip.rectTransform.StretchTop(-95, 190);
        strip.raycastTarget = false;

        var xpBack = Img(rt, "XpBar", square, new Color(0f, 0f, 0f, 0.6f));
        xpBack.rectTransform.StretchTop(-18, 36);
        var xpFill = Img(xpBack.transform, "Fill", square, new Color(0.35f, 0.7f, 1f));
        xpFill.rectTransform.Stretch();
        xpFill.type = Image.Type.Filled;
        xpFill.fillMethod = Image.FillMethod.Horizontal;
        var level = Txt(xpBack.transform, "Level", "Cấp 1", 28);
        level.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(110, 0), new Vector2(200, 36));

        var time = Txt(rt, "Time", "00:00", 56);
        time.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -85), new Vector2(300, 70));
        // Portrait: kills and debug stats go on a second row under the timer.
        var kills = Txt(rt, "Kills", "Hạ gục: 0", 34);
        kills.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-180, -155), new Vector2(320, 50));
        kills.alignment = TextAlignmentOptions.Right;
        var debug = Txt(rt, "DebugStats", "", 24, new Color(1f, 1f, 1f, 0.9f));
        debug.rectTransform.Place(new Vector2(0f, 1f), new Vector2(330, -160), new Vector2(620, 70));
        debug.alignment = TextAlignmentOptions.Left;

        // Pause button "II", top-right.
        var pause = Img(rt, "PauseButton", circle, new Color(0f, 0f, 0f, 0.55f));
        pause.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-70, -80), new Vector2(84, 84));
        var bar1 = Img(pause.transform, "Bar1", square, Color.white);
        bar1.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-11, 0), new Vector2(12, 36));
        bar1.raycastTarget = false;
        var bar2 = Img(pause.transform, "Bar2", square, Color.white);
        bar2.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(11, 0), new Vector2(12, 36));
        bar2.raycastTarget = false;
        var pauseButton = pause.gameObject.AddComponent<TapButton>();

        var bossBack = Img(rt, "BossBar", square, new Color(0f, 0f, 0f, 0.7f));
        bossBack.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -230), new Vector2(900, 34));
        var bossFill = Img(bossBack.transform, "Fill", square, new Color(0.8f, 0.1f, 0.2f));
        bossFill.rectTransform.Stretch(new Vector2(-8, -8));
        bossFill.type = Image.Type.Filled;
        bossFill.fillMethod = Image.FillMethod.Horizontal;
        var bossName = Txt(bossBack.transform, "Name", "Boss", 26);
        bossName.rectTransform.Stretch();

        Wire(hud, "xpFill", xpFill);
        Wire(hud, "levelText", level);
        Wire(hud, "timeText", time);
        Wire(hud, "killText", kills);
        Wire(hud, "debugText", debug);
        Wire(hud, "pauseButton", pauseButton);
        Wire(hud, "bossBar", bossBack.gameObject);
        Wire(hud, "bossFill", bossFill);
        Wire(hud, "bossName", bossName);
        Wire(hud, "joystick", joystick);
        return root;
    }

    // ---------------- Level-up / chest ----------------

    static GameObject BuildLevelUpPopup()
    {
        var (root, group) = PopupRoot<LevelUpPopup>("LevelUpPopup", 0.7f);
        var popup = root.GetComponent<LevelUpPopup>();

        var title = Txt(group, "Title", "LÊN CẤP!", 90, new Color(1f, 0.85f, 0.3f));
        title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 600), new Vector2(1000, 120));
        var hint = Txt(group, "Hint", "Chọn 1 nâng cấp: chạm vào thẻ, phím 1 / 2 / 3, hoặc tay cầm (gạt lên/xuống + A)", 30);
        hint.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 505), new Vector2(980, 90));
        hint.textWrappingMode = TextWrappingModes.Normal;

        var cards = new UpgradeCardView[3];
        for (var i = 0; i < 3; i++)
        {
            var card = Img(group, "Card" + i, square, new Color(0.12f, 0.12f, 0.18f, 0.95f));
            card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -80 - (i - 1) * 340), new Vector2(940, 300));
            var stripe = Img(card.transform, "Stripe", square, Color.white);
            stripe.rectTransform.StretchTop(-6, 12);
            var key = Txt(card.transform, "Key", "[1]", 28, new Color(1f, 1f, 1f, 0.5f));
            key.rectTransform.Place(new Vector2(0f, 1f), new Vector2(60, -45), new Vector2(80, 40));
            var name = Txt(card.transform, "Title", "Tên", 48);
            name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(700, 64));
            var badge = Txt(card.transform, "Badge", "MỚI!", 32, new Color(1f, 0.85f, 0.3f));
            badge.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-100, -45), new Vector2(180, 40));
            var desc = Txt(card.transform, "Description", "Mô tả", 32);
            desc.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -45), new Vector2(860, 150));
            desc.textWrappingMode = TextWrappingModes.Normal;

            var view = card.gameObject.AddComponent<UpgradeCardView>();
            Wire(view, "stripe", stripe);
            Wire(view, "keyHint", key);
            Wire(view, "title", name);
            Wire(view, "badge", badge);
            Wire(view, "description", desc);
            Wire(view, "highlight", Highlight(card.gameObject, 6f));
            Wire(view, "button", card.gameObject.AddComponent<TapButton>());
            cards[i] = view;
        }

        Wire(popup, "title", title);
        WireArray(popup, "cards", cards);
        return root;
    }

    // ---------------- Pause ----------------

    static GameObject BuildPausePopup()
    {
        var (root, group) = PopupRoot<PausePopup>("PausePopup", 0.7f);
        var popup = root.GetComponent<PausePopup>();

        var panel = Img(group, "Panel", square, new Color(0.1f, 0.11f, 0.16f, 0.97f));
        panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 700));
        var p = panel.transform;
        Txt(p, "Title", "TẠM DỪNG", 72, new Color(1f, 0.85f, 0.3f)).rectTransform
            .Place(new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(700, 100));

        var (musicRow, musicSlider, musicPercent) = SliderRow(p, "MusicRow", "Nhạc nền", 110);
        var (sfxRow, sfxSlider, sfxPercent) = SliderRow(p, "SfxRow", "Âm thanh hiệu ứng", -10);
        var (resumeRow, resume) = ButtonRow(p, "ResumeButton", "Tiếp tục", -150, new Color(0.2f, 0.6f, 1f));
        var (restartRow, restart) = ButtonRow(p, "RestartButton", "Chơi lại", -250, new Color(0.45f, 0.45f, 0.5f));

        Wire(popup, "musicSlider", musicSlider);
        Wire(popup, "musicPercent", musicPercent);
        Wire(popup, "sfxSlider", sfxSlider);
        Wire(popup, "sfxPercent", sfxPercent);
        Wire(popup, "resumeButton", resume);
        Wire(popup, "restartButton", restart);
        WireArray(popup, "rows", new Object[] { musicRow, sfxRow, resumeRow, restartRow });
        return root;
    }

    static (Outline row, Slider slider, TextMeshProUGUI percent) SliderRow(Transform parent, string name, string label, float y)
    {
        var row = Img(parent, name, square, new Color(1f, 1f, 1f, 0.04f));
        row.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(820, 110));
        Txt(row.transform, "Label", label, 34).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(640, 46));

        var sliderRect = Rect(row.transform, "Slider");
        sliderRect.Place(new Vector2(0.5f, 0.5f), new Vector2(-60, -24), new Vector2(560, 28));
        Img(sliderRect, "Background", square, new Color(1f, 1f, 1f, 0.15f)).rectTransform.Stretch();
        var fillArea = Rect(sliderRect, "Fill Area");
        fillArea.Stretch();
        var fill = Img(fillArea, "Fill", square, new Color(0.35f, 0.75f, 1f));
        fill.rectTransform.Stretch();
        var handleArea = Rect(sliderRect, "Handle Area");
        handleArea.Stretch();
        var handle = Img(handleArea, "Handle", circle, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(44, 44);

        var slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;

        var percent = Txt(row.transform, "Percent", "100%", 30);
        percent.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(330, -24), new Vector2(120, 40));
        return (Highlight(row.gameObject, 4f), slider, percent);
    }

    static (Outline row, TapButton button) ButtonRow(Transform parent, string name, string label, float y, Color color)
    {
        var row = Img(parent, name, square, color);
        row.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(420, 80));
        Txt(row.transform, "Label", label, 40).rectTransform.Stretch();
        return (Highlight(row.gameObject, 4f), row.gameObject.AddComponent<TapButton>());
    }

    // ---------------- Result ----------------

    static GameObject BuildResultPopup()
    {
        var (root, group) = PopupRoot<ResultPopup>("ResultPopup", 0.8f);
        var popup = root.GetComponent<ResultPopup>();

        var title = Txt(group, "Title", "SỐNG SÓT!", 96, new Color(1f, 0.85f, 0.3f));
        title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 260), new Vector2(1000, 150));
        title.enableAutoSizing = true; // long titles shrink to fit the portrait width
        title.fontSizeMin = 48;
        title.fontSizeMax = 96;
        var stats = Txt(group, "Stats", "Thời gian: 00:00\nHạ gục: 0\nCấp: 1", 44);
        stats.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(800, 220));
        var (_, restart) = ButtonRow(group, "RestartButton", "Chơi lại (R / A)", -200, new Color(0.2f, 0.6f, 1f));
        ((RectTransform)restart.transform).sizeDelta = new Vector2(460, 120);

        Wire(popup, "title", title);
        Wire(popup, "stats", stats);
        Wire(popup, "restartButton", restart);
        return root;
    }

    // ---------------- Addressables, catalog, scene ----------------

    static void FillCatalog(params GameObject[] popups)
    {
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PopupCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var entries = new PopupEntry[popups.Length];
        for (var i = 0; i < popups.Length; i++)
        {
            var prefab = popups[i];
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = "UI/" + prefab.name;

            var typeName = prefab.GetComponent<BasePopup>().GetType().Name;
            var old = catalog.Find(prefab.GetComponent<BasePopup>().GetType());
            entries[i] = new PopupEntry
            {
                TypeName = typeName,
                Asset = new AssetReferenceGameObject(guid),
                Preload = old?.Preload ?? true,      // keep flags you changed in the Inspector
                KeepLoaded = old?.KeepLoaded ?? true,
                KeepAsset = old?.KeepAsset ?? true,
            };
        }
        catalog.Entries = entries;
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(settings);
    }

    static void AssignHud(GameObject hudPrefab)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
        if (scope == null)
        {
            Debug.LogError("UiPrefabBuilder: no GameLifetimeScope in " + ScenePath);
            return;
        }
        Wire(scope, "hudPrefab", hudPrefab.GetComponent<HudView>());
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}

static class RectTransformLayout
{
    public static void Place(this RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    public static void Stretch(this RectTransform rt, Vector2? sizeDelta = null)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = sizeDelta ?? Vector2.zero;
    }

    // Full-width bar anchored to the top of the screen.
    public static void StretchTop(this RectTransform rt, float y, float height)
    {
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(0, y);
        rt.sizeDelta = new Vector2(0, height);
    }
}
