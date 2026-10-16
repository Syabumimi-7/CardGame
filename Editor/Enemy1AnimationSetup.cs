using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>既存Enemy1の参照を保ったまま、ゾンビの見た目とアニメーションを設定します。</summary>
public static class Enemy1AnimationSetup
{
    private const string ModelFolder = "Assets/model/Enemy/Enemy1/";
    private const string SetPath = "Assets/ScriptableObject/EnemyData/Enemy1Animations.asset";
    private const string RequestPath = "Temp/Enemy1AnimationSetup.request";
    private const string ReportPath = "Temp/Enemy1AnimationSetup-report.txt";

    [InitializeOnLoadMethod]
    private static void CheckRequest()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestPath);
            try
            {
                Configure();
                EnemyAnimationVerification.Verify();
            }
            catch (Exception exception)
            {
                File.WriteAllText(ReportPath, exception.ToString());
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/Enemies/Configure Enemy1 Zombie")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Playモードを終了してから設定してください。");
        var report = new StringBuilder();
        EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/ScriptableObject/EnemyData/Enemy1.asset");
        if (data == null || data.enemyPrefab == null) throw new InvalidOperationException("Enemy1のPrefab参照がありません。");
        string textureFolder = ModelFolder + "Textures";
        if (!AssetDatabase.IsValidFolder(textureFolder))
        {
            AssetDatabase.CreateFolder(ModelFolder.TrimEnd('/'), "Textures");
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ModelFolder + "Ch10_nonPBR.fbx");
            if (!importer.ExtractTextures(textureFolder))
                throw new InvalidOperationException("FBXからテクスチャを展開できませんでした。");
            AssetDatabase.Refresh();
            importer.SaveAndReimport();
        }
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelFolder + "Ch10_nonPBR.fbx");
        if (model == null) throw new InvalidOperationException("ゾンビモデルがありません。");

        AnimationClip idle = FindClip("Idle");
        AnimationClip death = FindClip("Dying");
        foreach (AnimationClip clip in new[] { idle, death })
        {
            // Genericリグは同じ骨階層を共有していることを、全カーブのパスで検証します。
            var missingPaths = AnimationUtility.GetCurveBindings(clip)
                .Where(binding => binding.type == typeof(Transform) && !string.IsNullOrEmpty(binding.path)
                    && model.transform.Find(binding.path) == null).Select(binding => binding.path).Distinct().ToArray();
            if (missingPaths.Length > 0)
                throw new InvalidOperationException(clip.name + "の骨がモデルにありません: " + string.Join(", ", missingPaths));
            report.AppendLine($"Clip OK: {clip.name}, {clip.length:F3}s, {AnimationUtility.GetCurveBindings(clip).Length} curves");
        }

        EnemyAnimationSet set = AssetDatabase.LoadAssetAtPath<EnemyAnimationSet>(SetPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<EnemyAnimationSet>();
            AssetDatabase.CreateAsset(set, SetPath);
        }
        set.idle = idle;
        set.death = death;
        EditorUtility.SetDirty(set);

        string prefabPath = AssetDatabase.GetAssetPath(data.enemyPrefab);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Transform previous = root.transform.Find("ZombieVisual");
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "ZombieVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            visual.transform.localScale = Vector3.one;

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("モデルにRendererがありません。");
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y <= 0f) throw new InvalidOperationException("モデルの高さが0です。");
            // 既存カプセルと同じ高さ・足元を保ちます。
            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
            float height = capsule != null ? capsule.height : 2f;
            Vector3 center = capsule != null ? capsule.center : Vector3.zero;
            float scale = height / bounds.size.y;
            Vector3 localCenter = root.transform.InverseTransformPoint(bounds.center);
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = center - localCenter * scale;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Litシェーダーがありません。");
            string materialFolder = ModelFolder + "Materials";
            if (!AssetDatabase.IsValidFolder(materialFolder)) AssetDatabase.CreateFolder(ModelFolder.TrimEnd('/'), "Materials");
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null) throw new InvalidOperationException("モデルのMaterialが未設定です。");
                    string path = materialFolder + "/" + source.name.Replace('/', '_') + "_URP.mat";
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(shader);
                        AssetDatabase.CreateAsset(material, path);
                    }
                    material.SetTexture("_BaseMap", source.mainTexture);
                    material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.color : Color.white);
                    if (source.HasProperty("_Glossiness")) material.SetFloat("_Smoothness", source.GetFloat("_Glossiness"));
                    EditorUtility.SetDirty(material);
                    materials[i] = material;
                    report.AppendLine($"Material: {source.name}, texture={source.mainTexture?.name ?? "none"}");
                }
                renderer.sharedMaterials = materials;
            }

            MeshRenderer capsuleRenderer = root.GetComponent<MeshRenderer>();
            if (capsuleRenderer != null) UnityEngine.Object.DestroyImmediate(capsuleRenderer);
            MeshFilter capsuleMesh = root.GetComponent<MeshFilter>();
            if (capsuleMesh != null) UnityEngine.Object.DestroyImmediate(capsuleMesh);
            Animator animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            EnemyAnimationPlayer player = root.GetComponent<EnemyAnimationPlayer>();
            if (player == null) player = root.AddComponent<EnemyAnimationPlayer>();
            player.animator = animator;
            player.animations = set;
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            report.AppendLine($"Prefab OK: {prefabPath}, visual scale={scale:F4}, height={height:F3}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        report.AppendLine("PASS: Enemy1 zombie configured.");
        File.WriteAllText(ReportPath, report.ToString());
        Debug.Log(report.ToString());
    }

    private static AnimationClip FindClip(string motion)
    {
        string path = ModelFolder + "Ch10_nonPBR@" + motion + ".fbx";
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (clip == null || clip.length <= 0f || clip.legacy)
            throw new InvalidOperationException(path + "に有効なMecanimクリップがありません。");
        return clip;
    }
}
