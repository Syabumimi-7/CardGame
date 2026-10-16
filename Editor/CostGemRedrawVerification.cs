using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicitly requested, isolated Editor verification; never included in a player.
[InitializeOnLoad]
public static class CostGemRedrawVerification
{
    const string Request = "Temp/CostGemRedrawVerification.request";
    const string Folder = "StageReview/RuntimePerformance/Phase1/";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static double nextCheck;
    static CostGemRedrawVerification() { EditorApplication.update += Poll; }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request)) return;
        try
        {
            string phase = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            if (phase != "before" && phase != "after") throw new ArgumentException("Unknown phase");
            Run(phase);
        }
        catch (Exception e) { File.WriteAllText(Folder + "error.txt", e.ToString()); Debug.LogException(e); }
    }

    static T Child<T>(Transform parent, string name) where T : Component
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    static void Run(string phase)
    {
        Scene active = SceneManager.GetActiveScene();
        string path = active.path;
        bool dirty = active.isDirty;
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameBalanceData balance = null;
        var snapshots = new StringBuilder();
        int redraws = 0;
        try
        {
            var root = new GameObject("Isolated HUD verification");
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, preview);
            var hud = root.AddComponent<BattleHudUI>();
            var battle = root.AddComponent<BattleManager>();
            hud.battle = battle;
            battle.enemyManager = root.AddComponent<EnemyManager>();
            battle.playerStatus = root.AddComponent<PlayerStatus>();
            balance = ScriptableObject.CreateInstance<GameBalanceData>();
            battle.balanceData = balance;
            battle.turnText = Child<TextMeshProUGUI>(root.transform, "Turn");
            battle.turnText.text = "";
            hud.battleCount = Child<TextMeshProUGUI>(root.transform, "Count");
            hud.enemyName = Child<TextMeshProUGUI>(root.transform, "Enemy");
            typeof(BattleHudUI).GetField("playerName", Private).SetValue(hud, Child<TextMeshProUGUI>(root.transform, "Player"));
            hud.banner = root.AddComponent<CanvasGroup>();
            hud.playerHealth = Child<HealthBarUI>(root.transform, "Player health");
            hud.enemyHealth = Child<HealthBarUI>(root.transform, "Enemy health");
            hud.playerHealth.statusText = Child<TextMeshProUGUI>(root.transform, "Player status");
            hud.enemyHealth.statusText = Child<TextMeshProUGUI>(root.transform, "Enemy status");
            hud.playerHealth.statusText.text = "";
            hud.enemyHealth.statusText.text = "";
            hud.costGems = new GameObject("Gems", typeof(RectTransform)).GetComponent<RectTransform>();
            SceneManager.MoveGameObjectToScene(hud.costGems.gameObject, preview);
            hud.gemTemplate = Child<BattleHudGraphic>(root.transform, "Template");
            hud.gemTemplate.shape = BattleHudGraphic.Shape.Gem;
            hud.gemTemplate.rectTransform.sizeDelta = new Vector2(20, 28);
            hud.gemTemplate.gameObject.SetActive(false);
            MethodInfo update = typeof(BattleHudUI).GetMethod("LateUpdate", Private);
            var gems = (List<BattleHudGraphic>)typeof(BattleHudUI).GetField("gems", Private).GetValue(hud);
            balance.maxCost = 3;
            battle.playerStatus.currentCost = 3;
            update.Invoke(hud, null);
            UnityAction count = () => redraws++;
            foreach (var gem in gems) gem.RegisterDirtyVerticesCallback(count);
            for (int i = 0; i < 100; i++) update.Invoke(hud, null);
            foreach (var gem in gems) gem.UnregisterDirtyVerticesCallback(count);

            int[,] cases = { {3,3}, {3,2}, {3,0}, {3,5}, {6,5}, {2,1}, {0,0}, {3,3}, {3,0} };
            MethodInfo populate = typeof(BattleHudGraphic).GetMethod("OnPopulateMesh", Private | BindingFlags.DeclaredOnly);
            for (int c = 0; c < cases.GetLength(0); c++)
            {
                balance.maxCost = cases[c,0];
                battle.playerStatus.currentCost = cases[c,1];
                update.Invoke(hud, null);
                snapshots.AppendLine($"Case {c}: {balance.maxCost}/{battle.playerStatus.currentCost}; gems={gems.Count}");
                foreach (var gem in gems)
                {
                    snapshots.AppendLine($"active={gem.gameObject.activeSelf}; color={JsonUtility.ToJson(gem.color)}; metal={JsonUtility.ToJson(gem.metal)}");
                    using (var vh = new VertexHelper())
                    {
                        populate.Invoke(gem, new object[] { vh });
                        var vertices = new List<UIVertex>();
                        vh.GetUIVertexStream(vertices);
                        foreach (var v in vertices)
                            snapshots.AppendLine(JsonUtility.ToJson(v));
                    }
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (balance != null) Object.DestroyImmediate(balance);
        }
        if (SceneManager.GetActiveScene().path != path || active.isDirty != dirty)
            throw new InvalidOperationException("Active scene changed");
        string snapshot = snapshots.ToString();
        File.WriteAllText(Folder + phase + "-geometry.txt", snapshot);
        File.WriteAllText(Folder + phase + "-redraws.txt", redraws.ToString());
        if (phase == "before" && redraws != 300) throw new InvalidOperationException("Unexpected baseline redraw count: " + redraws);
        if (phase == "after")
        {
            if (snapshot != File.ReadAllText(Folder + "before-geometry.txt")) throw new InvalidOperationException("Visual output differs");
            if (redraws != 0) throw new InvalidOperationException("Unnecessary redraws remain: " + redraws);
        }
        File.WriteAllText(Folder + phase + "-result.txt", $"PASS: 9 cost transitions; active flags, colors and all generated vertices recorded.\nSteady 100 updates: {redraws} dirty requests.\nActive scene unchanged: {path}, dirty={dirty}.\n");
    }
}
