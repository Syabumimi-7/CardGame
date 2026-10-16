using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EnemyHelpPreview
{
    [MenuItem("Tools/Help/Verify and Preview Enemy Portraits")]
    public static void Run()
    {
        Directory.CreateDirectory("StageReview/EnemyHelp");
        var scene = EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder();
        try
        {
            var ownerRoot = new GameObject("Help preview data"); ownerRoot.SetActive(false);
            SceneManager.MoveGameObjectToScene(ownerRoot, scene);
            var game = ownerRoot.AddComponent<GameManager>();
            var battle = ownerRoot.AddComponent<BattleManager>();
            game.battleManager = battle;
            battle.playerStatus = ownerRoot.AddComponent<PlayerStatus>();
            battle.enemyManager = ownerRoot.AddComponent<EnemyManager>();
            var enemies = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/ScriptableObject/EnemyData" })
                .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(e => e.name).ToArray();
            battle.enemyManager.normalEnemies = enemies.Where(e => e.enemyType == EnemyType.Normal).ToList();
            battle.enemyManager.bossEnemies = enemies.Where(e => e.enemyType == EnemyType.Boss).ToList();
            var root = new GameObject("Help preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 900);
            game.startScreenRoot = root;
            var cameraRoot = new GameObject("Help preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraRoot, scene);
            var camera = cameraRoot.GetComponent<Camera>(); camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.orthographic = true; camera.orthographicSize = 450;
            camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            canvas.worldCamera = camera;
            var help = ownerRoot.AddComponent<HelpScreenUI>(); help.Configure(game, false); help.OpenFromPause(root.transform);
            int portraits = 0;
            for (int i = 0; i < 3; i++)
            {
                help.ShowPage(10 + i);
                Canvas.ForceUpdateCanvases();
                var images = root.GetComponentsInChildren<Image>().Where(image => image.name == "EnemyPortrait").ToArray();
                foreach (var image in images)
                {
                    Require(image.sprite && image.sprite.texture.width == 512 && image.sprite.texture.height == 512, "Missing or mismatched photograph.");
                    Require(image.rectTransform.sizeDelta == new Vector2(224, 224), "Portrait size differs.");
                }
                var descriptions = root.GetComponentsInChildren<TMP_Text>().Where(t => t.name == "EnemyDescription").ToArray();
                Require(images.Length == descriptions.Length && images.Length > 0, "Enemy layout incomplete.");
                for (int j = 0; j < images.Length; j++)
                {
                    var image = images[j].rectTransform; var text = descriptions[j].rectTransform;
                    Require(image.anchoredPosition.x == 0 && text.anchoredPosition.x == 252, "Incorrect horizontal placement.");
                    Require(Mathf.Approximately(image.anchoredPosition.y, text.anchoredPosition.y), "Image and description do not align.");
                    Require(descriptions[j].GetPreferredValues(descriptions[j].text, text.rect.width, Mathf.Infinity).y <= text.rect.height + 1, "Description exceeds its row.");
                }
                portraits += images.Length;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
                RuinsStageBuilder.Capture(camera, $"StageReview/EnemyHelp/page-{i + 1}.png", 1600, 900);
                var scroll = root.GetComponentInChildren<ScrollRect>(); scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                RuinsStageBuilder.Capture(camera, $"StageReview/EnemyHelp/page-{i + 1}-bottom.png", 1600, 900);
                report.AppendLine($"PASS page {i + 1}: {images.Length} portraits, 224x224, left aligned, descriptions on right, no text overflow.");
            }
            Require(portraits == enemies.Length, "Not every enemy has a portrait.");
            // Switching back restores the text-only chapter and its automatic content sizing.
            help.ShowPage(0); Canvas.ForceUpdateCanvases();
            Require(root.GetComponentsInChildren<Image>().All(image => image.name != "EnemyPortrait"), "Portrait remains on a text page.");
            Require(root.GetComponentInChildren<ScrollRect>().content.GetComponent<ContentSizeFitter>().enabled, "Text-page sizing was not restored.");
            report.AppendLine("PASS all " + portraits + " enemies; text-page transition and scrolling content size.");
            File.WriteAllText("StageReview/EnemyHelp/layout-verification.txt", report.ToString(), Encoding.UTF8);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
