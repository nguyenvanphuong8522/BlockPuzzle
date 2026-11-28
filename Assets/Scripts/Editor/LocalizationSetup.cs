using System.Collections.Generic;
using System.IO;
using Survivor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Menu "Survivor/Localization/Setup": makes the game translatable.
//   - creates Assets/Resources/LocalizationSettings.asset (languages, fonts) if missing
//   - UI prefabs + Main scene: every fixed label whose text is in Localization.csv gets a LocalizedText (key);
//     every other TextMeshPro text gets a LocalizedFont (switches font with the language; its words come from code)
//   - adds the "Language" row to the pause menu
//   - text fields of assets (announcements, pig buff texts) are changed to their keys
// Safe to run again. "Reload Table" re-reads the CSV (also in Play mode).
public static class LocalizationSetup
{
    const string SettingsPath = "Assets/Resources/LocalizationSettings.asset";
    const string TablePath = "Assets/Resources/Localization/Localization.csv";
    const string PrefabDir = "Assets/Prefabs/UI";
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("Survivor/Localization/Setup")]
    public static void Setup()
    {
        var settings = EnsureSettings();
        Loc.Reload();
        var report = new List<string>();

        foreach (var path in Directory.GetFiles(PrefabDir, "*.prefab"))
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            var changed = TagTexts(root, report, Path.GetFileNameWithoutExtension(path));
            if (Path.GetFileNameWithoutExtension(path) == "PausePopup") changed |= AddPauseLanguageRow(root, report);
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (File.Exists(ScenePath) && !Application.isPlaying)
        {
            var scene = EditorSceneManager.GetActiveScene().path == ScenePath
                ? EditorSceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var changed = false;
            foreach (var go in scene.GetRootGameObjects()) changed |= TagTexts(go, report, "Main.unity");
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        report.AddRange(KeysInAssets());
        AssetDatabase.SaveAssets();
        Debug.Log($"Localization setup done ({settings.Languages.Count} languages).\n" + string.Join("\n", report));
    }

    [MenuItem("Survivor/Localization/Reload Table")]
    public static void ReloadTable()
    {
        AssetDatabase.ImportAsset(TablePath);
        Loc.Reload();
        Debug.Log("Localization table reloaded.");
    }

    static LocalizationSettings EnsureSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        if (settings.Table == null)
        {
            settings.Table = AssetDatabase.LoadAssetAtPath<TextAsset>(TablePath);
            EditorUtility.SetDirty(settings);
        }
        return settings;
    }

    // Fixed labels -> LocalizedText; texts written by code (referenced by a script field) -> LocalizedFont.
    static bool TagTexts(GameObject root, List<string> report, string where)
    {
        var referenced = ReferencedTexts(root);
        var changed = false;
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.GetComponent<LocalizedText>() != null || text.GetComponent<LocalizedFont>() != null) continue;
            var key = referenced.Contains(text) ? null : Loc.KeyOf(text.text);
            if (key != null)
            {
                text.gameObject.AddComponent<LocalizedText>().Key = key;
                report.Add($"{where}: {PathOf(text.transform)} -> {key}");
            }
            else text.gameObject.AddComponent<LocalizedFont>();
            changed = true;
        }
        return changed;
    }

    static HashSet<TMP_Text> ReferencedTexts(GameObject root)
    {
        var set = new HashSet<TMP_Text>();
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null || component is TMP_Text || component is Graphic) continue;
            var so = new SerializedObject(component);
            var it = so.GetIterator();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is TMP_Text t)
                    set.Add(t);
        }
        return set;
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    // Pause menu: a "Language: X" button row under Restart (press / left-right to switch).
    static bool AddPauseLanguageRow(GameObject root, List<string> report)
    {
        var popup = root.GetComponent<PausePopup>();
        if (popup == null) return false;
        var so = new SerializedObject(popup);
        if (so.FindProperty("languageButton").objectReferenceValue != null) return false;

        var restart = root.transform.Find("Group/Panel/RestartButton") ?? FindDeep(root.transform, "RestartButton");
        if (restart == null)
        {
            report.Add("PausePopup: RestartButton not found, language row not added.");
            return false;
        }
        var panel = (RectTransform)restart.parent;
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(panel.sizeDelta.y, 840f));

        var row = Object.Instantiate(restart.gameObject, panel);
        row.name = "LanguageButton";
        var rt = (RectTransform)row.transform;
        rt.anchoredPosition = ((RectTransform)restart).anchoredPosition + new Vector2(0f, -100f);
        rt.sizeDelta = new Vector2(Mathf.Max(rt.sizeDelta.x, 520f), rt.sizeDelta.y);
        row.GetComponent<Image>().color = new Color(0.25f, 0.45f, 0.35f);
        var label = row.GetComponentInChildren<TMP_Text>(true);
        var lt = label.GetComponent<LocalizedText>();
        if (lt != null) Object.DestroyImmediate(lt);
        if (label.GetComponent<LocalizedFont>() == null) label.gameObject.AddComponent<LocalizedFont>();
        label.text = "Ngôn ngữ: Tiếng Việt";

        so.FindProperty("languageButton").objectReferenceValue = row.GetComponent<TapButton>();
        so.FindProperty("languageLabel").objectReferenceValue = label;
        var rows = so.FindProperty("rows");
        rows.arraySize++;
        rows.GetArrayElementAtIndex(rows.arraySize - 1).objectReferenceValue = row.GetComponent<Outline>();
        so.ApplyModifiedPropertiesWithoutUndo();
        report.Add("PausePopup: added the language row.");
        return true;
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform child in t)
        {
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // Text fields in assets: replace a text that is in the table by its key.
    static IEnumerable<string> KeysInAssets()
    {
        var done = new List<string>();
        string ToKey(string value)
        {
            if (string.IsNullOrEmpty(value) || Loc.Has(value)) return value;
            return Loc.KeyOf(value) ?? value;
        }

        var enemy = EnemySettings.LoadOrDefault();
        var key = ToKey(enemy.EliteAnnounce);
        if (key != enemy.EliteAnnounce)
        {
            enemy.EliteAnnounce = key;
            EditorUtility.SetDirty(enemy);
            done.Add("EnemySettings.EliteAnnounce -> " + key);
        }

        var timeline = SpawnTimeline.LoadOrDefault();
        foreach (var e in timeline.Events)
        {
            key = ToKey(e.Announce);
            if (key == e.Announce) continue;
            e.Announce = key;
            EditorUtility.SetDirty(timeline);
            done.Add($"SpawnTimeline \"{e.Note}\" -> {key}");
        }

        foreach (var pig in Resources.LoadAll<PigMaceData>("Upgrades/Companions"))
        {
            // The pig texts have a number in them, so they don't match the table: set the keys directly.
            if (pig.AtkText != "buff.atk" || pig.HpText != "buff.hp")
            {
                pig.AtkText = "buff.atk";
                pig.HpText = "buff.hp";
                EditorUtility.SetDirty(pig);
                done.Add(pig.name + ": buff texts -> buff.atk / buff.hp");
            }
        }
        return done;
    }
}
