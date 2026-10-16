#if UNITY_EDITOR || (DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN)
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Opt-in development-player and Editor smoke test. Never active in a normal game.</summary>
public sealed class WindowsCompatibilityProbe : MonoBehaviour
{
    public static Action<int, int> ResizeEditor;
    public static Action FinishedEditor;
    public string output = "StageReview/WindowsCompatibility/Editor";
    public bool quick;
    private readonly List<string> errors = new List<string>();
    private int checks;
    private string report;
    private bool finished;
    private int originalVSync, originalFrameRate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Launch()
    {
        if (!Environment.GetCommandLineArgs().Contains("--compatibility-audit")) return;
        var probe = new GameObject("Windows compatibility audit").AddComponent<WindowsCompatibilityProbe>();
        probe.quick = true;
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "--audit-output");
        if (i >= 0 && i + 1 < args.Length) probe.output = args[i + 1];
    }

    private IEnumerator Start()
    {
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(output); report = Path.Combine(output, "verification.txt");
        File.WriteAllText(report, $"Unity {Application.unityVersion}; {SystemInfo.operatingSystem}\nGPU {SystemInfo.graphicsDeviceName}; API {SystemInfo.graphicsDeviceType}; VRAM {SystemInfo.graphicsMemorySize} MB; RAM {SystemInfo.systemMemorySize} MB\n");
        Application.logMessageReceived += OnLog;
        originalVSync = QualitySettings.vSyncCount; originalFrameRate = Application.targetFrameRate;
        var run = Run();
        while (true)
        {
            bool next; object current = null;
            try { next = run.MoveNext(); if (next) current = run.Current; }
            catch (Exception e) { File.AppendAllText(report, "FAIL " + e + "\n"); Finish(false); yield break; }
            if (!next) break;
            yield return current;
        }
        Finish(true);
    }

    private void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(message);
            File.AppendAllText(report, "RUNTIME ERROR " + message + "\n" + stack + "\n");
        }
    }

    private void Finish(bool passed)
    {
        finished = true; Application.logMessageReceived -= OnLog;
        Time.timeScale = 1;
        QualitySettings.vSyncCount = originalVSync; Application.targetFrameRate = originalFrameRate;
        File.AppendAllText(report, $"{(passed ? "PASS" : "FAIL")} checks={checks}; runtimeErrors={errors.Count}\n");
        if (Application.isEditor) FinishedEditor?.Invoke(); else Application.Quit(passed ? 0 : 1);
    }

    private IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(1);
        var game = FindObjectOfType<GameManager>(); Require(game != null, "GameManager loaded from build scene.");
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
        var sizes = quick ? new[] { new Vector2Int(1280, 720), new Vector2Int(1024, 768) }
            : new[] { new Vector2Int(1280,720), new Vector2Int(1366,768), new Vector2Int(1920,1080),
                new Vector2Int(2560,1440), new Vector2Int(3840,2160), new Vector2Int(1024,768),
                new Vector2Int(1280,1024), new Vector2Int(1920,1200), new Vector2Int(2560,1080), new Vector2Int(3440,1440) };
        foreach (var size in sizes)
        {
            if (Application.isEditor) ResizeEditor(size.x, size.y); else Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.35f);
            Require(Screen.width == size.x && Screen.height == size.y, $"Requested {size}; actual {Screen.width}x{Screen.height}.");
            string prefix = $"{size.x}x{size.y}";
            File.AppendAllText(report, "RESOLUTION " + prefix + "\n");
            game.ShowTitle(); yield return new WaitForSecondsRealtime(.8f);
            CheckViewport(); CheckControls(game.startScreenRoot.transform.Find("MenuPanel"), "title");
            Capture(prefix, "title"); yield return null;
            var help = (HelpScreenUI)typeof(GameManager).GetField("helpUI", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            help.Open(); yield return null;
            CheckViewport(); CheckControls(FindActive("HelpScreen"), "help");
            help.ShowPage(10); yield return null;
            Require(FindActive("HelpScreen").GetComponentsInChildren<Image>().Count(x => x.name == "EnemyPortrait") == 3, "Enemy portraits present.");
            Capture(prefix, "help"); yield return null; help.Close(); yield return null;
            var credits = game.GetComponent<CreditsScreenUI>(); credits.Open(); yield return null;
            CheckControls(FindActive("CreditsScreen"), "credits"); credits.Close(); yield return null;
            var collection = game.GetComponent<CardCollectionUI>(); collection.Open(); yield return null;
            CheckControls(FindActive("CardCollection"), "collection");
            collection.Close(); yield return null;
            game.startScreenRoot.transform.Find("MenuPanel/StartButton").GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(1f);
            CheckControls(game.startScreenRoot.transform.Find("GameStartPanel"), "pack selection");
            Capture(prefix, "packs"); yield return null;
            game.StartGame(0);
            float deckDeadline = Time.realtimeSinceStartup + 25;
            var startBattle = game.deckPreviewRoot.GetComponentsInChildren<Button>(true).First(b => b.name == "BattleStartButton");
            while (!startBattle.gameObject.activeInHierarchy && Time.realtimeSinceStartup < deckDeadline) yield return null;
            Require(startBattle.gameObject.activeInHierarchy, "Pack opening and deck entry completed.");
            CheckControls(game.deckPreviewRoot.transform, "deck preview");
            Capture(prefix, "deck"); yield return null;
            game.deckManager.CreateInitialDeck(0);
            game.ShowBattle(); game.battleManager.currentBattleCount = 1;
            UnityEngine.Random.InitState(12345); game.battleManager.StartBattle();
            float deadline = Time.realtimeSinceStartup + 20;
            while (!game.battleManager.CanPlayerAct && Time.realtimeSinceStartup < deadline) yield return null;
            Require(game.battleManager.CanPlayerAct, "Battle intro and draw completed.");
            CheckViewport(); CheckControls(game.battleUIRoot.transform, "battle");
            Require(game.battleManager.Hand.Count == game.battleManager.balanceData.handDrawCount, "Expected hand size.");
            Capture(prefix, "battle"); yield return null;
            float sampleStart = Time.realtimeSinceStartup; int frames = 0;
            while (Time.realtimeSinceStartup - sampleStart < .5f) { frames++; yield return null; }
            File.AppendAllText(report, $"PERFORMANCE {prefix}: {frames / (Time.realtimeSinceStartup - sampleStart):F1} FPS; allocated={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576} MiB; quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}\n");
            var card = game.battleManager.handUI.handParent.GetComponentsInChildren<CardUIView>().FirstOrDefault();
            Require(card != null, "Card view exists.");
            CardDetailOverlay.Show(card); yield return null;
            CheckControls(FindActive("CardDetailOverlay"), "detail");
            FindObjectOfType<CardDetailOverlay>().Close(); yield return null;
            game.PauseUI.Pause(); yield return null;
            float paused = Time.time; yield return new WaitForSecondsRealtime(.15f);
            Require(Time.timeScale == 0 && Time.time == paused, "Pause freezes scaled time.");
            CheckViewport(); CheckControls(FindActive("PauseMenu"), "pause");
            Capture(prefix, "pause"); yield return null;
            FindActive("PauseMenu").GetComponentsInChildren<Button>().First(b => b.name == "設定").onClick.Invoke(); yield return null;
            CheckControls(FindActive("Settings"), "settings");
            game.PauseUI.Resume(); yield return null;
            Require(Time.timeScale == 1 && !game.battleManager.IsPaused, "Resume restores time and battle input.");
            game.battleManager.CancelBattle();
            game.ProgressionUI.HandleBattleFinished(true); yield return null;
            CheckControls(FindActive("VictoryPanel"), "victory");
            game.battleManager.currentBattleCount = 10;
            game.ProgressionUI.HandleBattleFinished(true); yield return null;
            CheckControls(FindActive("GameClearPanel"), "clear");
            Capture(prefix, "clear"); yield return null;
            File.AppendAllText(report, $"PASS {prefix}: title/help/portraits/credits/collection/battle/detail/pause/settings/victory/clear\n");
        }
        // Advance the actual animation implementation with different frame steps for the same elapsed time.
        var animation = game.battleManager.playerVisualManager;
        game.deckManager.CreateInitialDeck(0); game.ShowBattle(); game.battleManager.currentBattleCount = 1; game.battleManager.StartBattle();
        yield return new WaitForSecondsRealtime(3);
        var player = animation.CurrentPlayer; Require(player != null, "Player model loaded.");
        player.enabled = false;
        var advance = typeof(PlayerBattleVisual).GetMethod("AdvanceAnimation", BindingFlags.NonPublic | BindingFlags.Instance);
        Quaternion[] baseline = null;
        foreach (int fps in new[] { 30, 60, 144 })
        {
            player.PlayIdle();
            for (int frame = 0; frame < fps; frame++) advance.Invoke(player, new object[] { 1f / fps });
            var pose = player.animator.GetComponentsInChildren<Transform>().Select(t => t.localRotation).ToArray();
            if (baseline == null) baseline = pose;
            else Require(pose.Select((q,i) => Quaternion.Angle(q, baseline[i])).Max() < .15f, $"Player animation same pose after 1 second at {fps} FPS.");
            File.AppendAllText(report, $"PASS animation: one second at {fps} FPS\n");
        }
        player.enabled = true;
        var normalEnemies = game.battleManager.enemyManager.normalEnemies;
        game.battleManager.enemyManager.normalEnemies = new List<EnemyData> { normalEnemies.First(e => e.name == "Enemy1") };
        string battleBaseline = null;
        foreach (int fps in new[] { 30, 60, 144 })
        {
            Application.targetFrameRate = fps;
            var battle = game.battleManager;
            battle.CancelBattle(); battle.playerStatus.currentHP = battle.playerStatus.maxHP;
            battle.turnBannerDuration = .1f; battle.enemyActionDelay = .05f;
            battle.handUI.drawDuration = .12f; battle.handUI.drawInterval = .02f; battle.handUI.discardDuration = .08f;
            UnityEngine.Random.InitState(24680);
            game.deckManager.CreateInitialDeck(0); battle.StartBattle();
            float deadline = Time.realtimeSinceStartup + 15;
            while (!battle.CanPlayerAct && Time.realtimeSinceStartup < deadline) yield return null;
            Require(battle.CanPlayerAct, $"Battle starts at target {fps} FPS.");
            float start = Time.realtimeSinceStartup;
            for (int turn = 0; turn < 2; turn++)
            {
                battle.EndPlayerTurn();
                deadline = Time.realtimeSinceStartup + 15;
                while (!battle.CanPlayerAct && Time.realtimeSinceStartup < deadline) yield return null;
                Require(battle.CanPlayerAct, $"Enemy turn completes at target {fps} FPS.");
            }
            string state = $"HP={battle.playerStatus.currentHP}; enemyHP={battle.enemyManager.CurrentEnemy.currentHP}; attack={battle.enemyManager.CurrentEnemy.AttackBonus}; cost={battle.playerStatus.currentCost}; hand="
                + string.Join(",", battle.Hand.Select(c => c.Data.name + ":" + c.CurrentPerformance.cost));
            if (battleBaseline == null) battleBaseline = state;
            else Require(state == battleBaseline, $"Same seeded battle result at target {fps} FPS: " + state);
            File.AppendAllText(report, $"PASS two enemy turns at target {fps} FPS; elapsed={Time.realtimeSinceStartup - start:F3}s; {state}\n");
        }
        game.battleManager.enemyManager.normalEnemies = normalEnemies;
        Require(errors.Count == 0, "No runtime errors.");
    }

    private void CheckViewport()
    {
        Rect expected = GameViewport.CalculateRect(Screen.width, Screen.height);
        Require(Camera.main != null && Mathf.Abs(Camera.main.aspect - GameViewport.Aspect) < .002f, "Scene camera aspect is 16:9.");
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>().Where(c => c.gameObject.scene.IsValid() && c.isActiveAndEnabled && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.name != "Letterbox"))
        {
            Rect bounds = ScreenBounds(GameViewport.ContentFor(canvas));
            Require(Mathf.Abs(bounds.xMin - expected.xMin * Screen.width) < 2 && Mathf.Abs(bounds.yMin - expected.yMin * Screen.height) < 2
                && Mathf.Abs(bounds.width - expected.width * Screen.width) < 2 && Mathf.Abs(bounds.height - expected.height * Screen.height) < 2, "Canvas viewport matches camera: " + canvas.name);
        }
        if (expected.xMin * Screen.width >= 1 || expected.yMin * Screen.height >= 1)
        {
            var hits = new List<RaycastResult>();
            Vector2 point = expected.xMin * Screen.width >= 1
                ? new Vector2(expected.xMin * Screen.width * .5f, Screen.height * .5f)
                : new Vector2(Screen.width * .5f, expected.yMin * Screen.height * .5f);
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Canvas>().name == "Letterbox", "Black bars block clicks into game.");
        }
    }

    private void CheckControls(Transform root, string screen)
    {
        Require(root != null, "Screen exists: " + screen);
        Rect normalized = GameViewport.CalculateRect(Screen.width, Screen.height);
        Rect area = new Rect(normalized.x * Screen.width, normalized.y * Screen.height, normalized.width * Screen.width, normalized.height * Screen.height);
        foreach (var button in root.GetComponentsInChildren<Button>())
        {
            if (!button.isActiveAndEnabled || !button.IsInteractable() || button.name == "OutsideClickArea" || button.GetComponentInParent<ScrollRect>() != null) continue;
            if (button.GetComponentsInParent<CanvasGroup>().Any(g => g.alpha < .1f || !g.blocksRaycasts || !g.interactable)) continue;
            Rect bounds = ScreenBounds((RectTransform)button.transform);
            Require(bounds.xMin >= area.xMin - 2 && bounds.xMax <= area.xMax + 2 && bounds.yMin >= area.yMin - 2 && bounds.yMax <= area.yMax + 2, $"{screen} control inside viewport: {button.name} {bounds}");
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = bounds.center }, hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, $"{screen} pointer hits visible control: {button.name}; first={(hits.Count > 0 ? hits[0].gameObject.name : "none")}");
        }
    }

    private static Transform FindActive(string name) => Resources.FindObjectsOfTypeAll<RectTransform>().FirstOrDefault(t => t.gameObject.scene.IsValid() && t.gameObject.activeInHierarchy && t.name == name);
    private static Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners.Min(p => p.x), corners.Min(p => p.y), corners.Max(p => p.x), corners.Max(p => p.y));
    }
    private void Capture(string resolution, string screen)
    {
        if (quick || resolution == "1280x720" || resolution == "1024x768" || resolution == "3440x1440" || resolution == "1920x1200")
            ScreenCapture.CaptureScreenshot(Path.Combine(output, resolution + "-" + screen + ".png"));
    }
    private void Require(bool valid, string message)
    {
        checks++;
        if (!valid) throw new InvalidOperationException(message);
    }
    private void OnDestroy() { Application.logMessageReceived -= OnLog; if (!finished && report != null) File.AppendAllText(report, "Interrupted before completion.\n"); }
}
#endif
