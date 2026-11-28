using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
            [Tooltip("Font for this language (TextMeshPro font asset). Empty = keep each text's own font. " +
                     "Japanese / Chinese / Korean need a font that has their characters.")]
            public TMP_FontAsset Font;
            [Tooltip("Optional material for that font (outline / shadow). Empty = the font's default material.")]
            public Material FontMaterial;
            [Tooltip("Devices set to these system languages start in this language (first launch only).")]
            public SystemLanguage[] SystemLanguages = Array.Empty<SystemLanguage>();
        }

        [Tooltip("The translation table (CSV, UTF-8).")]
        public TextAsset Table;

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
