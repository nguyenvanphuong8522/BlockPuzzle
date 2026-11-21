using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Build entry points. From the menu "Build/...", or from the command line:
//   Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildWindows
public static class BuildScript
{
    private const string AndroidOutput = "Build/Android/BlockPuzzle.apk";

    [MenuItem("Build/Build Android (CI)")]
    public static void BuildAndroid()
    {
        // Codemagic passes BUILD_NUMBER; fall back to the current value locally.
        var buildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER");
        if (int.TryParse(buildNumber, out var code))
            PlayerSettings.Android.bundleVersionCode = code;

        EditorUserBuildSettings.buildAppBundle = false;
        Build(BuildTarget.Android, AndroidOutput, BuildOptions.None);
    }

    // Release .exe: no debug panel.
    [MenuItem("Build/Build Windows (Release)")]
    public static void BuildWindows() =>
        Build(BuildTarget.StandaloneWindows64, WindowsOutput("Windows"), BuildOptions.None);

    // Development .exe: includes the in-game debug panel (F3) and the profiler connection.
    [MenuItem("Build/Build Windows (Development)")]
    public static void BuildWindowsDevelopment() =>
        Build(BuildTarget.StandaloneWindows64, WindowsOutput("WindowsDev"), BuildOptions.Development);

    static string WindowsOutput(string folder) => $"Build/{folder}/{PlayerSettings.productName}.exe";

    static void Build(BuildTarget target, string output, BuildOptions buildOptions)
    {
        var options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = output,
            target = target,
            options = buildOptions
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Build result: {report.summary.result}, output: {output}, size: {report.summary.totalSize} bytes");

        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
        if (!Application.isBatchMode && report.summary.result == BuildResult.Succeeded)
            EditorUtility.RevealInFinder(output);
    }
}
