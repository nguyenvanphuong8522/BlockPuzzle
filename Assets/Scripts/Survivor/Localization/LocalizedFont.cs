using TMPro;
using UnityEngine;

namespace Survivor
{
    // Put on a text whose words are set from code (HUD counters, card descriptions...): it only switches the font
    // with the language (e.g. a Japanese font), the code sets the text with Loc.Get / Loc.Format.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedFont : MonoBehaviour
    {
        TMP_Text text;
        TMP_FontAsset originalFont;
        Material originalMaterial;

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

        void Refresh() => Loc.ApplyFont(text, originalFont, originalMaterial);
    }
}
