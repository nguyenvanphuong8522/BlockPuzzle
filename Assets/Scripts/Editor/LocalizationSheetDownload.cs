using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Survivor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

// Menu "Survivor/Localization/Download from Google Sheet": downloads the sheet set in LocalizationSettings
// (Google Sheet Url) as CSV, checks it, overwrites Assets/Resources/Localization/Localization.csv and reloads the
// texts (also during Play). One way only: edit the texts in the sheet, not in the CSV file.
public static class LocalizationSheetDownload
{
    const string TablePath = "Assets/Resources/Localization/Localization.csv";
    const int TimeoutSeconds = 30;

    [MenuItem("Survivor/Localization/Download from Google Sheet")]
    public static void Download()
    {
        var settings = LocalizationSettings.LoadOrDefault();
        var url = (settings.GoogleSheetUrl ?? "").Trim();
        if (url.Length == 0)
        {
            Fail("No Google Sheet link yet.\n\nPaste it in Assets/Resources/LocalizationSettings.asset > Google Sheet Url.");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<LocalizationSettings>("Assets/Resources/LocalizationSettings.asset");
            return;
        }

        var csvUrl = ToCsvUrl(url);
        if (csvUrl == null)
        {
            Fail("This does not look like a Google Sheets link:\n" + url);
            return;
        }

        string text;
        using (var request = UnityWebRequest.Get(csvUrl))
        {
            request.timeout = TimeoutSeconds;
            var op = request.SendWebRequest();
            try
            {
                while (!op.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Localization", "Downloading the Google Sheet…",
                            request.downloadProgress))
                    {
                        request.Abort();
                        return;
                    }
                    System.Threading.Thread.Sleep(50);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                Fail($"Download failed: {request.error}\n\nIs the sheet shared as \"Anyone with the link can view\" (or published to the web)?");
                return;
            }
            text = Encoding.UTF8.GetString(request.downloadHandler.data);
        }

        // A private sheet answers with a Google sign-in page (HTML), not CSV.
        if (text.TrimStart().StartsWith("<"))
        {
            Fail("Google returned a web page instead of the table: the sheet is probably private.\n\n" +
                 "In Google Sheets: Share > General access > \"Anyone with the link\" (Viewer), or File > Share > Publish to web.");
            return;
        }

        var problems = Check(text, settings, out var keys);
        if (problems.Count > 0 && problems[0].StartsWith("!"))
        {
            Fail("The sheet was not used:\n" + string.Join("\n", problems));
            return;
        }

        var old = File.Exists(TablePath) ? File.ReadAllText(TablePath) : "";
        var normalized = text.TrimStart('﻿').Replace("\r\n", "\n");
        if (old.Replace("\r\n", "\n") == normalized)
        {
            Report($"No changes ({keys} keys).", problems);
            return;
        }

        File.WriteAllText(TablePath, normalized, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(TablePath);
        Loc.Reload();
        Report($"Localization.csv updated from the Google Sheet: {keys} keys.", problems);
    }

    [MenuItem("Survivor/Localization/Open Google Sheet")]
    public static void Open()
    {
        var url = (LocalizationSettings.LoadOrDefault().GoogleSheetUrl ?? "").Trim();
        if (url.Length == 0) Fail("No Google Sheet link yet (LocalizationSettings > Google Sheet Url).");
        else Application.OpenURL(url);
    }

    // Accepts the browser link (…/d/<id>/edit#gid=<tab>), a "Publish to web" link (…/d/e/<id>/pub…) or a CSV link.
    static string ToCsvUrl(string url)
    {
        if (!url.Contains("docs.google.com/spreadsheets")) return null;
        if (url.Contains("output=csv") || url.Contains("format=csv")) return url;

        var gid = Regex.Match(url, @"[#&?]gid=(\d+)");
        var tab = gid.Success ? gid.Groups[1].Value : "0";

        var published = Regex.Match(url, @"/spreadsheets/d/e/([\w-]+)");
        if (published.Success)
            return $"https://docs.google.com/spreadsheets/d/e/{published.Groups[1].Value}/pub?gid={tab}&single=true&output=csv";

        var id = Regex.Match(url, @"/spreadsheets/d/([\w-]+)");
        return id.Success ? $"https://docs.google.com/spreadsheets/d/{id.Groups[1].Value}/export?format=csv&gid={tab}" : null;
    }

    // Errors start with "!" (the file is not replaced); the rest are warnings.
    static List<string> Check(string text, LocalizationSettings settings, out int keys)
    {
        var problems = new List<string>();
        keys = 0;
        var rows = CsvReader.Parse(text);
        if (rows.Count < 2)
        {
            problems.Add("! The sheet is empty.");
            return problems;
        }

        var header = rows[0];
        if (header.Count == 0 || header[0].Trim().TrimStart('﻿') != "Key")
            problems.Add("! The first column must be titled \"Key\" (found \"" + (header.Count > 0 ? header[0] : "") + "\").");
        foreach (var language in settings.Languages)
            if (language != null && !header.Exists(h => string.Equals(h.Trim(), language.Column, System.StringComparison.OrdinalIgnoreCase)))
                problems.Add($"! No column \"{language.Column}\" (language {language.Code}).");
        if (problems.Count > 0) return problems;

        var seen = new HashSet<string>();
        for (var r = 1; r < rows.Count; r++)
        {
            var key = rows[r].Count > 0 ? rows[r][0].Trim() : "";
            if (key.Length == 0 || key.StartsWith("#")) continue;
            keys++;
            if (!seen.Add(key)) problems.Add($"Key \"{key}\" appears more than once (the last one wins).");
        }
        return problems;
    }

    static void Report(string message, List<string> warnings)
    {
        if (warnings.Count > 0) message += "\n\nWarnings:\n" + string.Join("\n", warnings);
        Debug.Log("Localization: " + message);
        EditorUtility.DisplayDialog("Localization", message, "OK");
    }

    static void Fail(string message)
    {
        Debug.LogWarning("Localization: " + message);
        EditorUtility.DisplayDialog("Localization", message, "OK");
    }
}
