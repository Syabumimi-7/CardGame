using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit isolated investigation only; does not alter game code/prefabs or enter Play mode.
[InitializeOnLoad]
public static class DeathStartInvestigation
{
    const string Request = "Temp/DeathStartInvestigation.request";
    const string Folder = "StageReview/WebGLPerformance/DeathStartInvestigation/";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static double nextCheck;
    static DeathStartInvestigation() { EditorApplication.update += Poll; }
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup<nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        nextCheck=EditorApplication.timeSinceStartup+1;
        if(!File.Exists(Request)) return;
        try { File.Delete(Request); Run(); }
        catch(Exception e) { File.WriteAllText(Folder+"error.txt",e.ToString()); UnityEngine.Debug.LogException(e); }
    }
    struct Pose
    {
        public Vector3 position, scale;
        public Quaternion rotation;
    }
    static Pose[] Snapshot(GameObject root)
    {
        var transforms=root.GetComponentsInChildren<Transform>(true);
        var poses=new Pose[transforms.Length];
        for(int i=0;i<poses.Length;i++) poses[i]=new Pose{position=transforms[i].localPosition,rotation=transforms[i].localRotation,scale=transforms[i].localScale};
        return poses;
    }
    static float Difference(Pose[] a,Pose[] b)
    {
        if(a.Length!=b.Length) throw new InvalidOperationException("Skeleton count differs");
        float max=0;
        for(int i=0;i<a.Length;i++)
        {
            max=Mathf.Max(max,Vector3.Distance(a[i].position,b[i].position),Vector3.Distance(a[i].scale,b[i].scale));
            max=Mathf.Max(max,Quaternion.Angle(a[i].rotation,b[i].rotation));
        }
        return max;
    }
    static double Timed(Action action)
    {
        var timer=Stopwatch.StartNew(); action(); timer.Stop(); return timer.Elapsed.TotalMilliseconds;
    }
    static void Run()
    {
        Scene original=SceneManager.GetActiveScene(); bool dirty=original.isDirty;
        Scene preview=EditorSceneManager.NewPreviewScene();
        var report=new StringBuilder("sample,mode,setup_ms,preparation_ms,death_trigger_ms,first_evaluate_ms,next_120_mean_ms\n");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/testEnemy.prefab");
        var deathType=typeof(EnemyAnimationPlayer);
        var advanceMethod=deathType.GetMethod("AdvanceAnimation",Private);
        var releaseMethod=deathType.GetMethod("ReleaseGraph",Private);
        var shotMethod=deathType.GetMethod("PlayShot",Private);
        var dyingField=deathType.GetField("dying",Private);
        var graphField=deathType.GetField("graph",Private);
        Pose[] firstLazy=null, firstPrepared=null;
        float prepPoseDifference=0;
        EnemyAnimationSet set=null;
        try
        {
            for(int sample=0;sample<6;sample++)
            for(int mode=0;mode<2;mode++)
            {
                GameObject actor=Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(actor,preview);
                var animation=actor.GetComponent<EnemyAnimationPlayer>(); set=animation.animations;
                try
                {
                    var advance=(Action<float>)Delegate.CreateDelegate(typeof(Action<float>),animation,advanceMethod);
                    double setup=Timed(animation.ResetForBattle);
                    advance(.5f);
                    double preparation=0;
                    if(mode==1)
                    {
                        Pose[] before=Snapshot(actor);
                        preparation=Timed(()=>
                        {
                            shotMethod.Invoke(animation,new object[]{set.death,1f});
                            ((PlayableGraph)graphField.GetValue(animation)).Evaluate(0f);
                        });
                        prepPoseDifference=Mathf.Max(prepPoseDifference,Difference(before,Snapshot(actor)));
                    }
                    // Prepared mode simulates moving node creation/binding to battle setup.
                    // Reflection setter cost remains in the prepared trigger measurement.
                    double trigger=mode==0?Timed(animation.PlayDeath):Timed(()=>dyingField.SetValue(animation,true));
                    double first=Timed(()=>advance(1f/60));
                    if(sample==0)
                    {
                        if(mode==0) firstLazy=Snapshot(actor); else firstPrepared=Snapshot(actor);
                    }
                    double steady=Timed(()=>{for(int i=0;i<120;i++) advance(1f/60);})/120;
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R},{3:R},{4:R},{5:R},{6:R}",sample,mode==0?"current_lazy":"prepared_probe",setup,preparation,trigger,first,steady));
                }
                finally { releaseMethod.Invoke(animation,null); Object.DestroyImmediate(actor); }
            }
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        if(SceneManager.GetActiveScene()!=original || original.isDirty!=dirty) throw new InvalidOperationException("Working scene changed");
        var clip=set.death;
        File.WriteAllText(Folder+"timings.csv",report.ToString());
        File.WriteAllText(Folder+"summary.txt",$"Editor-only CPU experiment, 6 interleaved pairs; not browser frame timings.\nClip length={clip.length:R}; fps={clip.frameRate:R}; curve bindings={AnimationUtility.GetCurveBindings(clip).Length}; object bindings={AnimationUtility.GetObjectReferenceCurveBindings(clip).Length}; runtime clip bytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip)}.\nPreparation pose difference (max position/scale distance or quaternion angle)={prepPoseDifference:R}.\nFirst death pose difference between lazy/prepared probe={Difference(firstLazy,firstPrepared):R}.\nNo game code/prefab/build changes; active scene unchanged, dirty={dirty}.\n");
    }
}
