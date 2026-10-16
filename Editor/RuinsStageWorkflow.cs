using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Editor-only, explicitly requested authoring/inspection commands. Never runs in a player.
[InitializeOnLoad]
public static class RuinsStageWorkflow
{
    private const string Request = "Temp/RuinsStageWorkflow.request";
    private const string Report = "Temp/RuinsStageWorkflow.txt";
    private static double nextCheck;
    static RuinsStageWorkflow() { EditorApplication.update += Update; }
    private static void Update()
    {
        if(EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        nextCheck=EditorApplication.timeSinceStartup+1;
        if(File.Exists("Temp/RuinsStageRefresh.request"))
        {
            File.Delete("Temp/RuinsStageRefresh.request");AssetDatabase.Refresh();return;
        }
        if(!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        string command=File.ReadAllText(Request).Trim();File.Delete(Request);
        try
        {
            if(command=="inspect") Inspect();
            else if(command=="build") { RuinsStageBuilder.Build(); Inspect(); }
            else if(command=="capture") Capture();
            else if(command=="tune") Tune();
            else if(command=="verify-play") RuinsStagePlayVerification.Begin();
            else throw new InvalidOperationException("Unknown stage command: " + command);
        }
        catch(Exception e) { File.WriteAllText(Report,e.ToString());Debug.LogException(e); }
    }
    private static void Inspect()
    {
        var scene=SceneManager.GetActiveScene();var sb=new StringBuilder();
        sb.AppendLine($"Scene: {scene.path}, dirty={scene.isDirty}");
        sb.AppendLine($"Camera: {Camera.main.transform.position} FOV={Camera.main.fieldOfView}");
        var enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/testEnemy.prefab"));
        enemy.transform.position=GameObject.Find("EnemySpawnPoint").transform.position;
        var animation=enemy.GetComponent<EnemyAnimationPlayer>();animation.animations.idle.SampleAnimation(animation.animator.gameObject,.2f);
        foreach(var renderer in enemy.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh=new Mesh();renderer.BakeMesh(mesh);
            sb.AppendLine($"Enemy posed Y: {mesh.vertices.Min(v=>renderer.transform.TransformPoint(v).y)} .. {mesh.vertices.Max(v=>renderer.transform.TransformPoint(v).y)}");
            Object.DestroyImmediate(mesh);
        }
        Object.DestroyImmediate(enemy);
        foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/model/") && p.EndsWith(".blend") && !AssetDatabase.IsValidFolder(p)))
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null) continue;
            foreach(var mf in asset.GetComponentsInChildren<MeshFilter>())
                sb.AppendLine($"Mesh {mf.name}: rotation={mf.transform.rotation.eulerAngles}, scale={mf.transform.lossyScale}, size={mf.sharedMesh.bounds.size}");
        }
        File.WriteAllText(Report,sb.ToString());
        Directory.CreateDirectory("StageReview");
        foreach(string path in AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/HDRI/") && p.EndsWith(".exr")))
            PreviewHDR(path);
    }
    private static void PreviewHDR(string path)
    {
        var source=AssetDatabase.LoadAssetAtPath<Texture>(path);
        var rt=RenderTexture.GetTemporary(1024,512,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var previous=RenderTexture.active;var read=new Texture2D(1024,512,TextureFormat.RGBAFloat,false,true);
        var result=new Texture2D(1024,512,TextureFormat.RGB24,false);
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,1024,512),0,0);read.Apply();
            var colors=read.GetPixels();
            for(int i=0;i<colors.Length;i++)
            {
                var c=colors[i]*.25f;
                colors[i]=new Color(Mathf.LinearToGammaSpace(c.r/(1+c.r)),Mathf.LinearToGammaSpace(c.g/(1+c.g)),Mathf.LinearToGammaSpace(c.b/(1+c.b)));
            }
            result.SetPixels(colors);result.Apply();File.WriteAllBytes("Temp/"+Path.GetFileNameWithoutExtension(path)+".png",result.EncodeToPNG());
        }
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(read);Object.DestroyImmediate(result);}
    }
    private static void Capture()
    {
        RuinsStageBuilder.Capture(Camera.main,"StageReview/ruins-refined.png",1600,900);
        File.WriteAllText(Report,"Capture complete: " + DateTime.Now.ToString("O"));
    }
    [Serializable] public class Tuning
    {
        public float exposure=.3f,skyExposure=.45f,skyRotation=130,fill=1.25f,sun=2.5f,moss=.26f;
        public float surfaceY=2.1f;
    }
    private static void Tune()
    {
        if(SceneManager.GetActiveScene().path!=RuinsStageBuilder.ScenePath)throw new InvalidOperationException("Refined scene required.");
        var tuning=JsonUtility.FromJson<Tuning>(File.ReadAllText("Temp/RuinsStageTune.json"));
        GameObject.Find("Soft sky fill").GetComponent<Light>().intensity=tuning.fill;
        GameObject.Find("Directional Light").GetComponent<Light>().intensity=tuning.sun;
        RenderSettings.skybox.SetFloat("_Exposure",tuning.skyExposure);RenderSettings.skybox.SetFloat("_Rotation",tuning.skyRotation);
        EditorUtility.SetDirty(RenderSettings.skybox);
        foreach(string name in new[]{"Weathered limestone","Ancient paving","Fractured stone"})
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(RuinsStageBuilder.Folder+"/"+name+".mat");
            mat.SetFloat("_MossAmount",tuning.moss);EditorUtility.SetDirty(mat);
        }
        var profile=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(RuinsStageBuilder.Folder+"/Evening grade.asset");
        profile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var grade);grade.postExposure.Override(tuning.exposure);EditorUtility.SetDirty(grade);
        var root=GameObject.Find("Ruins Stage").transform;
        float offset=tuning.surfaceY-root.position.y;root.position+=Vector3.up*offset;Camera.main.transform.position+=Vector3.up*offset;
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());Capture();
    }
}
