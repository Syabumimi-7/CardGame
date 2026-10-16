using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Checks all popup meshes and block/healing behavior.</summary>
[InitializeOnLoad]
public static class DamagePopupVerification
{
    const string Request = "Temp/DamagePopup.verify";
    static DamagePopupVerification() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating
            || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        Verify();
    }

    [MenuItem("Tools/Damage UI/Verify Effect Meshes")]
    public static void Verify()
    {
        GameObject root = null;
        Mesh mesh = null;
        try
        {
            root = new GameObject("Damage Effect Verification", typeof(Canvas));
            root.hideFlags = HideFlags.HideAndDontSave;
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var statusObject = new GameObject("Status Verification");
            statusObject.transform.SetParent(root.transform, false);
            statusObject.SetActive(false);
            var player = statusObject.AddComponent<PlayerStatus>();
            player.currentHP = player.maxHP = 50;
            player.currentBlock = 15;
            Require(player.TakeDamage(10) == 0 && player.currentBlock == 5 && player.currentHP == 50, "Partial block");
            Require(player.TakeDamage(5) == 0 && player.currentBlock == 0 && player.currentHP == 50, "Exact block break");
            player.currentBlock = 5;
            Require(player.TakeDamage(10) == 5 && player.currentBlock == 0 && player.currentHP == 45, "Block overflow");
            Require(player.Heal(30) == 5 && player.currentHP == 50, "Recovery clamps to missing HP");
            Require(player.Heal(30) == 0 && player.Heal(-5) == 0, "No zero/negative recovery");
            player.currentBlock = 10;
            Require(player.TakeDamage(3, isPoison: true) == 3 && player.currentBlock == 10, "Poison bypass");
            UnityEngine.Object.DestroyImmediate(statusObject);
            for (int style = 0; style < 12; style++)
            {
                var go = new GameObject("Effect", typeof(RectTransform), typeof(DamagePopupGraphic));
                go.transform.SetParent(root.transform, false);
                var graphic = go.GetComponent<DamagePopupGraphic>();
                if (graphic.canvasRenderer == null) throw new Exception("CanvasRenderer missing: " + style);
                graphic.rectTransform.sizeDelta = new Vector2(220, 160);
                graphic.style = style;
                graphic.progress = .4f;
                graphic.color = new Color(1, .5f, .2f, 1);
                graphic.SetAllDirty();
                Canvas.ForceUpdateCanvases();
                mesh = graphic.canvasRenderer.GetMesh();
                if (mesh == null || mesh.vertexCount == 0 || mesh.triangles.Length == 0)
                    throw new Exception("Rendered mesh empty: " + style);
                bool visible = false;
                foreach (var color in mesh.colors32) if (color.a > 0) visible = true;
                if (!visible) throw new Exception("All vertices transparent: " + style);
                if (graphic.canvasRenderer.materialCount == 0 || graphic.canvasRenderer.GetMaterial(0) == null)
                    throw new Exception("UI material missing: " + style);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = null;
                UnityEngine.Object.DestroyImmediate(go);
            }
            Directory.CreateDirectory("StageReview/DamageUI");
            File.WriteAllText("StageReview/DamageUI/effect-verification.txt", "PASS: All 12 styles have CanvasRenderer, nonempty rendered meshes, visible vertex alpha and UI materials.\nPASS: Partial block, exact break, overflow, capped recovery, zero recovery and poison bypass.\n");
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("StageReview/DamageUI");
            File.WriteAllText("StageReview/DamageUI/effect-verification.txt", "FAIL: " + e);
            Debug.LogException(e);
        }
        finally
        {
            if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        }
    }
    static void Require(bool result, string name)
    {
        if (!result) throw new Exception(name);
    }
}
