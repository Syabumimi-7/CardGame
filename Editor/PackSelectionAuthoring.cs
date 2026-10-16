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
using UnityEngine.EventSystems;
using System.Collections.Generic;

[InitializeOnLoad]
public static class PackSelectionAuthoring
{
    private const string Request = "Temp/PackSelection.request";
    private const string Report = "StageReview/pack-selection-verification.txt";
    private const string PlayReport = "StageReview/pack-opening-verification.txt";
    private const string TestKey = "PackSelectionVerification";
    static PackSelectionAuthoring() { EditorApplication.update += Poll; EditorApplication.update += VerifyStep; }
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "build") Build();
            else if (command == "preview") Preview();
            else if (command == "verify") BeginVerification();
            else throw new ArgumentException(command);
        }
        catch (Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
    }

    private static void BeginVerification()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        if (SceneManager.GetActiveScene().path != "Assets/Scenes/RuinsStageRefined.unity")
            throw new InvalidOperationException("Open RuinsStageRefined first.");
        SessionState.SetInt(TestKey, 1);
        SessionState.SetInt(TestKey + "Pack", 0);
        SessionState.SetFloat(TestKey + "Due", 0);
        SessionState.SetFloat(TestKey + "Deadline", float.MaxValue);
        File.WriteAllText(PlayReport, "Pack opening verification: " + DateTime.Now.ToString("s") + "\n");
        EditorApplication.EnterPlaymode();
    }

    private static void VerifyStep()
    {
        int step = SessionState.GetInt(TestKey, 0);
        if (step == 0 || !EditorApplication.isPlaying) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.realtimeSinceStartup < SessionState.GetFloat(TestKey + "Due", 0)) return;
        try
        {
            Application.runInBackground = true;
            if (Time.realtimeSinceStartup > SessionState.GetFloat(TestKey + "Deadline", float.MaxValue))
                throw new InvalidOperationException("Verification timed out at step " + step);
            var game = UnityEngine.Object.FindObjectOfType<GameManager>();
            int packIndex = SessionState.GetInt(TestKey + "Pack", 0);
            if (step == 1)
            {
                game.startScreenRoot.transform.Find("MenuPanel").gameObject.SetActive(false);
                var panel = game.startScreenRoot.transform.Find("GameStartPanel");
                panel.gameObject.SetActive(true); panel.localScale = Vector3.one;
                var group = panel.GetComponent<CanvasGroup>();
                if (group != null) { group.alpha = 1; group.interactable = group.blocksRaycasts = true; }
                Next(2, .8f);
            }
            else if (step == 2)
            {
                var pack = game.startScreenRoot.GetComponentsInChildren<PackSelectionItem>().Single(x => x.name == "Pack" + (packIndex + 1));
                var canvas = pack.GetComponentInParent<Canvas>();
                var point = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, pack.transform.position);
                var pointer = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
                var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count == 0 || hits[0].gameObject.GetComponentInParent<PackSelectionItem>() != pack)
                    throw new InvalidOperationException("Pack click blocked: " + pack.name + " hit=" + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
                ExecuteEvents.Execute(pack.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                if (packIndex == 0) { ScreenCapture.CaptureScreenshot("StageReview/pack-selection-ingame.png"); }
                ExecuteEvents.Execute(pack.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                if (packIndex == 1) Time.timeScale = 0;
                Next(20, .4f);
            }
            else if (step == 20)
            {
                var opening = game.GetComponent<PackOpeningPresentation>();
                if (!opening.IsPlaying || !game.startScreenRoot.activeInHierarchy || game.deckPreviewRoot.activeInHierarchy)
                    throw new InvalidOperationException("Opening changed screen before whiteout.");
                if (game.startScreenRoot.GetComponent<CanvasGroup>().alpha > .01f)
                    throw new InvalidOperationException("Other packs/UI did not fade.");
                var overlay = GameObject.Find("PackOpeningOverlay");
                if (overlay == null || overlay.transform.Find("OpeningPack") == null)
                    throw new InvalidOperationException("Selected pack missing.");
                game.StartGame((packIndex + 1) % 3);
                if (game.SelectedPoolType != packIndex)
                    throw new InvalidOperationException("Opening allowed a duplicate start.");
                ScreenCapture.CaptureScreenshot("StageReview/pack-opening-" + packIndex + "-isolation.png");
                Next(21, .9f);
            }
            else if (step == 21)
            {
                var lightObject = GameObject.Find("OpeningLight");
                if (lightObject == null) return;
                var light = lightObject.GetComponent<AcquisitionEffectGraphic>();
                if (light.Intensity < .3f) return;
                if (light.Cut < .99f || light.Intensity <= 0 || !game.startScreenRoot.activeInHierarchy)
                    throw new InvalidOperationException("Cut/light sequence failed.");
                var pack = (RectTransform)light.transform.parent;
                if (Mathf.Abs(pack.anchoredPosition.x) > .1f)
                    throw new InvalidOperationException("Pack not centered.");
                if (pack.Find("DetachedTop").GetComponent<CanvasGroup>().alpha > .01f) return;
                var remaining = pack.Find("RemainingWrapper") as RectTransform;
                var detached = pack.Find("DetachedTop") as RectTransform;
                if (remaining.GetComponent<RectMask2D>() == null || detached.GetComponent<RectMask2D>() == null
                    || remaining.Find("BottomVisual/IllustrationArea/Portrait") == null
                    || detached.Find("TopVisual/IllustrationArea/Portrait") == null)
                    throw new InvalidOperationException("Artwork was not split with the wrapper.");
                if (Mathf.Abs(remaining.anchoredPosition.y + remaining.rect.yMax - AcquisitionEffectGraphic.OpeningCutHeight) > .01f)
                    throw new InvalidOperationException("Artwork cut does not match the light slit.");
                if (detached.GetComponent<CanvasGroup>().alpha > .01f)
                    throw new InvalidOperationException("Cut seal did not disappear.");
                ScreenCapture.CaptureScreenshot("StageReview/pack-opening-" + packIndex + "-light.png");
                Next(22, 1.6f);
            }
            else if (step == 22)
            {
                if (game.GetComponent<PackOpeningPresentation>().IsPlaying) return;
                Time.timeScale = 1;
                if (GameObject.Find("PackOpeningOverlay") != null)
                    throw new InvalidOperationException("Opening overlay not cleaned up.");
                if (game.deckPreviewRoot.transform.Find("CardAcquisitionBackground") == null)
                    throw new InvalidOperationException("Acquisition background missing.");
                Next(3, 2.5f);
            }
            else if (step == 3)
            {
                if (game.SelectedPoolType != packIndex || game.deckManager.currentDeck.Count != 10 || !game.deckPreviewRoot.activeInHierarchy)
                    throw new InvalidOperationException("Pack start failed: " + packIndex);
                var start = game.deckPreviewRoot.GetComponentsInChildren<Button>().Single(x => x.name == "BattleStartButton");
                if (game.deckUIManager.cardParent.GetComponentsInChildren<CardUIView>().Length != 10)
                    throw new InvalidOperationException("Expected ten visible cards.");
                ScreenCapture.CaptureScreenshot("StageReview/pack-opening-" + packIndex + "-cards.png");
                Next(23, .2f);
            }
            else if (step == 23)
            {
                var start = game.deckPreviewRoot.GetComponentsInChildren<Button>().Single(x => x.name == "BattleStartButton");
                start.onClick.Invoke();
                Next(4, 4f);
            }
            else if (step == 4)
            {
                if (!game.battleUIRoot.activeInHierarchy || game.battleManager.Hand.Count == 0)
                    throw new InvalidOperationException("Battle did not start: " + packIndex);
                File.AppendAllText(PlayReport, "PASS play: pack" + (packIndex + 1) + " raycast, isolated pack, centered cut, detached seal, light, background, 10-card preview, start button, battle hand" + (packIndex == 1 ? ", opening at timeScale=0" : "") + ".\n");
                game.battleUIRoot.GetComponentInChildren<BattleHudUI>(true).ReturnToTitle();
                game.battleUIRoot.GetComponentsInChildren<Button>().Single(x => x.name == "はい").onClick.Invoke();
                if (packIndex < 2)
                {
                    SessionState.SetInt(TestKey + "Pack", packIndex + 1);
                    Next(1, 1f);
                }
                else Next(24, 1f);
            }
            else if (step == 24)
            {
                game.startScreenRoot.transform.Find("MenuPanel").gameObject.SetActive(false);
                var panel = game.startScreenRoot.transform.Find("GameStartPanel");
                panel.gameObject.SetActive(true);
                panel.localScale = Vector3.one;
                var group = panel.GetComponent<CanvasGroup>();
                if (group != null) { group.alpha = 1; group.interactable = group.blocksRaycasts = true; }
                game.StartGame(0);
                Next(25, .9f);
            }
            else if (step == 25)
            {
                game.ShowTitle();
                if (game.GetComponent<PackOpeningPresentation>().IsPlaying ||
                    game.startScreenRoot.GetComponent<CanvasGroup>().alpha != 1)
                    throw new InvalidOperationException("Opening cancellation did not restore source UI.");
                File.AppendAllText(PlayReport, "PASS play: cancel opening and restore UI; duplicate starts blocked.\n");
                Next(5, 1f);
            }
            else
            {
                // Check the new return button independently of pack selection.
                if (step == 5)
                {
                    var panel = game.startScreenRoot.transform.Find("GameStartPanel");
                    panel.gameObject.SetActive(true);
                    panel.Find("PackSelectionDesign/Composition/BackButton").GetComponent<Button>().onClick.Invoke();
                    Next(6, 1f); return;
                }
                if (!game.startScreenRoot.transform.Find("MenuPanel").gameObject.activeSelf)
                    throw new InvalidOperationException("Return button failed.");
                File.AppendAllText(PlayReport, "PASS play: return button.\n");
                SessionState.SetInt(TestKey, 0);
                EditorApplication.ExitPlaymode();
            }
        }
        catch (Exception e)
        {
            Time.timeScale = 1;
            SessionState.SetInt(TestKey, 0); File.AppendAllText(PlayReport, "FAIL: " + e + "\n");
            EditorApplication.ExitPlaymode();
        }
    }
    private static void Next(int step, float delay)
    {
        SessionState.SetInt(TestKey, step);
        SessionState.SetFloat(TestKey + "Due", Time.realtimeSinceStartup + delay);
        SessionState.SetFloat(TestKey + "Deadline", Time.realtimeSinceStartup + delay + 10);
    }

    [MenuItem("Tools/Packs/Build selection design")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory("StageReview");
        var report = new StringBuilder();
        foreach (string path in new[] { "Assets/Scenes/RuinsStageRefined.unity", "Assets/Scenes/RuinsStagePreview.unity", "Assets/TitleScene.unity" })
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) { report.AppendLine("Deferred unsaved scene; runtime installation enabled: " + path); continue; }
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var game = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<GameManager>(true)).Single();
                PackSelectionUI ui = PackSelectionUI.Install(game);
                if (ui == null) throw new InvalidOperationException("Pack panel missing: " + path);
                var packs = ui.GetComponentsInChildren<PackSelectionItem>(true);
                if (packs.Length != 3) throw new InvalidOperationException("Expected three packs.");
                for (int i = 0; i < 3; i++)
                {
                    if (packs[i].transform.Find("PackVisual/TopSeal") == null || packs[i].transform.Find("ContentsOrigin") == null)
                        throw new InvalidOperationException("Opening animation anchors missing.");
                    if (packs[i].GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "PackName").text != GameManager.GetPackDisplayName(i))
                        throw new InvalidOperationException("Pack label mismatch.");
                    game.deckManager.CreateInitialDeck(i);
                    if (game.deckManager.currentDeck.Count != 10) throw new InvalidOperationException("Initial deck does not contain 10 cards.");
                }
                game.deckManager.currentDeck.Clear();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                report.AppendLine("PASS: " + path + " — 3 packs, labels, opening anchors, 10-card initial decks.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        File.WriteAllText(Report, report.ToString());
        Preview();
    }

    [MenuItem("Tools/Packs/Preview selection design")]
    public static void Preview()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Pack preview", typeof(RectTransform), typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1600, 900);
            var cameraObject = new GameObject("Preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.orthographic = true; camera.orthographicSize = 450;
            camera.transform.position = new Vector3(0, 0, -1000);
            camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .085f, .10f);
            canvas.worldCamera = camera;
            var panel = new GameObject("GameStartPanel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            var game = root.AddComponent<GameManager>();
            game.startScreenRoot = root;
            PackSelectionUI.Install(game);
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                if (text.isTextOverflowing) throw new InvalidOperationException("Text overflow: " + text.text);
            }
            RuinsStageBuilder.Capture(camera, "StageReview/pack-selection-preview.png", 1600, 900);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
