using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class FrameStepVerification
{
    [MenuItem("Tools/Compatibility/Verify animation frame steps")]
    public static void Run()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var report = new StringBuilder();
        int checks = 0;
        try
        {
            var enemies = AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/ScriptableObject/EnemyData" })
                .Select(g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(e => e.name);
            foreach (var data in enemies)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(data.enemyPrefab, scene);
                try
                {
                    var enemy = model.GetComponent<EnemyInstance>();
                    var animation = model.GetComponentInChildren<EnemyAnimationPlayer>();
                    CheckPoses(data.name + " idle", animation.animator,
                        () => enemy.Setup(data, 1), dt => Advance(animation, dt), report, ref checks);
                    CheckPoses(data.name + " death", animation.animator,
                        () => { enemy.Setup(data, 1); animation.PlayDeath(); }, dt => Advance(animation, dt), report, ref checks);
                }
                finally { Object.DestroyImmediate(model); }
            }
            var manager = Object.FindObjectOfType<PlayerVisualManager>();
            foreach (var prefab in manager.playerPrefabs)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, scene);
                try
                {
                    var animation = model.GetComponent<PlayerBattleVisual>();
                    CheckPoses(prefab.name + " idle", animation.animator, animation.PlayIdle,
                        dt => Advance(animation, dt), report, ref checks);
                    CheckPoses(prefab.name + " death", animation.animator,
                        () => { animation.PlayIdle(); animation.PlayDeath(); }, dt => Advance(animation, dt), report, ref checks);
                }
                finally { Object.DestroyImmediate(model); }
            }
            report.AppendLine("PASS checks=" + checks + "; 8 enemies and 3 players, idle and death, 15/30/60/144 FPS time steps.");
        }
        catch (Exception e) { report.AppendLine("FAIL " + e); throw; }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Directory.CreateDirectory("StageReview/WindowsCompatibility");
            File.WriteAllText("StageReview/WindowsCompatibility/frame-steps.txt", report.ToString());
        }
    }

    static void CheckPoses(string name, Animator animator, Action reset, Action<float> advance, StringBuilder report, ref int checks)
    {
        var bones = animator.GetComponentsInChildren<Transform>();
        Quaternion[] baseline = null;
        foreach (int fps in new[] { 15, 30, 60, 144 })
        {
            reset();
            float elapsed = 0;
            while (elapsed < .73f)
            {
                float dt = Mathf.Min(1f / fps, .73f - elapsed);
                advance(dt); elapsed += dt;
            }
            var pose = bones.Select(b => b.localRotation).ToArray();
            if (baseline == null) baseline = pose;
            else
            {
                float difference = pose.Select((q, i) => Quaternion.Angle(q, baseline[i])).Max();
                if (difference > .15f) throw new InvalidOperationException(name + " differs at " + fps + " FPS by " + difference + " degrees.");
                checks++;
            }
            report.AppendLine("PASS " + name + " / " + fps + " FPS / 0.73 seconds");
        }
    }

    static void Advance(object animation, float dt)
    {
        animation.GetType().GetMethod("AdvanceAnimation", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(animation, new object[] { dt });
    }
}
