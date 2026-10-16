using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Explicit prefab migration and isolated preview; never edits the open scene.</summary>
[InitializeOnLoad]
public static class CardDesignAuthoring
{
    const string Request="Temp/CardDesign.request";
    const string Report="StageReview/card-design-verification.txt";
    static readonly string[] Prefabs={"Assets/Prefab/CardUI.prefab","Assets/Prefab/BattleHudCard.prefab"};
    static CardDesignAuthoring() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string command=File.ReadAllText(Request).Trim();File.Delete(Request);
        try { if(command=="build")Build();else if(command=="verify")Verify();else if(command=="text-preview")PreviewScreenText();else throw new ArgumentException(command); }
        catch(Exception e) {File.AppendAllText(Report,"FAIL: "+e+"\n");Debug.LogException(e);}
    }
    public static void PreviewScreenText()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("Screen text preview", typeof(RectTransform), typeof(Canvas), typeof(BattleProgressionUI));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(1200f, 800f);
            var cameraObject = new GameObject("Preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>();
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position = new Vector3(0f, 0f, -1000f);
            camera.orthographic = true; camera.orthographicSize = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.04f, 0.055f);
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 2000f;
            canvas.worldCamera = camera;
            var factory = typeof(BattleProgressionUI).GetMethod("CreateText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var owner = root.GetComponent<BattleProgressionUI>();
            string[] samples = { "報酬を選択", "獲得するカードを選択", "入れ替えるカードを選択", "あと <color=#F1CE83><b>3</b></color> 枚獲得できます" };
            float[] sizes = { 52f, 44f, 42f, 30f };
            for (int i = 0; i < samples.Length; i++)
            {
                var text = (TextMeshProUGUI)factory.Invoke(owner, new object[] { root.transform, samples[i], sizes[i],
                    new Vector2(0.5f, 0.82f - i * 0.21f), new Vector2(i == 3 ? 320f : 900f, 90f) });
                if (i == 3) { text.enableWordWrapping = false; text.enableAutoSizing = true; text.fontSizeMin = 20f; text.fontSizeMax = 30f; }
                text.ForceMeshUpdate();
                Require(!text.isTextOverflowing, "Screen text overflow: " + samples[i]);
            }
            Canvas.ForceUpdateCanvases();
            RuinsStageBuilder.Capture(camera, "StageReview/screen-text-preview.png", 1200, 800);
            File.AppendAllText(Report, "PASS screen text preview: StageReview/screen-text-preview.png\n");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [MenuItem("Tools/Cards/Apply reference design")]
    public static void Build()
    {
        Directory.CreateDirectory("StageReview");File.WriteAllText(Report,"Card design migration\n");
        foreach(string path in Prefabs)
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                ApplyToCard(root.GetComponent<CardUIView>());
                PrefabUtility.SaveAsPrefabAsset(root,path);
                File.AppendAllText(Report,"PASS prefab: "+path+"\n");
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        Verify();
    }
    public static void ApplyToCard(CardUIView card)
    {
        card.ApplyDesign();
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Font/NotoSansJP-Medium SDF.asset");
        const string path="Assets/Font/CardDesignText.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)
        {
            material=new Material(font.material){name="CardDesignText"};
            material.SetFloat(ShaderUtilities.ID_OutlineWidth,0);
            material.SetFloat(ShaderUtilities.ID_FaceDilate,0);
            material.DisableKeyword("UNDERLAY_ON");material.DisableKeyword("UNDERLAY_INNER");
            AssetDatabase.CreateAsset(material,path);
        }
        foreach(var text in new[]{card.cardNameText,card.costText,card.descriptionText})
        {
            text.font=font;text.fontSharedMaterial=material;
        }
    }
    [MenuItem("Tools/Cards/Verify reference design")]
    public static void Verify()
    {
        Directory.CreateDirectory("StageReview");
        var preview=EditorSceneManager.NewPreviewScene();
        var data=ScriptableObject.CreateInstance<CardData>();
        try
        {
            var root=new GameObject("Card design preview",typeof(RectTransform),typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root,preview);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;
            root.GetComponent<RectTransform>().sizeDelta=new Vector2(1100,760);
            var cameraObject=new GameObject("Preview camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,preview);
            var camera=cameraObject.GetComponent<Camera>();camera.scene=preview;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(preview);camera.transform.position=new Vector3(0,0,-1000);
            camera.orthographic=true;camera.orthographicSize=380;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.06f,.085f);
            camera.nearClipPlane=.1f;camera.farClipPlane=2000;canvas.worldCamera=camera;
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[0]);
            data.cardName="連続斬り";data.description="敵単体に\n2回ダメージを与える。";
            data.lowPerformance=data.mediumPerformance=data.highPerformance=new CardPerformance{cost=2};
            for(int i=0;i<4;i++)
            {
                data.rarity=(CardRarity)i;
                var card=Make(prefab,root.transform,new Vector2(-420+i*280,175),new Vector2(200,280),data);
                Label(root.transform,card.cardNameText.font,new[]{"コモン","アンコモン","レア（ホバー色）","スーパーレア"}[i],new Vector2(-420+i*280,345));
                if(i==2) {card.GetComponent<CardDesignLayout>().Surface.highlight=1;card.GetComponent<CardDesignLayout>().Surface.SetVerticesDirty();}
            }
            var assets=AssetDatabase.FindAssets("t:CardData",new[]{"Assets/ScriptableObject/CardData"});
            int count=0;
            foreach(string path in Prefabs)
            {
                var template=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach(string guid in assets)
                {
                    var source=AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                    var card=Make(template,root.transform,new Vector2(0,-180),new Vector2(176,246.4f),source);
                    card.cardNameText.ForceMeshUpdate();card.descriptionText.ForceMeshUpdate();
                    Require(!card.cardNameText.isTextOverflowing,"Name overflow: "+source.name);
                    Require(!card.descriptionText.isTextOverflowing,"Description overflow: "+source.name);
                    Object.DestroyImmediate(card.gameObject);count++;
                }
            }
            File.AppendAllText(Report,"PASS all card texts at hand size, both prefabs: "+count+"\n");
            for(int i=0;i<5;i++)
            {
                var source=AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(assets[i%assets.Length]));
                Make(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[i%2]),root.transform,new Vector2(-420+i*210,-180),new Vector2(176,246.4f),source);
            }
            Canvas.ForceUpdateCanvases();
            RuinsStageBuilder.Capture(camera,"StageReview/card-design-preview.png",1100,760);
            File.AppendAllText(Report,"PASS preview: StageReview/card-design-preview.png\n");
        }
        finally {Object.DestroyImmediate(data);EditorSceneManager.ClosePreviewScene(preview);}
    }
    static CardUIView Make(GameObject prefab,Transform parent,Vector2 position,Vector2 size,CardData data)
    {
        var root=Object.Instantiate(prefab,parent);var rect=root.GetComponent<RectTransform>();
        rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);
        rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;
        var view=root.GetComponent<CardUIView>();var card=new CardInstance(data);card.RollPerformance();view.Setup(card);
        view.ApplyDesign();view.ApplyDesign();
        Require(root.GetComponentsInChildren<CardDesignGraphic>(true).Length==1,"Repeated apply creates duplicate chrome");
        Require(view.cardImage.sprite==data.cardImage && view.cardImage.color.a==(data.cardImage!=null?1:0),"Illustration binding mismatch");
        Require(view.GetComponent<CardDesignLayout>().Surface.rarity==data.rarity,"Rarity mismatch");
        Require(view.cardNameText.text==data.cardName && view.descriptionText.text==card.GetDescription(),"Data binding mismatch");
        Require(root.GetComponentsInChildren<CardResponsiveLayout>(true).All(x=>!x.enabled),"Legacy layout conflict");
        // Detail overlay clones a configured card. Its serialized references must stay local.
        var clone=Object.Instantiate(root,parent);clone.GetComponent<CardUIView>().Setup(card);
        Require(clone.GetComponent<CardUIView>().cardNameText.transform.IsChildOf(clone.transform),"Cloned card references source text");
        Require(clone.GetComponent<CardUIView>().cardImage.sprite==data.cardImage,"Cloned illustration binding mismatch");
        Object.DestroyImmediate(clone);
        return view;
    }
    static void Label(Transform parent,TMP_FontAsset font,string value,Vector2 position)
    {
        var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);
        var t=go.GetComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=18;t.alignment=TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta=new Vector2(220,36);t.rectTransform.anchoredPosition=position;
    }
    static void Require(bool ok,string message) {if(!ok)throw new InvalidOperationException(message);}
}
