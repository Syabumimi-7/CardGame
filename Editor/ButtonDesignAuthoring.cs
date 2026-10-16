using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ButtonDesignAuthoring
{
    const string Request="Temp/ButtonDesign.request";
    const string Report="StageReview/button-design-inspect.txt";
    static ButtonDesignAuthoring() {EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string command=File.ReadAllText(Request).Trim();File.Delete(Request);
        try {if(command=="inspect")Inspect();else if(command=="build")Build();else if(command=="preview")Preview();else if(command=="verify")ButtonDesignVerification.Begin();else throw new ArgumentException(command);}
        catch(Exception e){File.WriteAllText(Report,e.ToString());Debug.LogException(e);}
    }
    public static void Inspect()
    {
        var report=new StringBuilder();var scene=SceneManager.GetActiveScene();
        report.AppendLine(scene.path+" dirty="+scene.isDirty+" playing="+EditorApplication.isPlaying);
        foreach(var button in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Button>(true)))
        {
            string path=button.name;for(var p=button.transform.parent;p!=null;p=p.parent)path=p.name+"/"+path;
            report.AppendLine(path+" | "+((RectTransform)button.transform).rect.size+" | "+
                string.Join(";",button.GetComponentsInChildren<TMP_Text>(true).Select(x=>x.text))+" | "+
                button.targetGraphic?.GetType().Name+" | active="+button.gameObject.activeSelf+" | "+
                string.Join(",",Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Select(i=>button.onClick.GetPersistentMethodName(i))));
        }
        File.WriteAllText(Report,report.ToString());
    }
    [MenuItem("Tools/Buttons/Apply reference design")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        if(!AssetDatabase.IsValidFolder("Assets/Resources"))AssetDatabase.CreateFolder("Assets","Resources");
        const string themePath="Assets/Resources/ButtonDesignTheme.asset";
        var theme=AssetDatabase.LoadAssetAtPath<ButtonDesignTheme>(themePath);
        if(theme==null){theme=ScriptableObject.CreateInstance<ButtonDesignTheme>();AssetDatabase.CreateAsset(theme,themePath);}
        theme.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NotoSansJP-Medium SDF.asset");
        const string materialPath="Assets/Font/ButtonDesignText.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null)
        {
            material=new Material(theme.font.material){name="ButtonDesignText"};
            material.SetFloat(ShaderUtilities.ID_OutlineWidth,0);
            material.SetFloat(ShaderUtilities.ID_FaceDilate,0);
            material.DisableKeyword("UNDERLAY_ON");material.DisableKeyword("UNDERLAY_INNER");
            AssetDatabase.CreateAsset(material,materialPath);
        }
        theme.textMaterial=material;EditorUtility.SetDirty(theme);AssetDatabase.SaveAssetIfDirty(theme);
        var report=new StringBuilder("Button design migration\n");
        foreach(string path in AssetDatabase.FindAssets("t:Scene",new[]{"Assets/Scenes"}).Select(AssetDatabase.GUIDToAssetPath).Concat(new[]{"Assets/TitleScene.unity"}))
        {
            var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
            if(!opened && scene.isDirty){report.AppendLine("Deferred dirty scene (runtime styling remains active): "+path);continue;}
            if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            try
            {
                foreach(var pile in scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<CardPileViewer>(true)))pile.ConfigureButtonArea();
                var buttons=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Button>(true)).ToArray();
                int changed=0;
                foreach(var button in buttons)
                {
                    if(!ButtonDesign.ShouldStyle(button))continue;
                    int listeners=button.onClick.GetPersistentEventCount();
                    ButtonDesign.Apply(button);
                    if(button.onClick.GetPersistentEventCount()!=listeners)throw new InvalidOperationException("Changed click listeners: "+button.name);
                    changed++;
                }
                if(changed>0){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
                report.AppendLine("PASS "+path+": "+changed+" styled, "+(buttons.Length-changed)+" excluded; click listeners preserved");
            }
            finally {if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        File.WriteAllText("StageReview/button-design-verification.txt",report.ToString());
        Preview();Inspect();
    }
    [MenuItem("Tools/Buttons/Preview reference design")]
    public static void Preview()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Button preview",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(root,scene);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;root.GetComponent<RectTransform>().sizeDelta=new Vector2(1200,650);
            var cameraObject=new GameObject("Preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.orthographic=true;camera.orthographicSize=325;camera.transform.position=new Vector3(0,0,-1000);
            camera.nearClipPlane=.1f;camera.farClipPlane=2000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.065f,.075f);canvas.worldCamera=camera;
            for(int i=0;i<4;i++)
            {
                var button=Sample(root.transform,"次のバトルへ",new Vector2(-435+i*290,180),new Vector2(250,76));
                var surface=button.GetComponent<ButtonDesign>().Surface;
                surface.hover=i==1?1:0;surface.pressed=i==2?1:0;
                if(i==3){button.interactable=false;ButtonDesign.Apply(button);}
                surface.SetVerticesDirty();
                Label(root.transform,new[]{"通常","ホバー","押下","無効"}[i],new Vector2(-435+i*290,250));
            }
            Sample(root.transform,"はじめる",new Vector2(-375,15),new Vector2(280,80));
            Sample(root.transform,"再開",new Vector2(0,15),new Vector2(200,60));
            Sample(root.transform,"もどる",new Vector2(375,15),new Vector2(160,48));
            Sample(root.transform,"新たなカードを獲得",new Vector2(-300,-140),new Vector2(420,78));
            Sample(root.transform,"削除せずに次のバトルに進む",new Vector2(300,-140),new Vector2(460,64));
            Canvas.ForceUpdateCanvases();
            foreach(var t in root.GetComponentsInChildren<TMP_Text>()){t.ForceMeshUpdate();if(t.isTextOverflowing)throw new InvalidOperationException("Preview text overflow: "+t.text);}
            RuinsStageBuilder.Capture(camera,"StageReview/button-design-preview.png",1200,650);
        }
        finally {EditorSceneManager.ClosePreviewScene(scene);}
    }
    static Button Sample(Transform parent,string text,Vector2 position,Vector2 size)
    {
        var go=new GameObject(text,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.sizeDelta=size;rect.anchoredPosition=position;
        var label=Label(go.transform,text,Vector2.zero);label.fontSize=28;
        var button=go.GetComponent<Button>();ButtonDesign.Apply(button);return button;
    }
    static TMP_Text Label(Transform parent,string value,Vector2 position)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var text=go.GetComponent<TextMeshProUGUI>();var theme=ButtonDesignTheme.Current;
        text.font=theme.font;text.fontSharedMaterial=theme.textMaterial;text.fontSize=22;text.text=value;text.alignment=TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta=new Vector2(270,36);text.rectTransform.anchoredPosition=position;return text;
    }
}
