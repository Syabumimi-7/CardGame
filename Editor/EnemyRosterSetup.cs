using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class EnemyRosterSetup
{
    private const string Request = "Temp/EnemyRoster.request";
    private const string Report = "Temp/EnemyRoster-report.txt";
    private static readonly string[] Names = { "Enemy1", "Enemy2", "Enemy3", "Enemy4", "Enemy5", "Enemy6", "Boss1", "Boss2" };
    static EnemyRosterSetup() { EditorApplication.update += Update; }
    private static void Update()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "inspect") Inspect();
            else if (command == "build") Build();
            else if (command == "verify") Verify();
            else if (command == "verify-spawns") VerifySpawns();
            else throw new InvalidOperationException("Unknown command: " + command);
        }
        catch (Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
    }
    private static void Inspect()
    {
        var report = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            report.AppendLine($"Scene: {scene.path}, dirty={scene.isDirty}");
        }
        foreach (string name in Names)
        {
            string path = ModelPath(name);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var clip in new[] { DedicatedClip(name, "Idle"), DedicatedClip(name, name == "Enemy1" ? "Dying" : "Sword And Shield Death") })
            {
                var missing = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform) && b.path.Length > 0 && model.transform.Find(b.path) == null).Select(b => b.path).Distinct().ToArray();
                report.AppendLine($"{name}: {clip.name}, missing={missing.Length}: {string.Join(", ", missing.Take(3))}");
            }
            report.AppendLine("Bones: " + string.Join(", ", model.GetComponentsInChildren<Transform>().Take(8).Select(t => t.name)));
        }
        File.WriteAllText(Report, report.ToString());
    }

    private const string DataFolder = "Assets/ScriptableObject/EnemyData/";

    private static string ModelPath(string name) => Directory.GetFiles("Assets/model/Enemy/" + name, "*.fbx").Single(p => !Path.GetFileName(p).Contains("@")).Replace('\\', '/');
    private static AnimationClip DedicatedClip(string name, string motion)
    {
        string path = Path.ChangeExtension(ModelPath(name), null) + "@" + motion + ".fbx";
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
        Require(clip.length > 0 && !clip.legacy, "Invalid clip: " + path);
        return clip;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private static void ValidateBindings(GameObject model, AnimationClip clip)
    {
        Require(!clip.isHumanMotion, "Expected a dedicated Generic clip: " + clip.name);
        var missing = AnimationUtility.GetCurveBindings(clip)
            .Where(b => b.type == typeof(Transform) && b.path.Length > 0 && model.transform.Find(b.path) == null)
            .Select(b => b.path).Distinct().ToArray();
        Require(missing.Length == 0, model.name + ": missing animation bones: " + string.Join(", ", missing));
    }

    [MenuItem("Tools/Enemies/Build Enemy Roster")]
    public static void Build()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before building.");
        // Check every source before changing the assets. Dedicated clips retain their original skeleton.
        foreach (string name in Names)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name));
            ValidateBindings(model, DedicatedClip(name, "Idle"));
            ValidateBindings(model, DedicatedClip(name, name == "Enemy1" ? "Dying" : "Sword And Shield Death"));
            Require(AssetDatabase.LoadAssetAtPath<EnemyData>(DataFolder + name + ".asset") != null, "Missing data: " + name);
        }
        foreach (string name in Names)
        {
            string modelPath = ModelPath(name);
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer.animationType != ModelImporterAnimationType.Generic || importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.SaveAndReimport();
            }
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DataFolder + name + ".asset");
            string setPath = DataFolder + name + "Animations.asset";
            var set = AssetDatabase.LoadAssetAtPath<EnemyAnimationSet>(setPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<EnemyAnimationSet>();
                var previous = data.enemyPrefab.GetComponent<EnemyAnimationPlayer>();
                if (previous != null && previous.animations != null) set.blendDuration = previous.animations.blendDuration;
                AssetDatabase.CreateAsset(set, setPath);
            }
            set.idle = DedicatedClip(name, "Idle");
            set.death = DedicatedClip(name, name == "Enemy1" ? "Dying" : "Sword And Shield Death");
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssetIfDirty(set);

            string prefabPath = name == "Boss1" ? "Assets/Prefab/Boss1.prefab" : AssetDatabase.GetAssetPath(data.enemyPrefab);
            if (name == "Boss1" && !File.Exists(prefabPath)) CreateBossPrefab(data, modelPath, prefabPath);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var player = root.GetComponent<EnemyAnimationPlayer>();
                if (player == null) player = root.AddComponent<EnemyAnimationPlayer>();
                var animator = root.GetComponentInChildren<Animator>();
                if (animator == null) animator = root.transform.GetChild(0).gameObject.AddComponent<Animator>();
                animator.avatar = null;
                animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false;
                player.animator = animator;
                player.animations = set;
                ValidateBindings(animator.gameObject, set.idle);
                ValidateBindings(animator.gameObject, set.death);
                foreach (var material in root.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
                    FixNormalMap(material);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            if (name == "Boss1")
            {
                data.enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
        }
        Verify();
        VerifySpawns();
    }

    private static void FixNormalMap(Material material)
    {
        if (material == null || !material.HasProperty("_BumpMap")) return;
        var texture = material.GetTexture("_BumpMap");
        if (texture == null) return;
        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture)) as TextureImporter;
        Require(importer != null, "Normal map must be an extracted texture: " + texture.name);
        if (importer.textureType == TextureImporterType.NormalMap) return;
        importer.textureType = TextureImporterType.NormalMap;
        importer.SaveAndReimport();
    }

    private static void CreateBossPrefab(EnemyData data, string modelPath, string prefabPath)
    {
        string folder = "Assets/model/Enemy/Boss1";
        Folder(folder + "/Textures");
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.ExtractTextures(folder + "/Textures");
        AssetDatabase.Refresh();
        importer.SaveAndReimport();
        Folder(folder + "/Materials");
        var root = UnityEngine.Object.Instantiate(data.enemyPrefab);
        root.name = "Boss1";
        root.transform.position = Vector3.zero;
        try
        {
            var oldRenderer = root.GetComponent<MeshRenderer>();
            if (oldRenderer != null) UnityEngine.Object.DestroyImmediate(oldRenderer);
            var oldMesh = root.GetComponent<MeshFilter>();
            if (oldMesh != null) UnityEngine.Object.DestroyImmediate(oldMesh);
            if (root.GetComponent<EnemyInstance>() == null) root.AddComponent<EnemyInstance>();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            visual.name = "Boss1Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(0, 180, 0);
            visual.transform.localScale = Vector3.one;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            Require(renderers.Length > 0, "Boss1 has no renderers");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var capsule = root.GetComponent<CapsuleCollider>();
            Require(capsule != null && bounds.size.y > 0, "Invalid Boss1 size");
            float scale = capsule.height / bounds.size.y;
            Vector3 center = root.transform.InverseTransformPoint(bounds.center);
            visual.transform.localScale = Vector3.one * scale;
            visual.transform.localPosition = capsule.center - center * scale;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "Missing URP shader");
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    Require(source != null, "Missing Boss1 material");
                    string path = folder + "/Materials/" + source.name.Replace('/', '_') + "_URP.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(shader);
                        AssetDatabase.CreateAsset(material, path);
                    }
                    material.SetTexture("_BaseMap", source.mainTexture);
                    material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.color : Color.white);
                    material.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.2f);
                    if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
                    {
                        material.SetTexture("_BumpMap", source.GetTexture("_BumpMap"));
                        material.EnableKeyword("_NORMALMAP");
                    }
                    FixNormalMap(material);
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static EnemyData[] NormalRoster() => Enumerable.Range(1, 6).Select(i => AssetDatabase.LoadAssetAtPath<EnemyData>(DataFolder + "Enemy" + i + ".asset")).ToArray();
    private static EnemyData[] BossRoster() => Enumerable.Range(1, 2).Select(i => AssetDatabase.LoadAssetAtPath<EnemyData>(DataFolder + "Boss" + i + ".asset")).ToArray();

    [MenuItem("Tools/Enemies/Verify Enemy Roster")]
    public static void Verify()
    {
        var report = new StringBuilder();
        var step = typeof(EnemyAnimationPlayer).GetMethod("AdvanceAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var data in NormalRoster().Concat(BossRoster()))
        {
            Require(data != null && data.enemyPrefab != null, "Missing enemy data/prefab");
            var preview = new PreviewRenderUtility();
            var root = UnityEngine.Object.Instantiate(data.enemyPrefab);
            root.transform.position = Vector3.zero;
            preview.AddSingleGO(root);
            try
            {
                var enemy = root.GetComponent<EnemyInstance>();
                var player = root.GetComponent<EnemyAnimationPlayer>();
                Require(player != null && player.animator != null, "Missing animation references: " + data.name);
                Require(root.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit" && m.GetTexture("_BaseMap") != null)), "Missing textures: " + data.name);
                Require(player.animations.idle == DedicatedClip(data.name, "Idle") &&
                    player.animations.death == DedicatedClip(data.name, data.name == "Enemy1" ? "Dying" : "Sword And Shield Death"), "Wrong dedicated clips: " + data.name);
                ValidateBindings(player.animator.gameObject, player.animations.idle);
                ValidateBindings(player.animator.gameObject, player.animations.death);
                Require(root.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0 && root.GetComponent<MeshRenderer>() == null, "Missing character model: " + data.name);
                foreach (var material in root.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials))
                {
                    var normal = material.GetTexture("_BumpMap");
                    Require(normal == null || ((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal))).textureType == TextureImporterType.NormalMap, "Normal texture setting: " + data.name);
                }
                enemy.Setup(data, 1);
                var bones = player.animator.GetComponentsInChildren<Transform>();
                var initial = bones.Select(b => b.localRotation).ToArray();
                for (int i = 0; i < 36; i++) step.Invoke(player, new object[] { 1f / 60f });
                Require(bones.Where((b, i) => Quaternion.Angle(b.localRotation, initial[i]) > 0.1f).Any(), "Idle does not animate: " + data.name);
                Capture(preview, data.name + "-idle");
                enemy.TakeDamage(enemy.maxHP);
                Require(enemy.IsDead && enemy.IsAnimating, "Death did not start: " + data.name);
                for (int i = 0; i < Mathf.CeilToInt((player.animations.death.length + 0.2f) * 60f); i++) step.Invoke(player, new object[] { 1f / 60f });
                Require(!enemy.IsAnimating, "Death blocks progression: " + data.name);
                Capture(preview, data.name + "-death");
                var dead = bones.Select(b => b.localRotation).ToArray();
                step.Invoke(player, new object[] { 1f });
                Require(!bones.Where((b, i) => Quaternion.Angle(b.localRotation, dead[i]) > 0.1f).Any(), "Death pose not held: " + data.name);
                enemy.Setup(data, 9);
                Require(!enemy.IsDead && !enemy.IsAnimating && enemy.maxHP == Mathf.RoundToInt(data.baseMaxHP * Mathf.Pow(data.scalingFactor, 8)), "Reset/stats failure: " + data.name);
                report.AppendLine("PASS " + data.name + ": dedicated clips/bones, character model, textures/normal maps, moving idle, death, final pose, reset.");
            }
            finally { preview.Cleanup(); }
        }
        report.AppendLine("PASS assets. Scene registration: " + string.Join(", ", NormalRoster().Select(d => d.name)) + "; " + string.Join(", ", BossRoster().Select(d => d.name)));
        File.WriteAllText(Report, report.ToString());
    }

    private static void Capture(PreviewRenderUtility preview, string name)
    {
        preview.BeginStaticPreview(new Rect(0, 0, 640, 640));
        preview.camera.transform.position = new Vector3(0, 0.4f, -4.5f);
        preview.camera.transform.LookAt(new Vector3(0, -0.1f, 0));
        preview.camera.nearClipPlane = 0.01f;
        preview.camera.farClipPlane = 50;
        preview.camera.fieldOfView = 40;
        preview.camera.clearFlags = CameraClearFlags.Color;
        preview.camera.backgroundColor = new Color(0.13f, 0.15f, 0.18f);
        preview.lights[0].intensity = 1.3f;
        preview.lights[0].transform.rotation = Quaternion.Euler(35, 150, 0);
        preview.lights[1].intensity = 0.8f;
        preview.ambientColor = Color.gray;
        preview.Render(true);
        var image = preview.EndStaticPreview();
        File.WriteAllBytes("Temp/" + name + ".png", image.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(image);
    }

    [MenuItem("Tools/Enemies/Verify Enemy Spawns")]
    public static void VerifySpawns()
    {
        var report = new StringBuilder();
        var randomState = UnityEngine.Random.state;
        var active = SceneManager.GetActiveScene();
        var expectedNormals = NormalRoster();
        var expectedBosses = BossRoster();
        Require(expectedNormals.All(d => d != null && d.enemyType == EnemyType.Normal), "Invalid normal roster");
        Require(expectedBosses.All(d => d != null && d.enemyType == EnemyType.Boss), "Invalid boss roster");
        string scriptGuid = AssetDatabase.AssetPathToGUID("Assets/Script/EnemyManager.cs");
        int managerCount = 0;
        try
        {
            foreach (string path in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                if (!File.ReadAllText(path).Contains(scriptGuid)) continue;
                var scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    foreach (var source in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyManager>(true)))
                    {
                        Require(source.normalEnemies.SequenceEqual(expectedNormals) && source.bossEnemies.SequenceEqual(expectedBosses), "Scene roster mismatch: " + path);
                        managerCount++;
                        var previewScene = EditorSceneManager.NewPreviewScene();
                        var root = new GameObject("EnemySpawnVerification");
                        SceneManager.MoveGameObjectToScene(root, previewScene);
                        var manager = root.AddComponent<EnemyManager>();
                        manager.enemySpawnPoint = root.transform;
                        manager.normalEnemies = source.normalEnemies.ToList();
                        manager.bossEnemies = source.bossEnemies.ToList();
                        var seen = new System.Collections.Generic.HashSet<EnemyData>();
                        UnityEngine.Random.InitState(240924);
                        try
                        {
                            for (int battle = 1; battle <= 10; battle++)
                            {
                                for (int trial = 0; trial < 24; trial++)
                                {
                                    manager.SpawnEnemy(battle);
                                    var enemy = manager.CurrentEnemy;
                                    Require(enemy != null, "No spawned enemy");
                                    var expected = battle == 10 ? expectedBosses : expectedNormals;
                                    Require(expected.Contains(enemy.Data), "Wrong enemy at battle " + battle);
                                    Require(enemy.currentHP == Mathf.RoundToInt(enemy.Data.baseMaxHP * Mathf.Pow(enemy.Data.scalingFactor, battle - 1)), "Incorrect spawn HP");
                                    seen.Add(enemy.Data);
                                    // Immediate cleanup is required by this Edit-mode preview test.
                                    UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                                }
                            }
                            Require(expectedNormals.Concat(expectedBosses).All(seen.Contains), "Not every enemy was generated");
                            report.AppendLine("PASS " + path + ": exact 6 normal/2 boss references; 240 real SpawnEnemy calls; battles 1-9 normals only, battle 10 bosses only; all 8 types generated.");
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(root);
                            EditorSceneManager.ClosePreviewScene(previewScene);
                        }
                    }
                }
                finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            }
            Require(managerCount > 0, "No scene EnemyManager found");
            File.WriteAllText("Temp/EnemyRoster-spawns-report.txt", report.ToString());
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            SceneManager.SetActiveScene(active);
        }
    }
}
