using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Explicit read-only investigation. Never builds, renders, bakes meshes or enters Play mode.
[InitializeOnLoad]
public static class WebGLPerformanceInspection
{
    const string Request = "Temp/WebGLPerformanceInspection.request";
    static WebGLPerformanceInspection() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        try
        {
            File.Delete(Request);
            Inspect();
        }
        catch (Exception e) { File.WriteAllText("StageReview/WebGLPerformance/inspection-error.txt", e.ToString()); }
    }
    static void Inspect()
    {
        var report = new StringBuilder();
        report.AppendLine("Read-only Editor inventory; loaded assets only, not browser timings.");
        foreach (var skin in Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>()
            .Where(s => s.sharedMesh != null).OrderByDescending(s => s.sharedMesh.vertexCount))
            report.AppendLine($"SKIN vertices={skin.sharedMesh.vertexCount}, bones={skin.bones.Length}, submeshes={skin.sharedMesh.subMeshCount}; {AssetDatabase.GetAssetPath(skin.sharedMesh)}; {skin.name}");
        foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            report.AppendLine($"FONT {font.name}; mode={font.atlasPopulationMode}; atlases={font.atlasTextureCount}");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var go = new GameObject("Read-only card geometry probe", typeof(RectTransform));
            go.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var graphic = go.AddComponent<CardDesignGraphic>();
            var populate = typeof(CardDesignGraphic).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (float highlight in new[] { 0f, 1f })
            {
                graphic.highlight = highlight;
                using (var vh = new VertexHelper())
                {
                    populate.Invoke(graphic, new object[] { vh });
                    report.AppendLine($"CARD background highlight={highlight}; vertices={vh.currentVertCount}; indices={vh.currentIndexCount}");
                }
            }
            using (var vh = new VertexHelper())
            {
                CardDesignGraphic.DrawCostBadge(vh);
                report.AppendLine($"CARD cost badge vertices={vh.currentVertCount}; indices={vh.currentIndexCount}");
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        File.WriteAllText("StageReview/WebGLPerformance/editor-inventory.txt", report.ToString());
    }
}
