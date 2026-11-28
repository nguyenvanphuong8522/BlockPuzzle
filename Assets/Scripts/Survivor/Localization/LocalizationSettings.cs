using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Survivor
{
    // Languages of the game: Assets/Resources/LocalizationSettings.asset.
    // Translations live in a CSV table (Assets/Resources/Localization/Localization.csv), one row per key and one
    // column per language: Key, Description, English, Vietnamese, Japanese... Edit it in Google Sheets / Excel and
    // save as CSV (UTF-8). To add a language: add a column to the CSV and a line here (Column = the column title).
    [CreateAssetMenu(menuName = "Survivor/Localization Settings", fileName = "LocalizationSettings")]
    public sealed class LocalizationSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Language
        {
            [Tooltip("Short code used in code and saved settings: vi, en, ja...")]
            public string Code = "en";
            [Tooltip("Title of this language's column in the CSV.")]
            public string Column = "English";
            [Tooltip("Name shown in the language picker, in that language (Tiếng Việt, English, 日本語).")]
            public string NativeName = "English";
            [Tooltip("How texts change font / material in this language. Each text checks the rules from the top and " +
                     "uses the FIRST one matching its own font (and material); put specific rules above general ones. " +
                     "No match = the text keeps its own font. Empty list = no change (e.g. Vietnamese / English).")]
            public List<FontRule> Fonts = new List<FontRule>();
            [Tooltip("Devices set to these system languages start in this language (first launch only).")]
            public SystemLanguage[] SystemLanguages = Array.Empty<SystemLanguage>();
        }

        // "Texts using font A (with material M) become font B with material N in this language."
        // The replacement font / material are Addressables, loaded only while this language is on and released when
        // the player switches away (big Japanese / Chinese fonts don't sit in memory for nothing). Dragging an asset
        // into the field marks it Addressable automatically.
        [Serializable]
        public sealed class FontRule
        {
            [Tooltip("Original font of the texts this rule changes. Empty = any font.")]
            public TMP_FontAsset FromFont;
            [Tooltip("Only texts using this material preset (e.g. the outline one). Empty = any material.")]
            public Material FromMaterial;
            [Tooltip("Font to use instead. Empty = keep the text's font (only change the material).")]
            public AssetReferenceT<TMP_FontAsset> ToFont = new AssetReferenceT<TMP_FontAsset>("");
            [Tooltip("Material preset to use. It must be made for the font above (or for the text's own font when " +
                     "To Font is empty). Empty = that font's default material.")]
            public AssetReferenceT<Material> ToMaterial = new AssetReferenceT<Material>("");

            public bool Matches(TMP_FontAsset font, Material material) =>
                (FromFont == null || FromFont == font) && (FromMaterial == null || FromMaterial == material);
        }

        [Tooltip("The translation table (CSV, UTF-8).")]
        public TextAsset Table;

        [Tooltip("Google Sheet with the same columns (Key, Description, English, Vietnamese...). Paste the normal link " +
                 "from the browser (…/spreadsheets/d/<id>/edit#gid=<tab>) or a \"Publish to web\" CSV link. The sheet " +
                 "must be shared as \"Anyone with the link can view\" (or published). Then use the menu " +
                 "Survivor > Localization > Download from Google Sheet. Editor only: the game never goes online.")]
        public string GoogleSheetUrl = "";

        public List<Language> Languages = new List<Language>
        {
            new Language { Code = "vi", Column = "Vietnamese", NativeName = "Tiếng Việt", SystemLanguages = new[] { SystemLanguage.Vietnamese } },
            new Language { Code = "en", Column = "English", NativeName = "English", SystemLanguages = new[] { SystemLanguage.English } },
        };

        [Tooltip("First launch: pick the language of the device when it is in the list above.")]
        public bool UseDeviceLanguage = true;
        [Tooltip("Language when the device language is not in the list (or Use Device Language is off).")]
        public string DefaultLanguage = "en";
        [Tooltip("A key with no text in the current language shows this language's text instead.")]
        public string FallbackLanguage = "en";
        [Tooltip("Editor / development builds: log every missing key once.")]
        public bool LogMissingKeys = true;

        public Language Find(string code)
        {
            foreach (var l in Languages)
                if (l != null && string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) return l;
            return null;
        }

        public static LocalizationSettings LoadOrDefault()
        {
            var settings = Resources.Load<LocalizationSettings>("LocalizationSettings");
            if (settings == null) settings = CreateInstance<LocalizationSettings>();
            if (settings.Table == null) settings.Table = Resources.Load<TextAsset>("Localization/Localization");
            return settings;
        }
    }
}
