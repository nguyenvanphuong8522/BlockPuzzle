using Survivor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Menu "Survivor/Setup Hud Speed Button": adds a round "x2" button next to the pause button of the HUD prefab
// (same look as the pause button) and wires it to HudView. Pressing it toggles normal / double game speed;
// it turns gold while fast. Safe to run again (does nothing when the button exists). Move it freely afterwards.
public static class HudSpeedButtonSetup
{
    const string HudPath = "Assets/Prefabs/UI/Hud.prefab";

    [MenuItem("Survivor/Setup Hud Speed Button")]
    public static void Setup()
    {
        var root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            var hud = root.GetComponent<HudView>();
            var so = new SerializedObject(hud);
            if (so.FindProperty("speedButton").objectReferenceValue != null)
            {
                Debug.Log("HUD speed button already there.");
                return;
            }
            var pause = (TapButton)so.FindProperty("pauseButton").objectReferenceValue;
            if (pause == null)
            {
                Debug.LogError("HudView has no pause button to copy.");
                return;
            }

            var button = Object.Instantiate(pause.gameObject, pause.transform.parent);
            button.name = "SpeedButton";
            for (var i = button.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(button.transform.GetChild(i).gameObject);

            // Next to the pause button, on the inner side of the screen.
            var prt = (RectTransform)pause.transform;
            var rt = (RectTransform)button.transform;
            var step = prt.sizeDelta.x + 16f;
            rt.anchoredPosition = prt.anchoredPosition + new Vector2(prt.anchorMin.x < 0.5f ? step : -step, 0f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(button.transform, false);
            var lrt = (RectTransform)labelGo.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var anyText = root.GetComponentInChildren<TMP_Text>(true);
            if (anyText != null) label.font = anyText.font;
            label.text = "x2";
            label.fontSize = 36;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            labelGo.AddComponent<LocalizedFont>();

            so.FindProperty("speedButton").objectReferenceValue = button.GetComponent<TapButton>();
            so.FindProperty("speedBackground").objectReferenceValue = button.GetComponent<Image>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            Debug.Log("HUD speed button added next to the pause button.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
