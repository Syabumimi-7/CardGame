using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Register the photographed illustrations, then verify the real UI prefabs.</summary>
[InitializeOnLoad]
public static class CardIllustrationBinding
{
    const string Request="Temp/CardIllustrations.request";
    const string Report="StageReview/CardStudio/card-illustration-binding.txt";
    static readonly string[] Prefabs={"Assets/Prefab/CardUI.prefab","Assets/Prefab/BattleHudCard.prefab"};
    static CardIllustrationBinding(){EditorApplication.update+=Poll;}
    static void Poll()
    {
        if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
        string command=File.ReadAllText(Request).Trim();File.Delete(Request);
        try
        {
            if(command=="bind")Bind();else if(command=="verify")Verify();else throw new ArgumentException(command);
            File.WriteAllText("Temp/CardIllustrations.result","OK "+command);
        }
        catch(Exception e){File.WriteAllText("Temp/CardIllustrations.result",e.ToString());Debug.LogException(e);}
    }
    static CardData[] Cards()=>AssetDatabase.FindAssets("t:CardData",new[]{"Assets/ScriptableObject/CardData"})
        .Select(g=>AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(g))).OrderBy(c=>c.name).ToArray();
    static string ImagePath(CardData card)=>"Assets/CardIllustrations/Output/"+
        (card.name=="Kandenkougeki" ? "kanndennkougeki" : card.name.ToLowerInvariant())+".png";
    static void Require(bool valid,string message){if(!valid)throw new InvalidOperationException(message);}
    [MenuItem("Tools/Cards/撮影した70枚のイラストを登録")]
    public static void Bind()
    {
        var cards=Cards();Require(cards.Length==70,"カード定義数が70ではありません。");
        // Validate every mapping before changing the first asset.
        foreach(var card in cards)Require(File.Exists(ImagePath(card)),"対応画像がありません: "+card.cardName+" / "+ImagePath(card));
        foreach(var card in cards)
        {
            string path=ImagePath(card);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            Require(importer!=null,"TextureImporterがありません: "+path);
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.sRGBTexture=true;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);Require(sprite!=null,"Spriteの読み込み失敗: "+path);
            Undo.RecordObject(card,"カードイラスト登録");card.cardImage=sprite;EditorUtility.SetDirty(card);
        }
        AssetDatabase.SaveAssets();
        Verify();
    }
    [MenuItem("Tools/Cards/70枚のイラスト表示を検証")]
    public static void Verify()
    {
        Directory.CreateDirectory("StageReview/CardStudio");var report=new StringBuilder();
        var cards=Cards();Require(cards.Length==70,"カード数が不正です。");
        foreach(var card in cards)
        {
            Require(card.cardImage!=null && AssetDatabase.GetAssetPath(card.cardImage)==ImagePath(card),"カード画像の対応不一致: "+card.name);
            report.AppendLine("PASS asset: "+card.cardName+" -> "+ImagePath(card));
        }
        var randomState=UnityEngine.Random.state;
        var scene=EditorSceneManager.NewPreviewScene();
        try
        {
            var root=new GameObject("Card illustration binding preview",typeof(RectTransform),typeof(Canvas));
            SceneManager.MoveGameObjectToScene(root,scene);
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;root.GetComponent<RectTransform>().sizeDelta=new Vector2(1500,1200);
            var cameraObject=new GameObject("PreviewCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.GetComponent<Camera>();camera.enabled=false;camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            camera.transform.position=new Vector3(0,0,-1000);camera.orthographic=true;camera.orthographicSize=600;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.04f,.055f,.07f);camera.nearClipPlane=.1f;camera.farClipPlane=2000;canvas.worldCamera=camera;
            foreach(string path in Prefabs)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Require(prefab!=null,"カードPrefabがありません。");
                foreach(var data in cards)
                {
                    var go=Object.Instantiate(prefab,root.transform);
                    var view=go.GetComponent<CardUIView>();view.Setup(new CardInstance(data));
                    view.ApplyDesign();view.Refresh();view.ApplyDesign();
                    CheckView(view,data);
                    var other=cards.First(c=>c!=data);view.Setup(new CardInstance(other));CheckView(view,other);
                    view.Setup(new CardInstance(data));
                    var clone=Object.Instantiate(go,root.transform);var cloned=clone.GetComponent<CardUIView>();cloned.Setup(new CardInstance(data));CheckView(cloned,data);
                    Require(cloned.cardImage.transform.IsChildOf(clone.transform),"拡大表示の画像参照が元のカードを指しています。");
                    view.Setup(null);Require(view.cardImage.sprite==null && view.cardImage.color.a==0,"カードの再利用時に古い画像が残ります。");
                    Object.DestroyImmediate(clone);Object.DestroyImmediate(go);
                }
                report.AppendLine("PASS UI prefab: "+path+" / 70 cards / refresh / rebind / detail clone / null reset");
            }
            string[] examples={"Kougeki","Kandenkougeki","Rensakougeki","Shukketsukougeki","Dokunokenn","Bougyo","Kougekiryokuappu","Dorookyouka","Togeburokku","Kaifuku","Kanden","Jubaku"};
            for(int i=0;i<examples.Length;i++)
            {
                var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[i%2]),root.transform);
                var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);
                rect.anchoredPosition=new Vector2(-540+(i%4)*360,380-(i/4)*380);rect.sizeDelta=new Vector2(250,350);rect.localScale=Vector3.one;
                go.GetComponent<CardUIView>().Setup(new CardInstance(cards.Single(c=>c.name==examples[i])));
            }
            Canvas.ForceUpdateCanvases();
            RuinsStageBuilder.Capture(camera,"StageReview/CardStudio/cards-with-illustrations.png",1500,1200);
            report.AppendLine("PASS preview: StageReview/CardStudio/cards-with-illustrations.png");
        }
        catch(Exception e){report.AppendLine("FAIL: "+e);throw;}
        finally{UnityEngine.Random.state=randomState;EditorSceneManager.ClosePreviewScene(scene);File.WriteAllText(Report,report.ToString(),Encoding.UTF8);}
    }
    static void CheckView(CardUIView view,CardData data)
    {
        Require(view.cardImage.sprite==data.cardImage && view.cardImage.enabled && view.cardImage.color==Color.white,"UI画像表示不一致: "+data.name);
        Require(view.cardImage.transform.parent.GetComponent<RectMask2D>()!=null,"イラスト枠のマスクがありません。");
        Require(view.GetComponentsInChildren<RectMask2D>().Count(m=>m.name=="IllustrationViewport")==1,"イラスト枠が重複しました。");
        Require(Mathf.Abs(view.cardImage.rectTransform.sizeDelta.x/view.cardImage.rectTransform.sizeDelta.y-data.cardImage.rect.width/data.cardImage.rect.height)<.001f,"画像の縦横比が変形しました。");
    }
}
