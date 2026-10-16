using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static partial class BattleHudAuthoring
{
    static TMP_FontAsset font;
    static Material textMaterial;
    static readonly Color Ink = new Color(.025f,.035f,.045f,.94f);
    static readonly Color Gold = new Color(.79f,.62f,.32f);
    static readonly Color Blue = new Color(.08f,.52f,1,.75f);
    static partial void Dispatch(string command)
    {
        if (command == "build") Build();
        else if (command == "repair-renderers") RepairRenderers();
        else if (command == "polish") Polish();
        else if (command == "verify") BattleHudVerification.Begin();
        else if (command == "center-hand") CenterHand();
        else if (command == "verify-title") BattleHudVerification.BeginTitle();
        else if (command == "preview-packs") BattleHudVerification.BeginPackPreview();
        else if (command == "verify-collection") BattleHudVerification.BeginCollection();
        else throw new InvalidOperationException(command);
    }
    [MenuItem("Tools/Battle UI/Center Hand and Arrange Controls")]
    static void CenterHand()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty)
            throw new InvalidOperationException("Saved refined scene in Edit mode required.");
        var hud = Object.FindObjectOfType<BattleHudUI>(true);
        Undo.RegisterFullObjectHierarchyUndo(hud.gameObject, "Center battle hand");
        PositionHudControl(hud.battle.handUI.handParent, 448f, 698f);
        hud.battle.handUI.handParent.sizeDelta = new Vector2(1024f, 350f);
        Undo.RecordObject(hud.battle.handUI, "Enlarge hand cards");
        hud.battle.handUI.baseCardSize = new Vector2(240f, 350f);
        hud.battle.handUI.selectedLift = 72f;
        EditorUtility.SetDirty(hud.battle.handUI);
        PositionHudControl((RectTransform)hud.transform.Find("Cost"), 38f, 690f);
        PositionHudControl((RectTransform)hud.battle.endTurnButton.transform, 1550f, 677f);
        PositionHudControl(hud.battle.handUI.warningText.rectTransform, 580f, 530f);
        Undo.FlushUndoRecordObjects();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Report, "CENTER HAND PASS: hand center x=960; cost above draw pile; end turn above graveyard.");
    }
    static void PositionHudControl(RectTransform rect, float x, float y)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
    }
    static Material TextMaterial(TMP_FontAsset source)
    {
        const string path="Assets/Font/BattleHudText.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null)
        {
            material=new Material(source.material){name="BattleHudText"};
            material.SetFloat(ShaderUtilities.ID_OutlineWidth,.035f);
            material.SetFloat(ShaderUtilities.ID_FaceDilate,0);
            AssetDatabase.CreateAsset(material,path);
        }
        return material;
    }
    static void Polish()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=ScenePath || scene.isDirty)throw new InvalidOperationException("Saved refined scene required.");
        var hud=Object.FindObjectOfType<BattleHudUI>(true);
        hud.pausePanel.GetComponent<Canvas>().sortingOrder=90;
        var material=TextMaterial(hud.battle.turnText.font);
        foreach(var text in hud.GetComponentsInChildren<TextMeshProUGUI>(true))text.fontSharedMaterial=material;
        foreach(var text in hud.pausePanel.GetComponentsInChildren<TextMeshProUGUI>(true))text.fontSharedMaterial=material;
        hud.playerHealth.showHPLabel=true;hud.enemyHealth.showHPLabel=true;
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefab/BattleHudCard.prefab");
        foreach(var text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))text.fontSharedMaterial=material;
        var nameText=prefab.GetComponent<CardUIView>().cardNameText;
        nameText.fontSizeMin=10;nameText.margin=new Vector4(5,0,5,0);
        PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefab/BattleHudCard.prefab");PrefabUtility.UnloadPrefabContents(prefab);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(Report,"POLISH PASS");
    }
    static void RepairRenderers()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode || scene.path!=ScenePath || scene.isDirty)throw new InvalidOperationException("Saved refined scene required.");
        foreach(var graphic in Object.FindObjectsOfType<BattleHudGraphic>(true))
            if(graphic.GetComponent<CanvasRenderer>()==null)graphic.gameObject.AddComponent<CanvasRenderer>();
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefab/BattleHudCard.prefab");
        foreach(var graphic in prefab.GetComponentsInChildren<BattleHudGraphic>(true))
            if(graphic.GetComponent<CanvasRenderer>()==null)graphic.gameObject.AddComponent<CanvasRenderer>();
        var rect=prefab.GetComponent<RectTransform>();rect.pivot=new Vector2(.5f,.5f);
        PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefab/BattleHudCard.prefab");PrefabUtility.UnloadPrefabContents(prefab);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText(Report,"REPAIR PASS: all graphics have CanvasRenderer.");
    }
    [MenuItem("Tools/Battle UI/Apply Design to Refined Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath || scene.isDirty) throw new InvalidOperationException("Open the saved RuinsStageRefined scene first.");
        var game = Object.FindObjectOfType<GameManager>(true);
        var battle = game.battleManager;
        var parent = game.battleUIRoot.transform;
        if (parent.Find("DesignedBattleHUD") != null) throw new InvalidOperationException("HUD already exists; edit the authored scene objects.");
        font = battle.turnText.font;
        textMaterial=TextMaterial(font);
        var scaler = parent.GetComponentInParent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        Undo.RegisterFullObjectHierarchyUndo(game.battleUIRoot,"Apply battle HUD design");
        parent.Find("BattleUI").gameObject.SetActive(false);
        parent.Find("SentakukaijoButton").gameObject.SetActive(false);
        var root = Rect("DesignedBattleHUD",parent,0,0,1920,1080);
        root.anchorMin=root.anchorMax=root.pivot=new Vector2(.5f,.5f);root.anchoredPosition=Vector2.zero;
        root.gameObject.AddComponent<BattleHudFit>();
        root.SetAsFirstSibling();
        var hud = root.gameObject.AddComponent<BattleHudUI>(); hud.game=game;hud.battle=battle;
        // A transparent, full-screen click surface preserves the existing deselect gesture.
        var empty=Rect("Deselect",root,0,0,1920,1080);Stretch(empty);
        var hit=empty.gameObject.AddComponent<Image>();hit.color=Color.clear;
        var deselect=empty.gameObject.AddComponent<Button>();deselect.transition=Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(deselect.onClick,battle.handUI.OnEmptyAreaClicked);

        var count=Frame("BattleCount",root,38,30,300,68,Ink);
        hud.battleCount=Text("Count",count,"1 / 10 連戦目",28,12,8,276,52);
        hud.playerHealth=Health(root,battle,false,out var playerRows);
        hud.enemyHealth=Health(root,battle,true,out var enemyRows);
        hud.playerStatusRows=playerRows;hud.enemyStatusRows=enemyRows;
        hud.enemyName=hud.enemyHealth.transform.Find("Name").GetComponent<TextMeshProUGUI>();

        var banner=Frame("SituationBanner",root,620,65,680,112,new Color(.015f,.07f,.15f,.96f),Blue);
        hud.bannerFrame=banner.GetComponent<BattleHudGraphic>();hud.banner=banner.gameObject.AddComponent<CanvasGroup>();
        hud.banner.alpha=0;hud.banner.blocksRaycasts=false;
        battle.turnText=Text("Situation",banner,"",40,25,16,630,80);
        battle.turnBannerDuration=2.2f;
        var intent=Frame("EnemyIntent",root,1400,470,482,156,new Color(.12f,.018f,.024f,.95f),new Color(1,.1f,.1f,.65f));
        intent.GetComponent<BattleHudGraphic>().metal=new Color(.88f,.27f,.23f);
        Text("Heading",intent,"敵の次の行動",17,20,10,442,25).color=new Color(1,.63f,.49f);
        battle.intentText=Text("Intent",intent,"",22,20,40,442,104);

        var card = BuildCard();
        var hand=Rect("HandArea",root,448,698,1024,350);
        var layout=hand.gameObject.AddComponent<HorizontalLayoutGroup>();layout.childAlignment=TextAnchor.MiddleCenter;
        layout.childControlWidth=false;layout.childControlHeight=false;layout.childForceExpandWidth=false;layout.childForceExpandHeight=false;layout.spacing=14;
        battle.handUI.handParent=hand;battle.handUI.cardPrefab=card;battle.handUI.baseCardSize=new Vector2(240,350);battle.handUI.cardSpacing=14;
        battle.handUI.selectedScale=1.16f;battle.handUI.selectedLift=72;
        battle.handUI.warningText=Text("Warning",root,"",27,580,530,760,58);
        battle.handUI.warningText.color=new Color(1,.7f,.37f);
        var piles=parent.GetComponentsInChildren<CardPileViewer>(true);
        foreach(var pile in piles)
        {
            bool grave=pile.pileType==CardPileViewer.PileType.DiscardPile;
            var button=MakeButton(grave?"Graveyard":"DrawPile",root,"",grave?1740:42,808,138,232,Ink);
            button.GetComponent<BattleHudGraphic>().shape=BattleHudGraphic.Shape.CardBack;
            var label=Text("Label",button.transform,grave?"墓地":"山札",23,8,192,122,32);
            label.color=new Color(.94f,.86f,.67f);
            var plate=Frame("CountPlate",button.transform,42,146,54,42,Ink);plate.GetComponent<BattleHudGraphic>().ornament=false;
            var countText=Text("Count",plate,"0",26,0,0,54,42);
            UnityEventTools.AddPersistentListener(button.onClick,pile.Open);
            if(grave){battle.handUI.graveyardIcon=(RectTransform)button.transform;battle.discardCountText=countText;}
            else {battle.handUI.deckIcon=(RectTransform)button.transform;battle.deckCountText=countText;}
            pile.cardPrefab=card;
        }
        var cost=Frame("Cost",root,38,690,332,86,Ink);
        Text("Label",cost,"残りコスト",16,18,6,130,24).alignment=TextAlignmentOptions.Left;
        hud.costGems=Rect("Gems",cost,15,34,168,40);
        var gemsLayout=hud.costGems.gameObject.AddComponent<HorizontalLayoutGroup>();gemsLayout.spacing=3;
        gemsLayout.childControlWidth=true;gemsLayout.childControlHeight=true;
        battle.costText=Text("CostValue",cost,"3 / 3",33,187,14,130,58);
        var end=MakeButton("EndTurn",root,"ターン終了",1550,677,332,112,new Color(.02f,.105f,.2f,.98f),Blue);
        battle.endTurnButton=end;battle.handUI.endTurnButton=end;
        hud.pauseButton=MakeButton("PauseButton",root,"",1810,30,72,68,Ink);
        var gear=Rect("Gear",hud.pauseButton.transform,10,8,52,52).gameObject.AddComponent<BattleHudGraphic>();
        gear.shape=BattleHudGraphic.Shape.Gear;gear.color=new Color(.9f,.87f,.76f);gear.raycastTarget=false;

        // Templates remain inactive and outside the gameplay hand.
        var templates=Rect("Templates",root,0,0,0,0);templates.gameObject.SetActive(false);
        var rowBg=Frame("StatusRow",templates,0,0,308,38,new Color(.02f,.026f,.035f,.95f));
        var rowSize=rowBg.gameObject.AddComponent<LayoutElement>();rowSize.preferredHeight=38;rowSize.minHeight=38;
        rowBg.GetComponent<BattleHudGraphic>().metal=new Color(.27f,.29f,.29f);rowBg.GetComponent<BattleHudGraphic>().ornament=false;
        var row=Text("StatusText",rowBg,"",19,0,0,308,38);Stretch(row.rectTransform);row.alignment=TextAlignmentOptions.Left;row.margin=new Vector4(8,0,8,0);
        hud.statusRowTemplate=row;
        var gem=Rect("GemTemplate",templates,0,0,42,42).gameObject.AddComponent<BattleHudGraphic>();gem.shape=BattleHudGraphic.Shape.Gem;gem.raycastTarget=false;
        hud.gemTemplate=gem;

        var pause=Rect("PauseOverlay",parent,0,0,1920,1080);Stretch(pause);
        var dim=pause.gameObject.AddComponent<Image>();dim.color=new Color(.004f,.008f,.015f,.84f);
        var pauseCanvas=pause.gameObject.AddComponent<Canvas>();pauseCanvas.overrideSorting=true;pauseCanvas.sortingOrder=90;
        pause.gameObject.AddComponent<GraphicRaycaster>();
        var menu=Frame("PauseMenu",pause,650,318,620,444,Ink,Blue);
        menu.anchorMin=menu.anchorMax=menu.pivot=new Vector2(.5f,.5f);menu.anchoredPosition=Vector2.zero;
        Text("Title",menu,"ポーズ",40,50,46,520,65);
        hud.resumeButton=MakeButton("Resume",menu,"再開",120,162,380,82,new Color(.025f,.12f,.23f,1),Blue);
        hud.titleButton=MakeButton("ReturnToTitle",menu,"タイトルへもどる",120,284,380,76,Ink);
        hud.pausePanel=pause.gameObject;pause.gameObject.SetActive(false);
        EditorUtility.SetDirty(battle);EditorUtility.SetDirty(battle.handUI);
        ButtonDesign.ApplyScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Report,"BUILD PASS: " + ScenePath);
    }

    static HealthBarUI Health(Transform root,BattleManager battle,bool enemy,out RectTransform rows)
    {
        var panel=Frame(enemy?"EnemyStatus":"PlayerStatus",root,enemy?1530:38,198,352,246,Ink);
        Text("Name",panel,enemy?"敵":GameManager.GetPackDisplayName(0),26,20,16,312,36).alignment=TextAlignmentOptions.Left;
        var barFrame=Frame("HPFrame",panel,20,60,312,34,Color.black);barFrame.GetComponent<BattleHudGraphic>().ornament=false;
        var back=Rect("HPBackground",barFrame,4,4,304,26);var bg=back.gameObject.AddComponent<Image>();bg.color=Color.black;bg.raycastTarget=false;
        var fill=Rect("HPFill",back,0,0,304,26).gameObject.AddComponent<Image>();fill.raycastTarget=false;
        var text=Text("HP",barFrame,"",20,8,0,296,34);text.alignment=TextAlignmentOptions.Left;
        var hp=panel.gameObject.AddComponent<HealthBarUI>();hp.source=enemy?HealthBarUI.HealthSource.CurrentEnemy:HealthBarUI.HealthSource.Player;
        hp.showHPLabel=true;
        hp.playerStatus=battle.playerStatus;hp.enemyManager=battle.enemyManager;hp.blackBackgroundImage=bg;hp.greenFillImage=fill;hp.hpText=text;
        hp.healthyColor=enemy?new Color(.78f,.09f,.12f):new Color(.08f,.65f,.25f);
        var status=Text("StatusSource",panel,"",19,20,102,310,128);status.gameObject.SetActive(false);hp.statusText=status;
        var scroll=Rect("StatusScroll",panel,18,105,316,127);var sr=scroll.gameObject.AddComponent<ScrollRect>();
        var viewport=Rect("Viewport",scroll,0,0,316,127);Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
        var scrollHit=viewport.gameObject.AddComponent<Image>();scrollHit.color=Color.clear;
        rows=Rect("Rows",viewport,0,0,316,127);rows.anchorMin=new Vector2(0,1);rows.anchorMax=Vector2.one;rows.sizeDelta=new Vector2(0,127);
        var vl=rows.gameObject.AddComponent<VerticalLayoutGroup>();vl.spacing=5;vl.childControlWidth=true;vl.childControlHeight=true;vl.childForceExpandHeight=false;
        var fit=rows.gameObject.AddComponent<ContentSizeFitter>();fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport=viewport;sr.content=rows;sr.horizontal=false;sr.scrollSensitivity=28;sr.movementType=ScrollRect.MovementType.Clamped;
        return hp;
    }

    static CardUIView BuildCard()
    {
        var root=Frame("BattleHudCard",null,0,0,192,280,new Color(.055f,.061f,.07f,1));
        root.pivot=new Vector2(.5f,.5f);
        root.GetComponent<BattleHudGraphic>().raycastTarget=true;
        var card=root.gameObject.AddComponent<CardUIView>();
        var art=Frame("EmptyIllustration",root,9,10,174,147,new Color(.024f,.03f,.04f,1));
        art.GetComponent<BattleHudGraphic>().ornament=false;Normalized(art,.05f,.045f,.9f,.50f);
        var imageRect=Rect("Illustration",art,0,0,174,147);Stretch(imageRect);
        card.cardImage=imageRect.gameObject.AddComponent<Image>();card.cardImage.color=Color.clear;card.cardImage.raycastTarget=false;
        var namePlate=Frame("NamePlate",root,9,159,174,42,new Color(.77f,.72f,.59f,1));Normalized(namePlate,.05f,.565f,.9f,.15f);
        namePlate.GetComponent<BattleHudGraphic>().ornament=false;
        card.cardNameText=Text("Name",namePlate,"",23,4,0,166,42);Stretch(card.cardNameText.rectTransform);card.cardNameText.color=new Color(.09f,.08f,.06f);
        card.cardNameText.fontSizeMin=10;card.cardNameText.margin=new Vector4(5,0,5,0);
        card.descriptionText=Text("Effect",root,"",17,15,205,162,63);Normalized(card.descriptionText.rectTransform,.08f,.74f,.84f,.22f);
        card.descriptionText.color=new Color(.9f,.86f,.75f);
        var badge=Frame("CostBadge",root,0,0,49,49,new Color(.018f,.18f,.36f,1),Blue);Normalized(badge,-.02f,-.01f,.265f,.18f);
        badge.GetComponent<BattleHudGraphic>().ornament=false;
        card.costText=Text("Cost",badge,"?",32,0,0,49,49);Stretch(card.costText.rectTransform);
        root.gameObject.AddComponent<UIHoverScale>();
        CardDesignAuthoring.ApplyToCard(card);
        var saved=PrefabUtility.SaveAsPrefabAsset(root.gameObject,"Assets/Prefab/BattleHudCard.prefab");Object.DestroyImmediate(root.gameObject);
        return saved.GetComponent<CardUIView>();
    }

    static RectTransform Rect(string name,Transform parent,float x,float y,float w,float h)
    {
        var go=new GameObject(name,typeof(RectTransform));go.layer=5;
        var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
    }
    static void Stretch(RectTransform r) {r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    static void Normalized(RectTransform r,float x,float y,float w,float h) {r.anchorMin=new Vector2(x,1-y-h);r.anchorMax=new Vector2(x+w,1-y);r.offsetMin=r.offsetMax=Vector2.zero;}
    static RectTransform Frame(string name,Transform parent,float x,float y,float w,float h,Color color,Color glow=default)
    {
        var r=Rect(name,parent,x,y,w,h);var g=r.gameObject.AddComponent<BattleHudGraphic>();g.color=color;g.metal=Gold;g.glow=glow;g.raycastTarget=false;return r;
    }
    static TextMeshProUGUI Text(string name,Transform parent,string value,float size,float x,float y,float w,float h)
    {
        var t=Rect(name,parent,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=value;t.fontSize=size;
        if(textMaterial!=null)t.fontSharedMaterial=textMaterial;
        t.enableAutoSizing=true;t.fontSizeMin=size*.65f;t.fontSizeMax=size;t.color=new Color(.97f,.95f,.9f);t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
        return t;
    }
    static Button MakeButton(string name,Transform parent,string label,float x,float y,float w,float h,Color color,Color glow=default)
    {
        var r=Frame(name,parent,x,y,w,h,color,glow);var g=r.GetComponent<BattleHudGraphic>();g.raycastTarget=true;
        var b=r.gameObject.AddComponent<Button>();b.targetGraphic=g;
        var colors=b.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.3f,1.3f,1.3f);colors.pressedColor=new Color(.65f,.7f,.8f);colors.disabledColor=new Color(.4f,.4f,.4f,.75f);b.colors=colors;
        var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
        if(!string.IsNullOrEmpty(label))Text("Label",r,label,32,14,8,w-28,h-16);
        return b;
    }
}
