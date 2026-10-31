using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Entry point for CI builds (Codemagic): Unity -executeMethod BuildScript.BuildAndroid
public static class BuildScript
{
    private const string AndroidOutput = "Build/Android/BlockPuzzle.apk";

    [MenuItem("Build/Build Android (CI)")]
    public static void BuildAndroid()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();

        // Codemagic passes BUILD_NUMBER; fall back to the current value locally.
        var buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER");
        if (int.TryParse(buildNumber, out var code))
            PlayerSettings.Android.bundleVersionCode = code;

        EditorUserBuildSettings.buildAppBundle = false;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = AndroidOutput,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Build result: {report.summary.result}, size: {report.summary.totalSize} bytes");

        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }
}
