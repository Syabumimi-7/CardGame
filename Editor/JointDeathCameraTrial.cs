using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// One opt-in enemy prefab only. No game build, source scene replacement or Play mode.
[InitializeOnLoad]
public static class JointDeathCameraTrial
{
    const string Request = "Temp/JointDeathCameraTrial.request";
    const string Folder = "StageReview/WebGLPerformance/DeathCameraTrial/";
    const string PrefabPath = "Assets/Prefab/testEnemy.prefab";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static double nextCheck;
    static JointDeathCameraTrial() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (!File.Exists(Request)) return;
        try
        {
            string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
            if (command == "configure") Configure();
            else if (command == "verify") Verify(false);
            else if (command == "capture") Verify(true);
            else throw new ArgumentException(command);
        }
        catch (Exception e) { File.WriteAllText(Folder + "error.txt", e.ToString()); Debug.LogException(e); }
    }
    static void Configure()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/ScriptableObject/EnemyData/Enemy1.asset");
        if (AssetDatabase.GetAssetPath(data.enemyPrefab) != PrefabPath) throw new InvalidOperationException("Enemy1 prefab changed");
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[]{"Assets/ScriptableObject/EnemyData"}))
        {
            var other = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
            if (other != data && other.enemyPrefab == data.enemyPrefab) throw new InvalidOperationException("Prefab is shared with another enemy");
        }
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var animator = root.GetComponentInChildren<Animator>();
            var transforms = root.GetComponentsInChildren<Transform>(true);
            File.WriteAllText(Folder + "rig.txt", string.Join("\n", transforms.Select(t=>t.name)));
            Func<string,Transform> find = name => transforms.FirstOrDefault(t=>t.name==name || t.name.EndsWith(":"+name) || t.name=="mixamorig"+name);
            var head = find("Head");
            var foot = find("LeftFoot");
            if(head==null || foot==null) throw new InvalidOperationException("Expected Head and LeftFoot joints");
            float scale = Mathf.Max(Mathf.Abs(root.transform.lossyScale.x), Mathf.Abs(root.transform.lossyScale.y), Mathf.Abs(root.transform.lossyScale.z));
            float height = Vector3.Distance(head.position, foot.position) * 1.12f / scale;
            var anchors = new List<JointDeathCameraBounds.Anchor>();
            HumanBodyBones[] bones = { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest,
                HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                HumanBodyBones.LeftHand, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes };
            string[] names={"Hips","Spine","Spine2","Neck","Head","LeftArm","LeftForeArm","LeftHand","RightArm","RightForeArm","RightHand","LeftUpLeg","LeftLeg","LeftFoot","LeftToeBase","RightUpLeg","RightLeg","RightFoot","RightToeBase"};
            for(int index=0;index<bones.Length;index++)
            {
                var bone=bones[index];
                Transform joint = find(names[index]);
                if (joint == null) continue;
                float radius = bone == HumanBodyBones.Hips || bone == HumanBodyBones.Spine || bone == HumanBodyBones.Chest ? .12f :
                    bone == HumanBodyBones.Head || bone == HumanBodyBones.Neck ? .09f : .065f;
                anchors.Add(new JointDeathCameraBounds.Anchor { joint = joint, radius = height * radius });
            }
            var bounds = root.GetComponent<JointDeathCameraBounds>();
            if (bounds == null) bounds = root.AddComponent<JointDeathCameraBounds>();
            bounds.anchors = anchors.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            File.WriteAllText(Folder + "configuration.txt", $"Enemy1 only: {data.enemyName}; anchors={anchors.Count}; reference height={height:R}; prefab={PrefabPath}\n");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    sealed class Shot
    {
        public Vector3 focus;
        public Quaternion rotation;
        public float distance, fov;
        // Same death-camera interpolation and fit as BattleCameraMotion, with a controlled test timestep.
        public void Step(Bounds subject, Camera camera, float delta)
        {
            Vector3 offset = -(rotation * Vector3.forward); offset.y = Mathf.Max(.18f, offset.y);
            float blend = 1 - Mathf.Exp(-delta / .45f);
            focus = Vector3.Lerp(focus, subject.center, blend);
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(-offset.normalized, Vector3.up), blend);
            fov = Mathf.Lerp(fov, 29, blend);
            distance = Mathf.Lerp(distance, Mathf.Max(2.2f, subject.size.y * 1.8f), blend);
            var points = Corners(subject);
            distance = Mathf.Max(distance, BattleCameraMotion.RequiredDistance(points, focus, rotation, fov, camera.aspect, new Rect(.13f,.25f,.74f,.60f), camera.nearClipPlane));
            camera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            camera.fieldOfView = fov;
        }
    }
    static Vector3[] Corners(Bounds bounds)
    {
        var result = new Vector3[8];
        for (int i=0;i<8;i++) result[i]=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
        return result;
    }
    static void Set(Component c, string name, object value) { c.GetType().GetField(name,Private).SetValue(c,value); }

    static void Verify(bool capture)
    {
        var active = SceneManager.GetActiveScene(); bool dirty = active.isDirty;
        var preview = EditorSceneManager.NewPreviewScene();
        GameObject actor = null; Mesh scratch = null;
        var report = new StringBuilder(); float maxCenterDifference=0, maxCameraDifference=0;
        int outsideFrame=0;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            actor = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(actor,preview);
            actor.transform.position=Vector3.zero;
            var bounds = actor.GetComponent<JointDeathCameraBounds>();
            var animation = actor.GetComponent<EnemyAnimationPlayer>();
            if(bounds==null || animation==null) throw new InvalidOperationException("Trial setup missing");
            animation.ResetForBattle();
            var advance=typeof(EnemyAnimationPlayer).GetMethod("AdvanceAnimation",Private);
            advance.Invoke(animation,new object[]{.5f});
            animation.PlayDeath();
            float length=animation.animations.death.length;
            var lightObject=new GameObject("Trial light"); SceneManager.MoveGameObjectToScene(lightObject,preview);
            var light=lightObject.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f;
            lightObject.transform.rotation=Quaternion.Euler(35,-30,0);
            var cameras=new Camera[2];
            for(int i=0;i<2;i++)
            {
                var go=new GameObject("Trial Camera "+i); go.SetActive(false); SceneManager.MoveGameObjectToScene(go,preview);
                cameras[i]=go.AddComponent<Camera>(); cameras[i].aspect=16f/9;
                cameras[i].clearFlags=CameraClearFlags.SolidColor; cameras[i].backgroundColor=new Color(.08f,.09f,.11f);
                cameras[i].nearClipPlane=.05f;
                cameras[i].scene=preview;
            }
            var motion=cameras[1].gameObject.AddComponent<BattleCameraMotion>();
            scratch=new Mesh(); Set(motion,"bakedPose",scratch);
            Set(motion,"enemy",actor.transform); Set(motion,"deathTarget",actor.transform);
            Set(motion,"enemyRenderers",actor.GetComponentsInChildren<Renderer>()); Set(motion,"enemyDeathBounds",bounds);
            var oldBounds=typeof(BattleCameraMotion).GetMethod("ActorBounds",Private);
            var newBounds=typeof(BattleCameraMotion).GetMethod("DeathSubjectBounds",Private);
            var renderers=actor.GetComponentsInChildren<Renderer>();
            var first=(Bounds)oldBounds.Invoke(motion,new object[]{actor.transform,renderers});
            Vector3 initialFocus=first.center+Vector3.left*2;
            Quaternion initialRotation=Quaternion.LookRotation(new Vector3(-.2f,-.18f,1),Vector3.up);
            var shots=new[]{new Shot{focus=initialFocus,rotation=initialRotation,distance=8,fov=45},new Shot{focus=initialFocus,rotation=initialRotation,distance=8,fov=45}};
            float step=1f/30; int frames=Mathf.CeilToInt(length/step);
            for(int frame=0;frame<=frames;frame++)
            {
                if(frame>0) advance.Invoke(animation,new object[]{step});
                scratch.Clear();
                var jointBounds=(Bounds)newBounds.Invoke(motion,null);
                if(scratch.vertexCount!=0) throw new InvalidOperationException("Trial death path baked the mesh");
                var meshBounds=(Bounds)oldBounds.Invoke(motion,new object[]{actor.transform,renderers});
                shots[0].Step(meshBounds,cameras[0],step); shots[1].Step(jointBounds,cameras[1],step);
                float centerDelta=Vector3.Distance(meshBounds.center,jointBounds.center);
                float cameraDelta=Vector3.Distance(cameras[0].transform.position,cameras[1].transform.position);
                maxCenterDifference=Mathf.Max(maxCenterDifference,centerDelta); maxCameraDifference=Mathf.Max(maxCameraDifference,cameraDelta);
                bool clipped=false;
                foreach(var point in Corners(meshBounds))
                {
                    Vector3 screen=cameras[1].WorldToViewportPoint(point);
                    if(screen.z<=0 || screen.x<0 || screen.x>1 || screen.y<0 || screen.y>1) clipped=true;
                }
                if(clipped) outsideFrame++;
                report.AppendLine($"frame={frame}; t={Mathf.Min(frame*step,length):F3}; boundsCenterDelta={centerDelta:F5}; cameraPositionDelta={cameraDelta:F5}; clipped={clipped}; oldSize={meshBounds.size}; newSize={jointBounds.size}");
                if(capture && (frame==0 || frame==frames/2 || frame==104 || frame==frames))
                    for(int i=0;i<2;i++) Capture(cameras[i],Folder+$"frame-{frame:D3}-"+(i==0?"before":"joints")+".png");
            }
            // Disabling/invalidating the opt-in returns to the exact original bounds calculation.
            bounds.enabled=false;
            Bounds fallback=(Bounds)newBounds.Invoke(motion,null);
            Bounds reference=(Bounds)oldBounds.Invoke(motion,new object[]{actor.transform,renderers});
            if(!fallback.Equals(reference)) throw new InvalidOperationException("Disabled fallback differs");
            bounds.enabled=true;
            var saved=bounds.anchors[0]; bounds.anchors[0]=default;
            fallback=(Bounds)newBounds.Invoke(motion,null);
            if(!fallback.Equals(reference)) throw new InvalidOperationException("Missing joint fallback differs");
            bounds.anchors[0]=saved;
            Set(motion,"enemyDeathBounds",null);
            fallback=(Bounds)newBounds.Invoke(motion,null);
            if(!fallback.Equals(reference)) throw new InvalidOperationException("Unconfigured enemy fallback differs");
            Set(motion,"enemyDeathBounds",bounds); Set(motion,"enemy",null);
            Set(motion,"player",actor.transform); Set(motion,"playerRenderers",renderers);
            fallback=(Bounds)newBounds.Invoke(motion,null);
            if(!fallback.Equals(reference)) throw new InvalidOperationException("Player fallback differs");
            Set(motion,"bakedPose",null);
            File.WriteAllText(Folder+"summary.txt",$"Enemy1 death: {frames+1} poses at 30Hz; {bounds.anchors.Length} joints; no BakeMesh on trial death path.\nMaximum bounds center difference={maxCenterDifference:R}; maximum camera position difference={maxCameraDifference:R}.\nOld mesh bounding-box corners outside trial viewport: {outsideFrame} frames.\nDisabled/missing-joint/unconfigured/player fallback equals original bounds.\nNo game build or Play mode; framing is approximate, not identical.\n");
        }
        finally
        {
            if(actor!=null) actor.GetComponent<EnemyAnimationPlayer>().enabled=false;
            EditorSceneManager.ClosePreviewScene(preview);
            if(scratch!=null) Object.DestroyImmediate(scratch);
        }
        if(SceneManager.GetActiveScene()!=active || active.isDirty!=dirty) throw new InvalidOperationException("Working scene changed");
        File.WriteAllText(Folder+"poses.txt",report.ToString());
    }
    static void Capture(Camera camera,string path)
    {
        var target=new RenderTexture(960,540,24); var read=new Texture2D(960,540,TextureFormat.RGB24,false);
        var active=RenderTexture.active;
        try
        {
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            read.ReadPixels(new Rect(0,0,960,540),0,0); read.Apply(); File.WriteAllBytes(path,read.EncodeToPNG());
        }
        finally { camera.targetTexture=null; RenderTexture.active=active; Object.DestroyImmediate(read); Object.DestroyImmediate(target); }
    }
}
