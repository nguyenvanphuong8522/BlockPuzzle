using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Survivor
{
    // Translations: Loc.Get("hud.kills") -> "Hạ gục" / "Kills"; Loc.Format("hud.level", 3) -> "Cấp 3" / "Level 3".
    // Texts in the scene / prefabs use the LocalizedText component; texts set from code call Loc.Get and listen to
    // Loc.Changed (fired when the player switches language) to refresh. The table is a CSV (see LocalizationSettings).
    // A key missing in the current language falls back to the fallback language, then to the given default text,
    // then to the key itself (so a missing translation is easy to spot).
    public static class Loc
    {
        const string PrefsKey = "language";

        static LocalizationSettings settings;
        static readonly Dictionary<string, string[]> table = new Dictionary<string, string[]>(StringComparer.Ordinal);
        static readonly HashSet<string> reportedMissing = new HashSet<string>();
        static readonly Dictionary<string, string> byText = new Dictionary<string, string>(StringComparer.Ordinal);
        static int current = -1, fallback = -1; // column indexes in the table rows
        static LocalizationSettings.Language currentLanguage;

        // Fired after the language changed (texts should refresh).
        public static event Action Changed;

        public static LocalizationSettings Settings
        {
            get
            {
                EnsureLoaded();
                return settings;
            }
        }

        public static string Language
        {
            get
            {
                EnsureLoaded();
                return currentLanguage != null ? currentLanguage.Code : "";
            }
        }

        public static LocalizationSettings.Language CurrentLanguage
        {
            get
            {
                EnsureLoaded();
                return currentLanguage;
            }
        }

        // Domain reload may be off in the editor: start clean every Play.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            settings = null;
            table.Clear();
            byText.Clear();
            reportedMissing.Clear();
            current = fallback = -1;
            currentLanguage = null;
            Changed = null;
        }

        public static bool Has(string key)
        {
            EnsureLoaded();
            return key != null && table.ContainsKey(key);
        }

        // For text fields in assets that may hold either a key or plain text: the translation when it is a key,
        // otherwise the text as written.
        // Plain text that matches a cell of the table in any language (e.g. "Đàn Cua tới!") is translated too.
        public static string Text(string keyOrText)
        {
            if (string.IsNullOrEmpty(keyOrText)) return "";
            if (Has(keyOrText)) return Get(keyOrText);
            var key = KeyOf(keyOrText);
            return key != null ? Get(key) : keyOrText;
        }

        // The key whose text (in any language) is exactly `text`, or null.
        public static string KeyOf(string text)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(text)) return null;
            if (byText.Count == 0)
                foreach (var pair in table)
                    for (var c = 2; c < pair.Value.Length; c++) // skip Key and Description
                    {
                        var cell = pair.Value[c];
                        if (!string.IsNullOrEmpty(cell) && !byText.ContainsKey(cell)) byText[cell] = pair.Key;
                    }
            return byText.TryGetValue(text, out var key) ? key : null;
        }

        public static string Get(string key) => Get(key, null);

        // `fallbackText` is shown when the key has no text at all (e.g. a designer's default in an asset).
        public static string Get(string key, string fallbackText)
        {
            if (string.IsNullOrEmpty(key)) return fallbackText ?? "";
            EnsureLoaded();
            if (table.TryGetValue(key, out var row))
            {
                var text = Cell(row, current);
                if (string.IsNullOrEmpty(text)) text = Cell(row, fallback);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            if (fallbackText != null) return fallbackText;
            ReportMissing(key);
            return key;
        }

        public static string Format(string key, params object[] args)
        {
            var pattern = Get(key);
            try
            {
                return string.Format(pattern, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"Loc: bad format in \"{key}\" ({Language}): {pattern}");
                return pattern;
            }
        }

        public static void SetLanguage(string code)
        {
            EnsureLoaded();
            var language = settings.Find(code);
            if (language == null || language == currentLanguage) return;
            Apply(language);
            PlayerPrefs.SetString(PrefsKey, language.Code);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // Next language in the list (for a simple "Language: X" button).
        public static void CycleLanguage(int step = 1)
        {
            EnsureLoaded();
            var list = settings.Languages;
            if (list.Count == 0) return;
            var i = Mathf.Max(0, list.IndexOf(currentLanguage));
            SetLanguage(list[((i + step) % list.Count + list.Count) % list.Count].Code);
        }

        // Sets the current language's font (if it has one) on a text; `original` is the text's own font to use
        // when the language has none. LocalizedText / LocalizedFont call this; code-made texts can too.
        public static void ApplyFont(TMP_Text text, TMP_FontAsset original, Material originalMaterial)
        {
            if (text == null) return;
            var language = CurrentLanguage;
            var font = language != null && language.Font != null ? language.Font : original;
            if (font == null) return;
            if (text.font != font) text.font = font;
            var material = language != null && language.Font != null
                ? (language.FontMaterial != null ? language.FontMaterial : font.material)
                : (originalMaterial != null ? originalMaterial : font.material);
            if (material != null && text.fontSharedMaterial != material) text.fontSharedMaterial = material;
        }

        // Re-reads the CSV (editor tools; also handy after editing the table during Play).
        public static void Reload()
        {
            var code = currentLanguage != null ? currentLanguage.Code : null;
            settings = null;
            table.Clear();
            byText.Clear();
            reportedMissing.Clear();
            EnsureLoaded();
            if (code != null && settings.Find(code) != null) Apply(settings.Find(code));
            Changed?.Invoke();
        }

        static string Cell(string[] row, int column) =>
            column >= 0 && column < row.Length ? row[column] : null;

        static void EnsureLoaded()
        {
            if (settings != null) return;
            settings = LocalizationSettings.LoadOrDefault();
            LoadTable();

            var saved = PlayerPrefs.GetString(PrefsKey, "");
            var language = settings.Find(saved);
            if (language == null && settings.UseDeviceLanguage)
                foreach (var l in settings.Languages)
                    if (l != null && Array.IndexOf(l.SystemLanguages, Application.systemLanguage) >= 0)
                    {
                        language = l;
                        break;
                    }
            if (language == null) language = settings.Find(settings.DefaultLanguage);
            if (language == null && settings.Languages.Count > 0) language = settings.Languages[0];
            Apply(language);
        }

        static string[] header = Array.Empty<string>();

        static void LoadTable()
        {
            table.Clear();
            byText.Clear();
            header = Array.Empty<string>();
            if (settings.Table == null)
            {
                Debug.LogWarning("Loc: no translation table (Assets/Resources/Localization/Localization.csv).");
                return;
            }
            var rows = CsvReader.Parse(settings.Table.text);
            if (rows.Count == 0) return;
            header = rows[0].ToArray();
            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0) continue;
                var key = row[0].Trim();
                if (key.Length == 0 || key.StartsWith("#")) continue; // empty line / comment
                var cells = row.ToArray();
                for (var c = 0; c < cells.Length; c++) cells[c] = cells[c].Replace("\\n", "\n");
                table[key] = cells;
            }
        }

        static int ColumnOf(LocalizationSettings.Language language)
        {
            if (language == null) return -1;
            for (var c = 0; c < header.Length; c++)
                if (string.Equals(header[c].Trim(), language.Column, StringComparison.OrdinalIgnoreCase)) return c;
            Debug.LogWarning($"Loc: the CSV has no column \"{language.Column}\" for language {language.Code}.");
            return -1;
        }

        static void Apply(LocalizationSettings.Language language)
        {
            currentLanguage = language;
            current = ColumnOf(language);
            fallback = ColumnOf(settings.Find(settings.FallbackLanguage));
        }

        static void ReportMissing(string key)
        {
            if (!settings.LogMissingKeys || !Debug.isDebugBuild || !reportedMissing.Add(key)) return;
            Debug.LogWarning($"Loc: missing key \"{key}\" (add it to Localization.csv).");
        }
    }
}
