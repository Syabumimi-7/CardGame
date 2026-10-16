using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
public static class WebGLBuildWorkflow
{
    private const string Request = "Temp/WebGLBuild.request";
    private const string ReportFolder = "StageReview/WebGL";

    static WebGLBuildWorkflow() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        File.Delete(Request);
        try { Build(); }
        catch (Exception e)
        {
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/error.txt", e.ToString());
            Debug.LogException(e);
        }
    }

    [MenuItem("Tools/Build/Build WebGL")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before building.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new InvalidOperationException("Install WebGL Build Support for this Unity version in Unity Hub.");
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0) throw new InvalidOperationException("No scenes enabled in Build Settings.");

        PlayerSettings.WebGL.template = "PROJECT:GameFullscreen";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.dataCaching = true;
        Directory.CreateDirectory(ReportFolder);
        File.WriteAllText(ReportFolder + "/status.txt", "Building WebGL: " + DateTime.Now.ToString("s"));
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Builds/WebGL",
            target = BuildTarget.WebGL,
            options = BuildOptions.DetailedBuildReport
        });
        var summary = result.summary;
        File.WriteAllText(ReportFolder + "/build.txt",
            $"{summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; bytes={summary.totalSize}; seconds={summary.totalTime.TotalSeconds}\n");
        File.WriteAllText(ReportFolder + "/messages.txt", string.Join("\n", result.steps.SelectMany(s => s.messages)
            .Where(m => m.type == LogType.Error || m.type == LogType.Warning).Select(m => m.type + " " + m.content)));
        File.WriteAllText(ReportFolder + "/status.txt", summary.result.ToString());
        if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("WebGL build failed; see StageReview/WebGL.");
        Debug.Log("WebGL build ready: Builds/WebGL/index.html");
    }
}
