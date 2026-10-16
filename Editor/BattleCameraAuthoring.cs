using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>カメラの初期構図を編集します。実行時にTransformを上書きしません。</summary>
[InitializeOnLoad]
public static class BattleCameraAuthoring
{
    const string Request = "Temp/BattleCamera.request";
    const string Report = "StageReview/battle-camera.txt";
    const string Key = "BattleCameraPreview";
    static readonly Vector3[] CandidatePositions = {
        new Vector3(3, 3, -12), new Vector3(2.5f, 3, -11), new Vector3(3, 3, -10),
        new Vector3(2, 3, -10), new Vector3(4, 3, -12), new Vector3(3, 3.1f, -11)
    };
    static readonly float[] CandidateFovs = { 40, 44, 48, 48, 44, 44 };
    static BattleCameraAuthoring() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (EditorApplication.isPlaying) { PreviewStep(); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "apply") Apply();
            else if (command == "stand-column") RuinsColumnPlacement.Apply();
            else if (command == "preview") BeginPreview();
            else if (command == "candidates") { BeginPreview(); SessionState.SetBool(Key + "Candidates", true); }
            else throw new ArgumentException(command);
        }
        catch (Exception e) { File.AppendAllText(Report, e + "\n"); Debug.LogException(e); }
    }

    [MenuItem("Tools/Stage/Apply Oblique Battle Camera")]
    static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        PlayerVisualAuthoring.Require(scene.path == PlayerVisualAuthoring.ScenePath && !EditorApplication.isPlayingOrWillChangePlaymode,
            "Open RuinsStageRefined outside Play mode.");
        var camera = Camera.main;
        var battle = Object.FindObjectOfType<BattleManager>(true);
        var player = battle.playerVisualManager.playerSpawnPoint;
        var enemy = battle.enemyManager.enemySpawnPoint;
        string beforePlayer = EditorJsonUtility.ToJson(player);
        string beforeEnemy = EditorJsonUtility.ToJson(enemy);
        var rig = GameObject.Find("BattleCameraRig");
        if (rig == null)
        {
            rig = new GameObject("BattleCameraRig");
            Undo.RegisterCreatedObjectUndo(rig, "Create battle camera rig");
        }
        Undo.RecordObject(rig.transform, "Set oblique battle framing");
        Vector3 position = new Vector3(3f, 3f, -12f);
        Vector3 lookAt = new Vector3(0f, 3.3f, -1.75f);
        rig.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        Undo.SetTransformParent(camera.transform, rig.transform, "Parent battle camera");
        Undo.RecordObject(camera.transform, "Reset camera local pose");
        camera.transform.localPosition = Vector3.zero;
        camera.transform.localRotation = Quaternion.identity;
        Undo.RecordObject(camera, "Zoom battle camera");
        camera.fieldOfView = 40f;
        PlayerVisualAuthoring.Require(beforePlayer == EditorJsonUtility.ToJson(player) && beforeEnemy == EditorJsonUtility.ToJson(enemy),
            "Actor spawn transforms changed");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Report, $"Saved {scene.path}\nCamera world position={camera.transform.position:F3}, rotation={camera.transform.eulerAngles:F3}, FOV={camera.fieldOfView}\nPlayer={player.position:F3}, Enemy={enemy.position:F3}\nPASS: both spawn transforms preserved exactly.\nCamera is a child of BattleCameraRig with zero local position/rotation; no runtime transform driver.\n");
    }

    [MenuItem("Tools/Stage/Preview Oblique Battle Camera")]
    static void BeginPreview()
    {
        PlayerVisualAuthoring.Require(SceneManager.GetActiveScene().path == PlayerVisualAuthoring.ScenePath
            && !EditorApplication.isPlayingOrWillChangePlaymode, "Open RuinsStageRefined outside Play mode.");
        SessionState.SetInt(Key, 1);
        SessionState.SetBool(Key + "Candidates", false);
        SessionState.SetInt(Key + "Candidate", 0);
        SessionState.SetFloat(Key + "Due", 0);
        SessionState.SetFloat(Key + "Deadline", float.MaxValue);
        EditorApplication.EnterPlaymode();
    }

    static void Next(int step, float seconds)
    {
        SessionState.SetInt(Key, step);
        SessionState.SetFloat(Key + "Due", Time.realtimeSinceStartup + seconds);
        SessionState.SetFloat(Key + "Deadline", Time.realtimeSinceStartup + seconds + 40);
    }

    static void PreviewStep()
    {
        int step = SessionState.GetInt(Key, 0);
        if (step == 0) return;
        EditorApplication.QueuePlayerLoopUpdate();
        if (Time.realtimeSinceStartup < SessionState.GetFloat(Key + "Due", 0)) return;
        try
        {
            PlayerVisualAuthoring.Require(Time.realtimeSinceStartup < SessionState.GetFloat(Key + "Deadline", float.MaxValue), "Camera preview timed out");
            var game = Object.FindObjectOfType<GameManager>();
            var battle = game.battleManager;
            var manager = battle.playerVisualManager;
            if (step == 1)
            {
                Application.runInBackground = true;
                game.StartGame(0);
                Next(2, 6);
            }
            else if (step == 2)
            {
                // 一時的なPlayモード設定で、大きな敵との構図も確認します。
                battle.enemyManager.normalEnemies = new System.Collections.Generic.List<EnemyData>
                    { AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/ScriptableObject/EnemyData/Boss1.asset") };
                game.StartBattleWithTransition();
                Next(3, 1);
            }
            else if (step == 3)
            {
                if (!battle.CanPlayerAct) return;
                if (SessionState.GetBool(Key + "Candidates", false)) { Next(20, 0); return; }
                Next(4, .8f);
            }
            else if (step == 20)
            {
                int index = SessionState.GetInt(Key + "Candidate", 0);
                var camera = Camera.main;
                Vector3 position = CandidatePositions[index];
                camera.transform.parent.SetPositionAndRotation(position, Quaternion.LookRotation(new Vector3(0, 3.3f, -1.75f) - position));
                camera.fieldOfView = CandidateFovs[index];
                Next(21, .4f);
            }
            else if (step == 21)
            {
                int index = SessionState.GetInt(Key + "Candidate", 0);
                ScreenCapture.CaptureScreenshot("StageReview/camera-candidate-" + index + ".png");
                Next(22, .3f);
            }
            else if (step == 22)
            {
                int index = SessionState.GetInt(Key + "Candidate", 0) + 1;
                SessionState.SetInt(Key + "Candidate", index);
                if (index < CandidatePositions.Length) Next(20, 0);
                else { SessionState.SetInt(Key, 0); EditorApplication.ExitPlaymode(); }
            }
            else if (step >= 4 && step <= 6)
            {
                int number = step - 3;
                var camera = Camera.main;
                var bounds = PlayerVisualAuthoring.PosedBounds(manager.CurrentPlayer.gameObject);
                var enemyBounds = PlayerVisualAuthoring.PosedBounds(battle.enemyManager.CurrentEnemy.gameObject);
                Vector3 p = camera.WorldToViewportPoint(bounds.center);
                Vector3 e = camera.WorldToViewportPoint(enemyBounds.center);
                PlayerVisualAuthoring.Require(p.x < e.x && p.z < e.z, "Incorrect left-front/right-back composition");
                File.AppendAllText(Report, $"Player{number} viewport center={p:F3}, enemy={e:F3}\n");
                ScreenCapture.CaptureScreenshot("StageReview/battle-camera-player" + number + ".png");
                Next(step + 10, .4f);
            }
            else if (step >= 14 && step <= 16)
            {
                int number = step - 13;
                if (number < 3)
                {
                    manager.SpawnPlayer(number, battle.playerStatus);
                    Next(step - 9, .8f);
                }
                else
                {
                    File.AppendAllText(Report, "PASS: captured all three players with battle HUD in Play mode.\n");
                    SessionState.SetInt(Key, 0);
                    EditorApplication.ExitPlaymode();
                }
            }
        }
        catch (Exception e)
        {
            File.AppendAllText(Report, "FAIL: " + e + "\n");
            SessionState.SetInt(Key, 0);
            EditorApplication.ExitPlaymode();
            Debug.LogException(e);
        }
    }
}
