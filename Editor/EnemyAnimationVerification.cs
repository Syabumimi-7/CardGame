using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>シーンを変更せず、実際のPrefab・クリップ・戦闘ステータスで回帰検証します。</summary>
public static class EnemyAnimationVerification
{
    private static readonly MethodInfo Advance = typeof(EnemyAnimationPlayer).GetMethod(
        "AdvanceAnimation", BindingFlags.Instance | BindingFlags.NonPublic);

    [MenuItem("Tools/Enemies/Verify Enemy1 Animations")]
    public static void Verify()
    {
        var report = new StringBuilder();
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/ScriptableObject/EnemyData/Enemy1.asset");
        var preview = new PreviewRenderUtility();
        GameObject root = UnityEngine.Object.Instantiate(data.enemyPrefab);
        root.transform.position = Vector3.zero;
        preview.AddSingleGO(root);
        try
        {
            EnemyInstance enemy = root.GetComponent<EnemyInstance>();
            EnemyAnimationPlayer player = root.GetComponent<EnemyAnimationPlayer>();
            Require(player != null && player.animator != null && player.animations != null, "Prefab references");
            Require(root.GetComponent<MeshRenderer>() == null, "Capsule mesh removed");
            Require(root.GetComponentsInChildren<SkinnedMeshRenderer>().All(renderer =>
                renderer.sharedMaterials.All(material => material != null && material.GetTexture("_BaseMap") != null)), "Zombie textures assigned");
            enemy.Setup(data, 1);
            Require(!enemy.IsAnimating, "Idle does not block battle");
            Transform[] bones = player.animator.GetComponentsInChildren<Transform>();
            Quaternion[] initial = bones.Select(bone => bone.localRotation).ToArray();
            Step(player, 0.6f);
            Require(bones.Where((bone, index) => Quaternion.Angle(bone.localRotation, initial[index]) > 0.1f).Any(), "Idle moves skeleton");
            Capture(preview, "idle");
            Step(player, player.animations.idle.length * 2f);
            Require(!enemy.IsAnimating, "Idle loops without locking battle");

            enemy.currentBlock = 10;
            Require(enemy.TakeDamage(3) == 0 && !enemy.IsAnimating, "Fully blocked damage stays idle");
            enemy.currentBlock = 0;
            int previousHP = enemy.currentHP;
            Require(enemy.TakeDamage(3) == 3 && enemy.currentHP == previousHP - 3 && !enemy.IsAnimating, "HP damage stays idle");
            enemy.AddPoison(2, 2);
            Require(enemy.TickPoison() == 2 && !enemy.IsAnimating, "Poison damage stays idle");

            enemy.currentHP = 1;
            enemy.TickPoison();
            Require(enemy.IsDead && enemy.IsAnimating, "Lethal poison starts death");
            Step(player, player.animations.death.length + 0.2f);
            Require(!enemy.IsAnimating, "Death completes for battle cleanup");
            Capture(preview, "death");
            Quaternion[] deadPose = bones.Select(bone => bone.localRotation).ToArray();
            player.PlayDeath();
            Step(player, 1f);
            Require(bones.Where((bone, index) => Quaternion.Angle(bone.localRotation, deadPose[index]) > 0.1f).Any() == false,
                "Death holds pose and cannot be interrupted");
            Require(enemy.TakeDamage(100) == 0, "Dead enemy ignores subsequent damage");

            enemy.Setup(data, 1);
            Require(!enemy.IsDead && !enemy.IsAnimating, "Reset restores idle after death");
            enemy.TakeDamage(enemy.maxHP);
            Require(enemy.IsDead && enemy.IsAnimating, "Lethal direct attack starts death");
            player.enabled = false;
            Require(!enemy.IsAnimating, "Disabled animation cannot deadlock battle");
            report.AppendLine("PASS: idle, block, damage stays idle, poison stays idle, death, death priority, reset, disabled fallback.");
            report.AppendLine("Screenshots: Temp/Enemy1-idle.png, Enemy1-death.png");
            File.WriteAllText("Temp/EnemyAnimationVerification-report.txt", report.ToString());
            Debug.Log(report.ToString());
        }
        catch (Exception exception)
        {
            File.WriteAllText("Temp/EnemyAnimationVerification-report.txt", exception.ToString());
            throw;
        }
        finally { preview.Cleanup(); }
    }

    private static void Step(EnemyAnimationPlayer player, float seconds)
    {
        // 描画と同じ更新処理を固定刻みで進め、Editorのフレーム時間には依存しません。
        while (seconds > 0f)
        {
            float delta = Mathf.Min(1f / 60f, seconds);
            Advance.Invoke(player, new object[] { delta });
            seconds -= delta;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + message);
    }

    private static void Capture(PreviewRenderUtility preview, string name)
    {
        preview.BeginStaticPreview(new Rect(0, 0, 640, 640));
        preview.camera.transform.position = new Vector3(0f, 0.5f, -4f);
        preview.camera.transform.LookAt(new Vector3(0f, -0.15f, 0f));
        preview.camera.nearClipPlane = 0.01f;
        preview.camera.farClipPlane = 50f;
        preview.camera.fieldOfView = 40f;
        preview.camera.clearFlags = CameraClearFlags.Color;
        preview.camera.backgroundColor = new Color(0.13f, 0.15f, 0.18f);
        preview.lights[0].intensity = 1.3f;
        preview.lights[0].transform.rotation = Quaternion.Euler(35, 150, 0);
        preview.lights[1].intensity = 0.8f;
        preview.ambientColor = Color.gray;
        preview.Render(true);
        Texture2D texture = preview.EndStaticPreview();
        File.WriteAllBytes("Temp/Enemy1-" + name + ".png", texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }
}
