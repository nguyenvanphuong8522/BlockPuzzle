using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Build entry points. From the menu "Build/...", or from the command line:
//   Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildWindows
public static class BuildScript
{
    private const string AndroidOutput = "Build/Android/PalRush.apk";

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

    // The game is portrait on every platform.
    static void ApplyPortraitSettings()
    {
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait; // Android / iOS
        PlayerSettings.defaultWebScreenWidth = 540;                         // WebGL canvas
        PlayerSettings.defaultWebScreenHeight = 960;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;            // Windows: portrait window
        PlayerSettings.defaultScreenWidth = 720;
        PlayerSettings.defaultScreenHeight = 1280;
        PlayerSettings.resizableWindow = true;
    }

    // Exe named after the game; characters Windows forbids in file names (: ? * ...) are dropped.
    static string WindowsOutput(string folder)
    {
        var name = string.Concat(PlayerSettings.productName.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
        return $"Build/{folder}/{(name.Length > 0 ? name : "Game")}.exe";
    }

    // WebGL: open Build/WebGL/index.html through a web server (not by double-clicking the file).
    [MenuItem("Build/Build WebGL")]
    public static void BuildWebGL()
    {
        // Brotli = smallest download; the fallback lets it run on any server, even without special headers.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        Build(BuildTarget.WebGL, "Build/WebGL", BuildOptions.None);
    }

    // In the Editor (menu), switching platform is not instant: Unity recompiles the scripts after this call
    // returns, and building Addressables during that fails ("Unable to build with the current configuration").
    // So: switch, remember the build, and run it once the Editor has reloaded on the new platform.
    const string PendingKey = "BuildScript.Pending";

    [InitializeOnLoadMethod]
    static void ResumePendingBuild()
    {
        var pending = SessionState.GetString(PendingKey, "");
        if (string.IsNullOrEmpty(pending)) return;
        var parts = pending.Split('|');
        if (parts.Length != 3 || !Enum.TryParse(parts[0], out BuildTarget target) || !int.TryParse(parts[2], out var opts)) return;
        // Wait until the Editor is idle (done compiling / importing) on the new platform.
        var notBefore = EditorApplication.timeSinceStartup + 3.0;  // let the recompile start first
        var giveUpAt = EditorApplication.timeSinceStartup + 120.0;
        EditorApplication.CallbackFunction run = null;
        run = () =>
        {
            if (EditorApplication.timeSinceStartup < notBefore || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                if (EditorApplication.timeSinceStartup < giveUpAt) return;
                EditorApplication.update -= run;
                SessionState.EraseString(PendingKey);
                Debug.LogError($"Build cancelled: the platform did not switch to {target}.");
                return;
            }
            EditorApplication.update -= run;
            if (SessionState.GetString(PendingKey, "") != pending) return; // already started elsewhere
            SessionState.EraseString(PendingKey);
            Build(target, parts[1], (BuildOptions)opts);
        };
        EditorApplication.update += run;
    }

    static void Build(BuildTarget target, string output, BuildOptions buildOptions)
    {
        ApplyPortraitSettings();

        // Popups are Addressables: their content must be built for this platform before the player.
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            if (!Application.isBatchMode)
            {
                SessionState.SetString(PendingKey, $"{target}|{output}|{(int)buildOptions}");
                Debug.Log($"Switching platform to {target}; the build starts by itself when the Editor is ready.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
                ResumePendingBuild(); // in case no script reload happens; after a reload [InitializeOnLoadMethod] picks it up
                return;
            }
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(target), target);
        }
        // The platform highlighted in the Build Profiles window must match too: Addressables checks the build
        // settings of the *selected* platform (e.g. Android = IL2CPP) and refuses to build when it differs.
        var group = BuildPipeline.GetBuildTargetGroup(target);
        EditorUserBuildSettings.selectedBuildTargetGroup = group;
        if (group == BuildTargetGroup.Standalone) EditorUserBuildSettings.selectedStandaloneTarget = target;

        AddressableAssetSettings.BuildPlayerContent(out var addressablesResult);
        if (!string.IsNullOrEmpty(addressablesResult.Error))
        {
            Debug.LogError("Addressables build failed: " + addressablesResult.Error);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

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
        if (report.summary.result != BuildResult.Succeeded) return;

        // Windows release: also pack exactly what players need into one zip, ready to send.
        var zip = target == BuildTarget.StandaloneWindows64 && (buildOptions & BuildOptions.Development) == 0
            ? ZipWindowsBuild(output) : null;
        if (!Application.isBatchMode) EditorUtility.RevealInFinder(zip ?? output);
    }

    // Zips the game next to the build folder: Build/<ProductName>_Windows.zip, with everything inside one folder.
    // Left out: Unity's "do not ship" debug folders and leftovers from older builds under another name
    // (other .exe files and their _Data folders).
    static string ZipWindowsBuild(string exePath)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(exePath));
            var exeName = System.IO.Path.GetFileNameWithoutExtension(exePath);
            var folderName = exeName.Replace(" ", "");
            var zipPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dir), folderName + "_Windows.zip");
            if (System.IO.File.Exists(zipPath)) System.IO.File.Delete(zipPath);

            bool Ship(string relative)
            {
                var top = relative.Split('/', '\\')[0];
                if (top.Contains("DoNotShip") || top.Contains("DontShip")) return false;
                if (top.EndsWith("_Data")) return top == exeName + "_Data";
                if (top.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return top == exeName + ".exe" || top.StartsWith("UnityCrashHandler");
                return true;
            }

            var count = 0;
            using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (var file in System.IO.Directory.GetFiles(dir, "*", System.IO.SearchOption.AllDirectories))
                {
                    var relative = file.Substring(dir.Length + 1);
                    if (!Ship(relative)) continue;
                    System.IO.Compression.ZipFileExtensions.CreateEntryFromFile(archive, file,
                        folderName + "/" + relative.Replace('\\', '/'), System.IO.Compression.CompressionLevel.Optimal);
                    count++;
                }
            }
            var mb = new System.IO.FileInfo(zipPath).Length / (1024f * 1024f);
            Debug.Log($"Zipped {count} files for sharing: {zipPath} ({mb:0.0} MB)");
            return zipPath;
        }
        catch (Exception e)
        {
            Debug.LogError("Could not zip the Windows build: " + e.Message);
            return null;
        }
    }
}
