using TMPro;
using UnityEngine;

namespace Survivor
{
    // Put on a TextMeshPro text with a fixed label: it shows the translation of `key` and switches font with the
    // language. For a label with numbers, set Args from code (SetArgs) and write {0} {1} in the CSV.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [Tooltip("Key in Localization.csv, e.g. pause.title")]
        public string Key;

        TMP_Text text;
        TMP_FontAsset originalFont;
        Material originalMaterial;
        object[] args;

        void Awake()
        {
            text = GetComponent<TMP_Text>();
            originalFont = text.font;
            originalMaterial = text.fontSharedMaterial;
        }

        void OnEnable()
        {
            Loc.Changed += Refresh;
            Refresh();
        }

        void OnDisable() => Loc.Changed -= Refresh;

        public void SetKey(string key, params object[] formatArgs)
        {
            Key = key;
            args = formatArgs != null && formatArgs.Length > 0 ? formatArgs : null;
            Refresh();
        }

        public void SetArgs(params object[] formatArgs)
        {
            args = formatArgs;
            Refresh();
        }

        public void Refresh()
        {
            if (text == null) return;
            Loc.ApplyFont(text, originalFont, originalMaterial);
            if (string.IsNullOrEmpty(Key)) return;
            text.text = args != null ? Loc.Format(Key, args) : Loc.Get(Key);
        }
    }
}
