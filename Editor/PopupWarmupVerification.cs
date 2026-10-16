using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PopupWarmupVerification
{
    const string Request="Temp/PopupWarmupVerification.request";
    const string Folder="StageReview/WebGLPerformance/WarmupFix/";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static double nextCheck;
    static PopupWarmupVerification(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(EditorApplication.timeSinceStartup<nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
        nextCheck=EditorApplication.timeSinceStartup+1;
        if(!File.Exists(Request))return;
        try{File.Delete(Request);Verify();}
        catch(Exception e){File.WriteAllText(Folder+"error.txt",e.ToString());Debug.LogException(e);}
    }
    static object Get(object target,string field)=>target.GetType().GetField(field,Private|BindingFlags.Public).GetValue(target);
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private|BindingFlags.Public).SetValue(target,value);
    static void Assert(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    static GameObject New(Scene scene,string name,bool active)
    {
        var go=new GameObject(name);go.SetActive(active);SceneManager.MoveGameObjectToScene(go,scene);return go;
    }
    static DamagePopupUI UI(Scene scene,string name)
    {
        var go=New(scene,name,false);var owner=go.AddComponent<BattleManager>();
        var ui=go.AddComponent<DamagePopupUI>();ui.Initialize(owner);
        var root=(RectTransform)Get(ui,"root");root.SetParent(null); // Activate graphics, never the test BattleManager.
        return ui;
    }
    static int Count(object collection)=>(int)collection.GetType().GetProperty("Count").GetValue(collection);
    static string Snapshot(DamagePopupUI ui)
    {
        var list=(IList)Get(ui,"active");var p=list[list.Count-1];var text=(TextMeshProUGUI)Get(p,"text");
        var effect=(DamagePopupGraphic)Get(p,"effect");text.ForceMeshUpdate();
        var s=new StringBuilder();
        s.AppendLine($"text={text.text}; fontSize={text.fontSize}; color={JsonUtility.ToJson(text.color)}; style={text.fontStyle}; outline={text.outlineWidth}; materialKeywords={string.Join(",",text.fontMaterial.shaderKeywords.OrderBy(k=>k))}");
        foreach(string field in new[]{"offset","elapsed","delay","localAnchor","lastAnchor"})s.AppendLine(field+"="+JsonUtility.ToJson(Get(p,field)));
        var rect=(RectTransform)Get(p,"rect");s.AppendLine("scale="+JsonUtility.ToJson(rect.localScale));
        for(int mesh=0;mesh<text.textInfo.meshInfo.Length;mesh++)
        {
            var info=text.textInfo.meshInfo[mesh];s.AppendLine("textVertices="+info.vertexCount);
            for(int i=0;i<info.vertexCount;i++)s.AppendLine(JsonUtility.ToJson(info.vertices[i])+JsonUtility.ToJson(info.colors32[i])+JsonUtility.ToJson(info.uvs0[i]));
        }
        using(var vh=new VertexHelper())
        {
            typeof(DamagePopupGraphic).GetMethod("OnPopulateMesh",Private|BindingFlags.DeclaredOnly).Invoke(effect,new object[]{vh});
            var vertices=new List<UIVertex>();vh.GetUIVertexStream(vertices);
            foreach(var v in vertices)s.AppendLine(JsonUtility.ToJson(v));
        }
        return s.ToString();
    }
    static void Verify()
    {
        var scene=SceneManager.GetActiveScene();bool dirty=scene.isDirty;
        var random=UnityEngine.Random.state;var preview=EditorSceneManager.NewPreviewScene();
        var results=new StringBuilder();
        try
        {
            var warm=UI(preview,"Prepared popup");var fresh=UI(preview,"Fresh popup");
            var root=(RectTransform)Get(warm,"root");
            Assert(root.gameObject.activeInHierarchy,"Warmup root must be active");
            var prepare=warm.PrepareForBattle();Assert(prepare.MoveNext(),"First preparation must yield a rendered frame");
            Assert(prepare.Current is WaitForEndOfFrame,"Must wait until after drawing");
            Assert(Count(Get(warm,"preparing"))==4 && Count(Get(warm,"active"))==0,"Preparation must not emit damage");
            Assert(random.Equals(UnityEngine.Random.state),"Preparation consumed game RNG");
            ((IDisposable)prepare).Dispose();
            Assert(Count(Get(warm,"pool"))==4 && Count(Get(warm,"preparing"))==0,"Cancelled preparation did not return its objects");
            Assert(!(bool)Get(warm,"visualsPrepared"),"Cancelled preparation was incorrectly marked ready");
            prepare=warm.PrepareForBattle();Assert(prepare.MoveNext(),"Retry must render again");
            Assert(!prepare.MoveNext(),"Preparation should complete after drawing");
            Assert((bool)Get(warm,"visualsPrepared") && Count(Get(warm,"pool"))==4,"Completed preparation must retain four reusable popups");
            Assert(!warm.PrepareForBattle().MoveNext(),"Prepared UI should skip further warmup");
            Assert(root.GetComponentsInChildren<CanvasGroup>(true).All(g=>!g.gameObject.activeSelf || g.alpha==0),"Preparation leaked a visible popup");
            results.AppendLine("PASS: real active graphics, 4 pooled popups, no active damage entries, cancellation/retry/idempotence, RNG preserved.");

            var target=New(preview,"Popup recipient",true).transform;
            var emit=typeof(DamagePopupUI).GetMethod("Emit",Private);
            var visuals=new StringBuilder();
            for(int style=0;style<12;style++)
            {
                warm.Clear();fresh.Clear();
                for(int overlap=0;overlap<4;overlap++)
                {
                    UnityEngine.Random.InitState(1400+style*4+overlap);var seed=UnityEngine.Random.state;
                    emit.Invoke(warm,new object[]{target,42,style,overlap*.18f});
                    UnityEngine.Random.state=seed;
                    emit.Invoke(fresh,new object[]{target,42,style,overlap*.18f});
                    string a=Snapshot(warm),b=Snapshot(fresh);
                    Assert(a==b,$"Visible output differs for style {style}, overlap {overlap}");
                    visuals.AppendLine($"STYLE={style}; OVERLAP={overlap}");visuals.Append(a);
                }
            }
            results.AppendLine("PASS: 48 fresh/prepared comparisons; text, font keywords, text/effect vertices, color, scale, popup offsets and delay match.");
            File.WriteAllText(Folder+"visuals.txt",visuals.ToString());

            // Drive the actual transition iterator without Play mode, completing each fade explicitly.
            warm.Clear();Set(warm,"visualsPrepared",false);
            var cover=New(preview,"Opaque transition test",false);cover.AddComponent<RectTransform>();
            var coverCanvas=cover.AddComponent<Canvas>();coverCanvas.sortingOrder=100;
            var image=cover.AddComponent<Image>();image.color=new Color(.008f,.014f,.022f,1);
            var group=cover.AddComponent<CanvasGroup>();var transition=cover.AddComponent<ScreenTransitionUI>();
            Set(transition,"canvasGroup",group);
            cover.SetActive(true);group.alpha=0;
            int shown=0,started=0;
            Action show=()=>{Assert(group.alpha==1 && image.color.a==1,"Preparation cover must be opaque");shown++;};
            Action start=()=>{Assert((bool)Get(warm,"visualsPrepared"),"Battle began before preparation");started++;};
            Set(transition,"preparation",warm.PrepareForBattle());
            var routine=(IEnumerator)typeof(ScreenTransitionUI).GetMethod("PrepareAndPlay",Private).Invoke(transition,new object[]{show,start});
            Assert(routine.MoveNext(),"Fade-out should yield");
            ((Sequence)Get(transition,"sequence")).Complete();
            Assert(routine.MoveNext() && routine.Current is WaitForEndOfFrame,"Must hold opacity through warmup drawing");
            Assert(shown==1 && started==0 && group.alpha==1,"Preparation did not hold battle start");
            Assert(coverCanvas.sortingOrder>root.GetComponentInParent<Canvas>().sortingOrder,"Warmup rendered above the cover");
            Assert(routine.MoveNext() && started==1,"Battle must start once after preparation");
            ((Sequence)Get(transition,"sequence")).Complete();
            Assert(!routine.MoveNext() && !group.blocksRaycasts && !cover.activeSelf,"Completed transition retained its cover/input lock");
            results.AppendLine("PASS: opaque cover above actual popup canvas, preparation precedes battle start, start invoked exactly once, fade/input cleanup.");
            Set(warm,"visualsPrepared",false);cover.SetActive(true);group.alpha=0;group.blocksRaycasts=true;
            Set(transition,"preparation",warm.PrepareForBattle());
            routine=(IEnumerator)typeof(ScreenTransitionUI).GetMethod("PrepareAndPlay",Private).Invoke(transition,new object[]{show,start});
            Assert(routine.MoveNext(),"Cancelled transition fade should yield");
            ((Sequence)Get(transition,"sequence")).Complete();
            Assert(routine.MoveNext() && routine.Current is WaitForEndOfFrame,"Cancellation test did not reach preparation");
            ((IDisposable)routine).Dispose();
            Assert(Count(Get(warm,"pool"))==4 && Count(Get(warm,"preparing"))==0 && !cover.activeSelf && !group.blocksRaycasts && started==1,"Cancelling a covered preparation leaked objects, input lock or battle start");
            results.AppendLine("PASS: interrupted transition returns preparing objects, removes input lock/cover and does not start battle.");
        }
        finally
        {
            var materials=new HashSet<Material>(preview.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMP_Text>(true)).Select(t=>t.fontSharedMaterial).Where(m=>m!=null && !AssetDatabase.Contains(m)));
            foreach(var ui in preview.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DamagePopupUI>(true)))
                foreach(string name in new[]{"active","pool","preparing"})Get(ui,name).GetType().GetMethod("Clear").Invoke(Get(ui,name),null);
            EditorSceneManager.ClosePreviewScene(preview);
            foreach(var material in materials)if(material!=null)Object.DestroyImmediate(material);
            UnityEngine.Random.state=random;
        }
        Assert(SceneManager.GetActiveScene()==scene && scene.isDirty==dirty,"Working scene changed");
        results.AppendLine($"Working scene unchanged, dirty={dirty}. No Play mode or game build. Browser improvement remains unmeasured without a new build.");
        File.WriteAllText(Folder+"result.txt",results.ToString());
    }
}
