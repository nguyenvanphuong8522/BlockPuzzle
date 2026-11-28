using System.Collections.Generic;
using System.IO;
using Survivor;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using static UiPrefabBuilder;

// Menu "Survivor/Build Home UI": the Home screen (energy, settings, chapter card, Play), the Settings popup (volumes,
// language) and the Yes / No popup, as Addressable prefabs added to PopupCatalog. Also creates HomeSettings.asset and
// three chapters (Resources/Chapters) when missing. Existing prefabs / assets are NOT overwritten (safe to run again);
// "Rebuild Home UI (overwrite)" remakes the three prefabs.
public static class HomeUiBuilder
{
    const string PrefabDir = "Assets/Prefabs/UI";
    const string CatalogPath = "Assets/Resources/PopupCatalog.asset";
    const string HomeSettingsPath = "Assets/Resources/HomeSettings.asset";
    const string ChapterDir = "Assets/Resources/Chapters";
    const string SpriteDir = "Assets/Art/Sprites";

    static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    static readonly Color PanelColor = new Color(0.1f, 0.11f, 0.16f, 0.97f);

    [MenuItem("Survivor/Build Home UI")]
    public static void Build() => Run(false);

    [MenuItem("Survivor/Rebuild Home UI (overwrite)")]
    public static void Rebuild()
    {
        if (Application.isBatchMode || EditorUtility.DisplayDialog("Rebuild Home UI",
                "This overwrites HomePopup, SettingsPopup and ConfirmPopup prefabs. Your edits to them will be lost.",
                "Overwrite", "Cancel"))
            Run(true);
    }

    static void Run(bool overwrite)
    {
        Directory.CreateDirectory(PrefabDir);
        CreateUiSprites();
        var home = Save("HomePopup", BuildHome, overwrite);
        var settings = Save("SettingsPopup", BuildSettings, overwrite);
        var confirm = Save("ConfirmPopup", BuildConfirm, overwrite);
        AddToCatalog(home, settings, confirm);
        EnsureHomeSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("Survivor: Home UI ready (HomePopup, SettingsPopup, ConfirmPopup, HomeSettings, chapters).");
    }

    static GameObject Save(string name, System.Func<GameObject> build, bool overwrite)
    {
        var path = $"{PrefabDir}/{name}.prefab";
        if (!overwrite && File.Exists(path)) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = build();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------------- Helpers ----------------

    static Sprite Icon(string name)
    {
        var path = $"{SpriteDir}/{name}.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Fixed label: its words come from Localization.csv (key).
    static TextMeshProUGUI Label(Transform parent, string name, string key, string text, float size, Color? color = null)
    {
        var t = Txt(parent, name, text, size, color);
        t.gameObject.AddComponent<LocalizedText>().Key = key;
        return t;
    }

    // Text written by code: only its font follows the language.
    static TextMeshProUGUI CodeText(Transform parent, string name, string text, float size, Color? color = null)
    {
        var t = Txt(parent, name, text, size, color);
        t.gameObject.AddComponent<LocalizedFont>();
        return t;
    }

    static TapButton RoundButton(Transform parent, string name, Vector2 anchor, Vector2 pos, float size, Sprite icon, float iconScale)
    {
        var bg = Img(parent, name, circle, new Color(0f, 0f, 0f, 0.55f));
        bg.rectTransform.Place(anchor, pos, new Vector2(size, size));
        var ic = Img(bg.transform, "Icon", icon, Color.white);
        ic.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * iconScale, size * iconScale));
        ic.preserveAspect = true;
        ic.raycastTarget = false;
        return bg.gameObject.AddComponent<TapButton>();
    }

    static void LabelButton(Transform parent, string name, string key, string text, Vector2 pos, Vector2 size, Color color,
        out TapButton button)
    {
        var bg = Img(parent, name, square, color);
        bg.rectTransform.Place(new Vector2(0.5f, 0.5f), pos, size);
        Label(bg.transform, "Label", key, text, 44).rectTransform.Stretch();
        button = bg.gameObject.AddComponent<TapButton>();
    }

    // ---------------- Home ----------------

    static GameObject BuildHome()
    {
        var (root, group) = PopupRoot<HomePopup>("HomePopup", 0f);
        var popup = root.GetComponent<HomePopup>();
        var gear = Icon("ui_gear");
        var bolt = Icon("ui_energy");
        var lockIcon = Icon("ui_lock");
        var arrow = Icon("ui_arrow");

        // Background (the beach shows faintly behind) and the title.
        var bg = Img(group, "Background", square, new Color(0.05f, 0.12f, 0.2f, 0.88f));
        bg.rectTransform.Stretch();
        Label(group, "Title", "home.title", "PAL RUSH", 120, Gold).rectTransform
            .Place(new Vector2(0.5f, 1f), new Vector2(0, -260), new Vector2(1000, 150));
        Label(group, "Subtitle", "home.subtitle", "SURVIVORS", 56, Color.white).rectTransform
            .Place(new Vector2(0.5f, 1f), new Vector2(0, -355), new Vector2(1000, 70));

        // Energy pill, top left: bolt + "60/60", refill timer under it.
        var pill = Img(group, "Energy", square, new Color(0f, 0f, 0f, 0.55f));
        pill.rectTransform.Place(new Vector2(0f, 1f), new Vector2(200, -80), new Vector2(300, 84));
        var boltImage = Img(pill.transform, "Icon", bolt, Gold);
        boltImage.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(46, 0), new Vector2(70, 70));
        boltImage.preserveAspect = true;
        var energyText = CodeText(pill.transform, "Value", "60/60", 46);
        energyText.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(30, 0), new Vector2(200, 70));
        var energyTimer = CodeText(group, "EnergyTimer", "Đầy", 30, new Color(1f, 1f, 1f, 0.85f));
        energyTimer.rectTransform.Place(new Vector2(0f, 1f), new Vector2(200, -150), new Vector2(300, 44));

        // Settings, top right.
        var settings = RoundButton(group, "SettingsButton", new Vector2(1f, 1f), new Vector2(-80, -80), 110, gear, 0.62f);

        // Chapter card.
        var card = Img(group, "ChapterCard", square, new Color(0.16f, 0.36f, 0.55f, 1f));
        card.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(820, 960));
        var c = card.transform;
        var number = CodeText(c, "Number", "Chương 1", 44, new Color(1f, 1f, 1f, 0.8f));
        number.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(760, 60));
        var name = CodeText(c, "Name", "Bãi Cát Vàng", 64, Gold);
        name.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -135), new Vector2(760, 90));
        name.enableAutoSizing = true;
        name.fontSizeMin = 36;
        name.fontSizeMax = 64;
        var previewBack = Img(c, "PreviewBack", circle, new Color(0f, 0f, 0f, 0.25f));
        previewBack.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(560, 560));
        previewBack.raycastTarget = false;
        var preview = Img(c, "Preview", null, Color.white);
        preview.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(540, 540));
        preview.preserveAspect = true;
        preview.raycastTarget = false;
        var info = CodeText(c, "Info", "Kỷ lục: 00:00", 38);
        info.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(760, 120));
        info.textWrappingMode = TextWrappingModes.Normal;
        var page = CodeText(c, "Page", "1 / 3", 30, new Color(1f, 1f, 1f, 0.6f));
        page.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(300, 44));

        var lockOverlay = Img(c, "Locked", square, new Color(0f, 0f, 0f, 0.45f));
        lockOverlay.rectTransform.Stretch();
        lockOverlay.raycastTarget = false;
        var lockImage = Img(lockOverlay.transform, "Icon", lockIcon, new Color(1f, 1f, 1f, 0.9f));
        lockImage.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(200, 200));
        lockImage.preserveAspect = true;
        lockImage.raycastTarget = false;

        var swipe = card.gameObject.AddComponent<SwipeArea>();

        // Arrows beside the card.
        var previous = RoundButton(group, "PreviousButton", new Vector2(0.5f, 0.5f), new Vector2(-470, 80), 96, arrow, 0.55f);
        previous.transform.Find("Icon").localRotation = Quaternion.Euler(0, 0, 180);
        var next = RoundButton(group, "NextButton", new Vector2(0.5f, 0.5f), new Vector2(470, 80), 96, arrow, 0.55f);

        // Play button + energy cost, message line above it.
        var message = CodeText(group, "Message", "", 36, new Color(1f, 0.55f, 0.5f));
        message.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 450), new Vector2(1000, 60));
        var play = Img(group, "PlayButton", square, new Color(1f, 0.72f, 0.15f));
        play.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 300), new Vector2(600, 180));
        Label(play.transform, "Label", "home.play", "BẮT ĐẦU", 72, new Color(0.25f, 0.12f, 0.02f)).rectTransform
            .Place(new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(560, 100));
        var costIcon = Img(play.transform, "CostIcon", bolt, new Color(0.25f, 0.12f, 0.02f));
        costIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-30, -50), new Vector2(40, 40));
        costIcon.preserveAspect = true;
        costIcon.raycastTarget = false;
        var cost = CodeText(play.transform, "Cost", "-5", 36, new Color(0.25f, 0.12f, 0.02f));
        cost.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(20, -50), new Vector2(80, 44));
        cost.alignment = TextAlignmentOptions.Left;
        var playButton = play.gameObject.AddComponent<TapButton>();

        Wire(popup, "energyText", energyText);
        Wire(popup, "energyTimer", energyTimer);
        Wire(popup, "settingsButton", settings);
        Wire(popup, "card", card);
        Wire(popup, "chapterNumber", number);
        Wire(popup, "chapterName", name);
        Wire(popup, "preview", preview);
        Wire(popup, "chapterInfo", info);
        Wire(popup, "pageText", page);
        Wire(popup, "lockOverlay", lockOverlay.gameObject);
        Wire(popup, "previousButton", previous);
        Wire(popup, "nextButton", next);
        Wire(popup, "swipe", swipe);
        Wire(popup, "playButton", playButton);
        Wire(popup, "playBackground", play);
        Wire(popup, "playCost", cost);
        Wire(popup, "message", message);
        return root;
    }

    // ---------------- Settings ----------------

    static GameObject BuildSettings()
    {
        var (root, group) = PopupRoot<SettingsPopup>("SettingsPopup", 0.7f);
        var popup = root.GetComponent<SettingsPopup>();
        var arrow = Icon("ui_arrow");

        var panel = Img(group, "Panel", square, PanelColor);
        panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 820));
        var p = panel.transform;
        Label(p, "Title", "settings.title", "CÀI ĐẶT", 72, Gold).rectTransform
            .Place(new Vector2(0.5f, 0.5f), new Vector2(0, 310), new Vector2(700, 100));

        var (_, musicSlider, musicPercent) = SliderRow(p, "MusicRow", "Nhạc nền", 170);
        var (_, sfxSlider, sfxPercent) = SliderRow(p, "SfxRow", "Âm thanh hiệu ứng", 40);
        Retag(p.Find("MusicRow/Label"), "pause.music");
        Retag(p.Find("SfxRow/Label"), "pause.sfx");

        // Language row: "Ngôn ngữ" + < Tiếng Việt >
        var row = Img(p, "LanguageRow", square, new Color(1f, 1f, 1f, 0.04f));
        row.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -100), new Vector2(820, 130));
        Label(row.transform, "Label", "settings.language", "Ngôn ngữ", 34).rectTransform
            .Place(new Vector2(0.5f, 0.5f), new Vector2(0, 34), new Vector2(640, 46));
        var langButton = Img(row.transform, "LanguageButton", square, new Color(0.25f, 0.45f, 0.35f));
        langButton.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(420, 64));
        var langLabel = CodeText(langButton.transform, "Value", "Tiếng Việt", 36);
        langLabel.rectTransform.Stretch();
        var previous = RoundButton(row.transform, "Previous", new Vector2(0.5f, 0.5f), new Vector2(-280, -24), 70, arrow, 0.55f);
        previous.transform.Find("Icon").localRotation = Quaternion.Euler(0, 0, 180);
        var next = RoundButton(row.transform, "Next", new Vector2(0.5f, 0.5f), new Vector2(280, -24), 70, arrow, 0.55f);

        LabelButton(p, "CloseButton", "common.close", "Đóng", new Vector2(0, -290), new Vector2(420, 100),
            new Color(0.2f, 0.6f, 1f), out var close);

        Wire(popup, "musicSlider", musicSlider);
        Wire(popup, "musicPercent", musicPercent);
        Wire(popup, "sfxSlider", sfxSlider);
        Wire(popup, "sfxPercent", sfxPercent);
        Wire(popup, "languageButton", langButton.gameObject.AddComponent<TapButton>());
        Wire(popup, "languageLabel", langLabel);
        Wire(popup, "languagePrevious", previous);
        Wire(popup, "languageNext", next);
        Wire(popup, "closeButton", close);
        return root;
    }

    static void Retag(Transform label, string key)
    {
        if (label == null) return;
        label.gameObject.AddComponent<LocalizedText>().Key = key;
    }

    // ---------------- Confirm ----------------

    static GameObject BuildConfirm()
    {
        var (root, group) = PopupRoot<ConfirmPopup>("ConfirmPopup", 0.6f);
        var popup = root.GetComponent<ConfirmPopup>();
        var panel = Img(group, "Panel", square, PanelColor);
        panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 480));
        var p = panel.transform;
        var message = CodeText(p, "Message", "Về Home?", 44);
        message.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(780, 220));
        message.textWrappingMode = TextWrappingModes.Normal;
        LabelButton(p, "NoButton", "common.no", "Hủy", new Vector2(-200, -150), new Vector2(340, 110),
            new Color(0.45f, 0.45f, 0.5f), out var no);
        LabelButton(p, "YesButton", "common.yes", "Đồng ý", new Vector2(200, -150), new Vector2(340, 110),
            new Color(0.75f, 0.25f, 0.25f), out var yes);
        Wire(popup, "message", message);
        Wire(popup, "yesButton", yes);
        Wire(popup, "noButton", no);
        return root;
    }

    // ---------------- Catalog, settings, chapters ----------------

    static void AddToCatalog(params GameObject[] popups)
    {
        var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var catalog = AssetDatabase.LoadAssetAtPath<PopupCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PopupCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }
        var entries = new List<PopupEntry>(catalog.Entries);
        foreach (var prefab in popups)
        {
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
            var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
            entry.address = "UI/" + prefab.name;
            var type = prefab.GetComponent<BasePopup>().GetType();
            entries.RemoveAll(e => e != null && e.TypeName == type.Name);
            entries.Add(new PopupEntry { TypeName = type.Name, Asset = new AssetReferenceGameObject(guid) });
        }
        catalog.Entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(settings);
    }

    static void EnsureHomeSettings()
    {
        var home = AssetDatabase.LoadAssetAtPath<HomeSettings>(HomeSettingsPath);
        if (home == null)
        {
            home = ScriptableObject.CreateInstance<HomeSettings>();
            AssetDatabase.CreateAsset(home, HomeSettingsPath);
        }
        if (home.Chapters != null && home.Chapters.Length > 0) return;

        Directory.CreateDirectory(ChapterDir);
        // Starting chapters (edit / add more: Create > Survivor > Chapter, then list them in HomeSettings > Chapters).
        home.Chapters = new[]
        {
            Chapter(1, new Color(0.16f, 0.4f, 0.55f), "enemy6_kingcrab_0", 1f, 1f, 1f, null),
            Chapter(2, new Color(0.45f, 0.22f, 0.42f), "enemy8_hammerhead_0", 1.6f, 1.3f, 1.15f,
                Schedule(2, (180f, "CaMapBua"), (360f, "VuaCua"), (600f, "TomTit"))),
            Chapter(3, new Color(0.12f, 0.16f, 0.32f), "enemy9_mantisshrimp_0", 2.4f, 1.6f, 1.3f,
                Schedule(3, (180f, "TomTit"), (360f, "CaMapBua"), (600f, "TomTit"))),
        };
        EditorUtility.SetDirty(home);
    }

    static ChapterData Chapter(int n, Color color, string preview, float hp, float damage, float spawn, BossSchedule bosses)
    {
        var path = $"{ChapterDir}/Chapter{n}.asset";
        var c = AssetDatabase.LoadAssetAtPath<ChapterData>(path);
        if (c != null) return c;
        c = ScriptableObject.CreateInstance<ChapterData>();
        c.NameKey = $"chapter.{n}.name";
        c.CardColor = color;
        c.Preview = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{preview}.png");
        c.EnemyHpScale = hp;
        c.DamageTakenScale = damage;
        c.SpawnRateScale = spawn;
        c.Bosses = bosses;
        AssetDatabase.CreateAsset(c, path);
        return c;
    }

    static BossSchedule Schedule(int n, params (float time, string id)[] bosses)
    {
        var path = $"{ChapterDir}/Chapter{n}_Bosses.asset";
        var s = AssetDatabase.LoadAssetAtPath<BossSchedule>(path);
        if (s != null) return s;
        s = ScriptableObject.CreateInstance<BossSchedule>();
        s.Bosses.Clear();
        foreach (var (time, id) in bosses) s.Bosses.Add(new BossSchedule.Entry { Time = time, BossId = id });
        AssetDatabase.CreateAsset(s, path);
        return s;
    }
}
