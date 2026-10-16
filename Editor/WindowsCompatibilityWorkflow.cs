using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class WindowsCompatibilityWorkflow
{
    const string Request = "Temp/WindowsCompatibility.request";
    const string Folder = "StageReview/WindowsCompatibility";
    const string Session = "WindowsCompatibilityRunning";
    static EditorWindow gameView;
    static object sizeGroup;

    static WindowsCompatibilityWorkflow()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Session, false))
            {
                WindowsCompatibilityProbe.ResizeEditor = Resize;
                WindowsCompatibilityProbe.FinishedEditor = () => EditorApplication.ExitPlaymode();
                new GameObject("Compatibility audit").AddComponent<WindowsCompatibilityProbe>();
            }
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Session, false))
            {
                SessionState.SetBool(Session, false);
                var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                var view = EditorWindow.GetWindow(type);
                type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(view, SessionState.GetInt(Session + ".Size", 0));
            }
        };
    }

    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
        Directory.CreateDirectory(Folder);
        try
        {
            if (command == "inspect") Inspect();
            else if (command == "test") Begin();
            else if (command == "build") Build();
            else if (command == "build-release") BuildRelease();
            else if (command == "frame-steps") FrameStepVerification.Run();
            else throw new ArgumentException(command);
        }
        catch (Exception e) { File.WriteAllText(Folder + "/workflow-error.txt", e.ToString()); Debug.LogException(e); }
    }

    static void Inspect()
    {
        var report = new StringBuilder();
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup()) report.AppendLine($"SCENE {scene.path}; active={scene.isActive}; loaded={scene.isLoaded}; dirty={SceneManager.GetSceneByPath(scene.path).isDirty}");
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>().Where(c => c.gameObject.scene.IsValid()))
        {
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            report.AppendLine($"CANVAS {canvas.name}; mode={canvas.renderMode}; root={canvas.isRootCanvas}; reference={(scaler ? scaler.referenceResolution.ToString() : "none")}; active={canvas.gameObject.activeInHierarchy}");
        }
        report.AppendLine($"Windows64 support={BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)}; backend={PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone)}");
        report.AppendLine("API " + string.Join(",", PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64)));
        report.AppendLine($"GPU {SystemInfo.graphicsDeviceName}; VRAM={SystemInfo.graphicsMemorySize}; RAM={SystemInfo.systemMemorySize}");
        File.WriteAllText(Folder + "/inspection.txt", report.ToString());
    }

    [MenuItem("Tools/Compatibility/Run Windows display checks")]
    public static void Begin()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        string scenePath = EditorBuildSettings.scenes.Single(s => s.enabled).path;
        var games = UnityEngine.Object.FindObjectsOfType<GameManager>(true);
        if (games.Length != 1 || games[0].gameObject.scene.path != scenePath)
            throw new InvalidOperationException("Open the enabled build scene as the only gameplay scene before testing.");
        var type = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var view = EditorWindow.GetWindow(type);
        int index = (int)type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
        SessionState.SetInt(Session + ".Size", index);
        SessionState.SetBool(Session, true);
        EditorApplication.EnterPlaymode();
    }

    static void Resize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singletonType.GetProperty("instance").GetValue(null);
        var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
        sizeGroup = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
        var sizeType = assembly.GetType("UnityEditor.GameViewSize");
        var kind = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
        var size = Activator.CreateInstance(sizeType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, new object[] { kind, width, height, "Compatibility audit" }, null);
        sizeGroup.GetType().GetMethod("AddCustomSize").Invoke(sizeGroup, new[] { size });
        int index = (int)sizeGroup.GetType().GetMethod("GetTotalCount").Invoke(sizeGroup, null) - 1;
        var type = assembly.GetType("UnityEditor.GameView");
        gameView = EditorWindow.GetWindow(type);
        type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(gameView, index);
        gameView.Repaint();
    }

    [MenuItem("Tools/Compatibility/Build Windows test player")]
    public static void Build() { BuildPlayer(true); }

    [MenuItem("Tools/Compatibility/Build Windows release player")]
    public static void BuildRelease() { BuildPlayer(false); }

    static void BuildPlayer(bool development)
    {
        string path = Folder + (development ? "/Build/Game.exe" : "/Release/Game.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.DetailedBuildReport | (development ? BuildOptions.Development : BuildOptions.None)
        });
        var summary = result.summary;
        string reportName = development ? "build" : "build-release";
        File.WriteAllText(Folder + "/" + reportName + ".txt", $"{summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; bytes={summary.totalSize}; seconds={summary.totalTime.TotalSeconds}\n");
        File.WriteAllText(Folder + "/" + reportName + "-messages.txt", string.Join("\n", result.steps.SelectMany(s => s.messages)
            .Where(m => m.type == LogType.Error || m.type == LogType.Warning).Select(m => m.type + " " + m.content)));
        if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows player build failed.");
    }
}
