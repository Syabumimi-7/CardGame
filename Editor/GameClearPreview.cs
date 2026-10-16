using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Render the real clear-screen factory without starting or completing a run.</summary>
[InitializeOnLoad]
public static class GameClearPreview
{
    const string Request = "Temp/GameClearPreview.request";
    const string ImagePath = "StageReview/game-clear-preview.png";

    static GameClearPreview() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        try { Capture(); File.WriteAllText("Temp/GameClearPreview.result", "PASS " + ImagePath); }
        catch (Exception e) { File.WriteAllText("Temp/GameClearPreview.result", e.ToString()); Debug.LogException(e); }
    }

    [MenuItem("Tools/Preview/クリア画面を撮影")]
    public static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("再生を停止してから撮影してください。");
        Directory.CreateDirectory("StageReview");
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var ownerRoot = new GameObject("Clear preview owner"); ownerRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(ownerRoot, scene);
            var owner = ownerRoot.AddComponent<GameManager>();
            var root = new GameObject("Clear preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 900);
            var progression = root.AddComponent<BattleProgressionUI>();
            progression.Configure(owner, null, null, root);
            typeof(BattleProgressionUI).GetMethod("ShowGameClear", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(progression, null);
            var cameraRoot = new GameObject("Clear preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.GetComponent<Camera>(); camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.orthographic = true; camera.orthographicSize = 450;
            camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
            var labels = root.GetComponentsInChildren<TMP_Text>().Select(t => t.text).ToArray();
            if (!labels.Contains("GAME CLEAR!") || !labels.Contains("タイトルにもどる")) throw new InvalidOperationException("クリア画面の生成に失敗しました。");
            RuinsStageBuilder.Capture(camera, ImagePath, 1600, 900);
            Debug.Log("クリア画面を撮影しました: " + ImagePath);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
