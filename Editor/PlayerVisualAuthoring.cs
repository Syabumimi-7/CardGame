using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>明示実行でプレイヤーPrefabとRuinsStageRefinedの参照を作成します。</summary>
[InitializeOnLoad]
public static class PlayerVisualAuthoring
{
    public const string ScenePath = "Assets/Scenes/RuinsStageRefined.unity";
    private const string Request = "Temp/PlayerVisual.request";
    private const string Report = "StageReview/player-visual-authoring.txt";
    static PlayerVisualAuthoring() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            if (command == "inspect") Inspect();
            else if (command == "build") Build();
            else if (command == "center-spawn") CenterSpawn();
            else if (command == "verify") PlayerVisualVerification.Begin();
            else throw new ArgumentException(command);
        }
        catch (Exception e) { File.WriteAllText(Report, e.ToString()); Debug.LogException(e); }
    }

    private static string ModelPath(int number) => Directory.GetFiles("Assets/model/Player/Player" + number, "*.fbx")
        .Single(p => !Path.GetFileName(p).Contains("@")).Replace('\\', '/');
    private static AnimationClip Clip(int number, bool death)
    {
        string path = Directory.GetFiles("Assets/model/Player/Player" + number, "*.fbx")
            .Single(p => death ? p.Contains("Death") : p.Contains("@Idle")).Replace('\\', '/');
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
    }
    public static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private static void Inspect()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            sb.AppendLine($"Scene: {scene.path}, dirty={scene.isDirty}");
        }
        sb.AppendLine($"Camera: {Camera.main?.transform.position}, rotation={Camera.main?.transform.eulerAngles}");
        for (int i = 1; i <= 3; i++)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(i));
            sb.AppendLine($"Player{i}: {ModelPath(i)} idle={Clip(i, false).length} death={Clip(i, true).length}");
            foreach (var material in model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct())
                sb.AppendLine($"Material: {material.name}, shader={material.shader.name}, texture={material.mainTexture?.name}");
        }
        File.WriteAllText(Report, sb.ToString());
    }

    [MenuItem("Tools/Players/Build Battle Players")]
    public static void Build()
    {
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode first.");
        var scene = SceneManager.GetActiveScene();
        Require(scene.path == ScenePath && !scene.isDirty, "Open the saved RuinsStageRefined scene first.");
        var battle = Object.FindObjectOfType<BattleManager>(true);
        Require(battle != null && battle.enemyManager.enemySpawnPoint != null, "Battle references missing");
        for (int i = 1; i <= 3; i++)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(i));
            ValidateBindings(model, Clip(i, false));
            ValidateBindings(model, Clip(i, true));
        }
        var prefabs = new PlayerBattleVisual[3];
        for (int i = 1; i <= 3; i++) prefabs[i - 1] = BuildPrefab(i);
        var manager = battle.GetComponent<PlayerVisualManager>();
        if (manager == null) manager = Undo.AddComponent<PlayerVisualManager>(battle.gameObject);
        Undo.RecordObject(manager, "Configure player visuals");
        manager.playerPrefabs = prefabs;
        if (manager.playerSpawnPoint == null)
        {
            var anchor = new GameObject("PlayerSpawnPoint");
            Undo.RegisterCreatedObjectUndo(anchor, "Create player spawn point");
            anchor.transform.SetParent(battle.enemyManager.enemySpawnPoint.parent, true);
            Vector3 enemy = battle.enemyManager.enemySpawnPoint.position;
            float floorY = GameObject.Find("Ruins Stage").transform.position.y;
            anchor.transform.position = new Vector3(enemy.x, floorY + .02f, enemy.z - 3f);
            anchor.transform.rotation = Quaternion.LookRotation(new Vector3(enemy.x - anchor.transform.position.x, 0, enemy.z - anchor.transform.position.z));
            manager.playerSpawnPoint = anchor.transform;
        }
        Undo.RecordObject(battle, "Assign player visuals");
        battle.playerVisualManager = manager;
        EditorUtility.SetDirty(battle);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Report, "PASS: three player prefabs, dedicated clips, URP materials, editable PlayerSpawnPoint, scene references saved.\n");
        VerifyAssets();
    }

    [MenuItem("Tools/Players/Center Player Spawn")]
    private static void CenterSpawn()
    {
        var scene = SceneManager.GetActiveScene();
        Require(!EditorApplication.isPlayingOrWillChangePlaymode && scene.path == ScenePath,
            "Open RuinsStageRefined outside Play mode first.");
        var manager = Object.FindObjectOfType<PlayerVisualManager>(true);
        Require(manager != null && manager.playerSpawnPoint != null, "Player spawn point missing");
        var anchor = manager.playerSpawnPoint;
        Undo.RecordObject(anchor, "Center player spawn");
        anchor.SetPositionAndRotation(new Vector3(0f, anchor.position.y, -1.5f), Quaternion.identity);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText("StageReview/player-position.txt", $"Saved {scene.path}\nPlayerSpawnPoint position={anchor.position:F3}, rotation={anchor.eulerAngles:F3}\n");
    }

    private static void ValidateBindings(GameObject model, AnimationClip clip)
    {
        Require(clip != null && clip.length > 0 && !clip.legacy && !clip.isHumanMotion, "Expected dedicated Generic clip");
        var missing = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(Transform)
            && b.path.Length > 0 && model.transform.Find(b.path) == null).Select(b => b.path).Distinct();
        Require(!missing.Any(), model.name + ": missing animation bones: " + string.Join(",", missing));
    }

    private static PlayerBattleVisual BuildPrefab(int number)
    {
        string modelPath = ModelPath(number);
        string folder = "Assets/model/Player/Player" + number;
        if (!AssetDatabase.IsValidFolder(folder + "/Textures")) AssetDatabase.CreateFolder(folder, "Textures");
        if (!AssetDatabase.IsValidFolder(folder + "/Materials")) AssetDatabase.CreateFolder(folder, "Materials");
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.ExtractTextures(folder + "/Textures");
        AssetDatabase.Refresh();
        importer.SaveAndReimport();
        var root = new GameObject("Player" + number);
        try
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
            var animator = visual.GetComponent<Animator>();
            if (animator == null) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var playback = root.AddComponent<PlayerBattleVisual>();
            playback.animator = animator;
            playback.idle = Clip(number, false);
            playback.death = Clip(number, true);
            playback.idle.SampleAnimation(visual, 0f);
            Bounds bounds = PosedBounds(root);
            Require(bounds.size.y > .01f, "Empty model");
            // 人物の足元をPrefab原点に揃え、既存の敵と近い身長にします。
            float scale = 1.8f / bounds.size.y;
            visual.transform.localScale *= scale;
            visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var source = materials[slot];
                    Require(source != null, "Missing material");
                    string path = folder + "/Materials/" + source.name.Replace('/', '_') + "_URP.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(material, path);
                    }
                    material.SetTexture("_BaseMap", source.mainTexture);
                    material.SetColor("_BaseColor", source.HasProperty("_Color") ? source.color : Color.white);
                    material.SetFloat("_Smoothness", .2f);
                    material.SetFloat("_Cull", 0f);
                    if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null)
                    {
                        var normal = source.GetTexture("_BumpMap");
                        var textureImporter = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                        if (textureImporter != null && textureImporter.textureType != TextureImporterType.NormalMap)
                        {
                            textureImporter.textureType = TextureImporterType.NormalMap;
                            textureImporter.SaveAndReimport();
                        }
                        material.SetTexture("_BumpMap", normal);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    EditorUtility.SetDirty(material);
                    AssetDatabase.SaveAssetIfDirty(material);
                    materials[slot] = material;
                }
                renderer.sharedMaterials = materials;
            }
            string prefabPath = "Assets/Prefab/Player" + number + ".prefab";
            return PrefabUtility.SaveAsPrefabAsset(root, prefabPath).GetComponent<PlayerBattleVisual>();
        }
        finally { Object.DestroyImmediate(root); }
    }

    public static Bounds PosedBounds(GameObject root)
    {
        Bounds bounds = default;
        bool first = true;
        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh = new Mesh();
            renderer.BakeMesh(mesh);
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 point = renderer.transform.TransformPoint(vertex);
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
            Object.DestroyImmediate(mesh);
        }
        return bounds;
    }

    private static void VerifyAssets()
    {
        var step = typeof(PlayerBattleVisual).GetMethod("AdvanceAnimation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        for (int i = 1; i <= 3; i++)
        {
            var preview = new PreviewRenderUtility();
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player" + i + ".prefab"));
            preview.AddSingleGO(root);
            try
            {
                var playback = root.GetComponent<PlayerBattleVisual>();
                ValidateBindings(playback.animator.gameObject, playback.idle);
                ValidateBindings(playback.animator.gameObject, playback.death);
                Require(root.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null && m.shader.name == "Universal Render Pipeline/Lit")), "Invalid materials");
                playback.PlayIdle();
                var bones = playback.animator.GetComponentsInChildren<Transform>();
                var initial = bones.Select(b => b.localRotation).ToArray();
                step.Invoke(playback, new object[] { .6f });
                Require(bones.Where((b, n) => Quaternion.Angle(b.localRotation, initial[n]) > .1f).Any(), "Idle does not move");
                playback.PlayDeath();
                Require(playback.IsAnimatingDeath, "Death did not start");
                step.Invoke(playback, new object[] { playback.death.length + .1f });
                Require(!playback.IsAnimatingDeath, "Death did not end");
                var dead = bones.Select(b => b.localRotation).ToArray();
                playback.PlayDeath();
                step.Invoke(playback, new object[] { 1f });
                Require(!bones.Where((b, n) => Quaternion.Angle(b.localRotation, dead[n]) > .1f).Any(), "Death pose not held");
                playback.PlayIdle();
                Require(!playback.IsDying && !playback.IsAnimatingDeath, "Idle reset failed");
                File.AppendAllText(Report, $"PASS Player{i}: skeleton bindings, URP materials, moving idle, death completion, held final pose, reset; idle={playback.idle.length:F3}s death={playback.death.length:F3}s\n");
            }
            finally { preview.Cleanup(); }
        }
    }
}
