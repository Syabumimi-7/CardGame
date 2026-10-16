using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

/// <summary>Exercise actual scene controls and runtime reward/detail factories.</summary>
[InitializeOnLoad]
public static class ButtonDesignVerification
{
    const string Key="ButtonDesignVerification",Report="StageReview/button-design-play-verification.txt";
    static Button start;
    static int clickCount,deckCount;
    static ButtonDesignVerification(){EditorApplication.update+=Update;Application.logMessageReceived+=Log;}
    public static void Begin()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(EditorApplication.isPlayingOrWillChangePlaymode||scene.isDirty||scene.path!=BattleHudAuthoring.ScenePath)
            throw new InvalidOperationException("Saved refined scene in Edit mode required.");
        File.WriteAllText(Report,"Button design Play verification\n");
        SessionState.SetInt(Key,1);SessionState.SetInt(Key+"Errors",0);SessionState.SetFloat(Key+"Due",0);
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();EditorApplication.EnterPlaymode();
    }
    static void Log(string message,string stack,LogType type)
    {
        if(SessionState.GetInt(Key,0)==0 || (type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert))return;
        SessionState.SetInt(Key+"Errors",SessionState.GetInt(Key+"Errors",0)+1);File.AppendAllText(Report,"ERROR: "+message+"\n");
    }
    static void Update()
    {
        int step=SessionState.GetInt(Key,0);if(step==0||!EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();if(Time.realtimeSinceStartup<SessionState.GetFloat(Key+"Due",0))return;
        try
        {
            var game=Object.FindObjectOfType<GameManager>();var battle=game.battleManager;
            var hud=game.battleUIRoot.GetComponentInChildren<BattleHudUI>(true);
            var progress=game.ProgressionUI;
            if(step==1)
            {
                Application.runInBackground=true;Coverage();
                var menu=game.startScreenRoot.transform.Find("MenuPanel");
                Require(menu.GetComponentsInChildren<Button>(true).All(b=>b.GetComponent<ButtonDesign>()==null),"Title Play/Help/Collection remain unchanged");
                game.StartGame(0);Next(2,5);
            }
            else if(step==2)
            {
                start=game.deckPreviewRoot.GetComponentsInChildren<Button>().First(b=>b.name=="BattleStartButton");
                clickCount=0;start.onClick.AddListener(()=>clickCount++);Check(start);
                ScreenCapture.CaptureScreenshot("StageReview/button-design-deck.png");
                Send(start,ExecuteEvents.pointerEnterHandler);Next(3,.5f);
            }
            else if(step==3)
            {Require(start.GetComponent<ButtonDesign>().Surface.hover>.99f,"Pointer hover lights gold frame (hover="+start.GetComponent<ButtonDesign>().Surface.hover+", interactable="+start.IsInteractable()+", design active="+start.GetComponent<ButtonDesign>().isActiveAndEnabled+")");Send(start,ExecuteEvents.pointerDownHandler);Next(4,.5f);}
            else if(step==4)
            {Require(start.GetComponent<ButtonDesign>().Surface.pressed>.99f,"Pointer down shows depressed state");Send(start,ExecuteEvents.pointerUpHandler);Send(start,ExecuteEvents.pointerExitHandler);Next(5,.2f);}
            else if(step==5)
            {
                var s=start.GetComponent<ButtonDesign>().Surface;Require(s.hover<.01f && s.pressed<.01f,"Release and exit restore normal appearance");
                start.interactable=false;Next(6,.2f);
            }
            else if(step==6)
            {
                Require(start.GetComponent<ButtonDesign>().Surface.disabled>.99f,"Disabled button is desaturated");
                Send(start,ExecuteEvents.pointerClickHandler);Require(clickCount==0,"Disabled button does not invoke action");
                start.interactable=true;Next(7,.2f);
            }
            else if(step==7){Click(start);Next(8,7);}
            else if(step==8)
            {
                Require(clickCount==1 && battle.CanPlayerAct,"Start action fires once and battle starts");Coverage();
                ScreenCapture.CaptureScreenshot("StageReview/button-design-battle.png");Click(hud.pauseButton);Next(9,.2f);
            }
            else if(step==9)
            {Require(hud.IsPaused && Time.timeScale==0,"Pause action works");Send(hud.resumeButton,ExecuteEvents.pointerEnterHandler);Next(10,.2f);}
            else if(step==10)
            {
                Require(hud.resumeButton.GetComponent<ButtonDesign>().Surface.hover>.99f,"Hover animates while time scale is zero");
                Check(hud.resumeButton);Check(hud.titleButton);ScreenCapture.CaptureScreenshot("StageReview/button-design-pause.png");Next(11,.2f);
            }
            else if(step==11){Click(hud.resumeButton);Next(12,.2f);}
            else if(step==12)
            {
                Require(!hud.IsPaused && Time.timeScale==1,"Resume action restores gameplay");
                Click(Find("DrawPile"));ScreenCapture.CaptureScreenshot("StageReview/button-design-pile.png");Next(13,.6f);
            }
            else if(step==13)
            {
                var pile=game.battleUIRoot.GetComponentsInChildren<CardPileViewer>(true).First(p=>p.pileType==CardPileViewer.PileType.DrawPile);
                Require(pile.panel.activeInHierarchy,"Draw pile opens");Check(pile.closeButton);ScreenCapture.CaptureScreenshot("StageReview/button-design-pile.png");Next(14,.2f);
            }
            else if(step==14)
            {
                var piles=game.battleUIRoot.GetComponentsInChildren<CardPileViewer>(true);
                Click(piles.First(p=>p.pileType==CardPileViewer.PileType.DrawPile).closeButton);
                Click(Find("Graveyard"));Next(15,.2f);
            }
            else if(step==15)
            {
                var pile=game.battleUIRoot.GetComponentsInChildren<CardPileViewer>(true).First(p=>p.pileType==CardPileViewer.PileType.DiscardPile);
                Require(pile.panel.activeInHierarchy,"Graveyard opens");Click(pile.closeButton);
                CardDetailOverlay.Show(battle.handUI.handParent.GetComponentInChildren<CardUIView>());Next(16,.2f);
            }
            else if(step==16)
            {Click(Find("BackButton"));Next(17,.2f);}
            else if(step==17)
            {
                Require(Object.FindObjectOfType<CardDetailOverlay>()==null,"Detail back button closes modal");
                // Exercise the public post-battle UI entry point without modifying asset/balance data.
                progress.HandleBattleFinished(true);Next(18,.2f);
            }
            else if(step==18)
            {Coverage();Check(Find("ゲームをやめる"));Click(Find("次のバトルへ"));Next(19,.2f);}
            else if(step==19)
            {
                Coverage();Check(Find("新たなカードを獲得"));Check(Find("カードを一枚削除"));Check(Find("HP",true));
                ScreenCapture.CaptureScreenshot("StageReview/button-design-rewards.png");Next(20,.2f);
            }
            else if(step==20){Click(Find("新たなカードを獲得"));Next(21,2);}
            else if(step==21){Coverage();Click(Find("現在のデッキ"));Next(22,.2f);}
            else if(step==22){Click(Find("もどる"));Next(23,.2f);}
            else if(step==23)
            {
                var reward=Object.FindObjectOfType<RewardCardClickHandler>();
                var card=reward.GetComponent<CardUIView>();progress.SelectRewardCard(card.Card.Data,card.gameObject);Next(24,.2f);
            }
            else if(step==24){Check(Find("獲得"));Click(Find("もどる"));Next(25,.2f);}
            else if(step==25)
            {
                var reward=Object.FindObjectOfType<RewardCardClickHandler>();var card=reward.GetComponent<CardUIView>();
                progress.SelectRewardCard(card.Card.Data,card.gameObject);deckCount=game.deckManager.currentDeck.Count;Next(26,.2f);
            }
            else if(step==26){Click(Find("獲得"));Next(27,.2f);}
            else if(step==27)
            {
                Require(game.deckManager.currentDeck.Count==deckCount+1,"Reward acquisition remains connected");
                int max=battle.balanceData.maxDeckSize;while(game.deckManager.currentDeck.Count<max)game.deckManager.currentDeck.Add(new CardInstance(game.deckManager.currentDeck[0].Data));
                var reward=Object.FindObjectsOfType<RewardCardClickHandler>().First(r=>r.enabled);var card=reward.GetComponent<CardUIView>();
                progress.SelectRewardCard(card.Card.Data,card.gameObject);Next(28,.2f);
            }
            else if(step==28){Click(Find("獲得"));Next(29,.2f);}
            else if(step==29)
            {
                Coverage();Check(Find("獲得をやめる"));
                var source=Object.FindObjectOfType<DeckReplacementCardClickHandler>().GetComponent<CardUIView>();
                var panel=source.transform.parent.parent.gameObject;progress.ShowReplacementCard(source.Card,panel,source.gameObject);Next(30,.2f);
            }
            else if(step==30){Check(Find("このカードを削除"));Click(Find("もどる"));Next(31,.2f);}
            else if(step==31){Click(Find("獲得をやめる"));Next(32,.2f);}
            else if(step==32){Click(Find("もどる"));Next(33,.2f);}
            else if(step==33){progress.HandleBattleFinished(true);Next(34,.2f);}
            else if(step==34){Click(Find("次のバトルへ"));Next(35,.2f);}
            else if(step==35){Click(Find("カードを一枚削除"));Next(36,.2f);}
            else if(step==36)
            {
                Check(Find("削除せずに次のバトルに進む"));
                var source=Object.FindObjectOfType<RemovalCardClickHandler>().GetComponent<CardUIView>();progress.ShowRemovalConfirmation(source.Card,source.gameObject);Next(37,.2f);
            }
            else if(step==37){Check(Find("削除する"));Click(Find("もどる"));Next(38,.2f);}
            else if(step==38)
            {battle.currentBattleCount=10;progress.HandleBattleFinished(true);Next(39,.2f);}
            else if(step==39){Coverage();Click(Find("タイトルにもどる"));Next(40,1);}
            else if(step==40)
            {
                Require(game.startScreenRoot.activeInHierarchy,"Game-clear return button returns to title");
                Require(SessionState.GetInt(Key+"Errors",0)==0,"No runtime errors");
                File.AppendAllText(Report,"PASS\n");SessionState.SetInt(Key,0);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception e)
        {File.AppendAllText(Report,"FAIL: "+e+"\n");SessionState.SetInt(Key,0);Time.timeScale=1;EditorApplication.ExitPlaymode();}
    }
    static void Coverage()
    {
        foreach(var b in Object.FindObjectsOfType<Button>(true))
        {
            bool expected=ButtonDesign.ShouldStyle(b);
            Require((b.GetComponent<ButtonDesign>()!=null)==expected,"Style coverage: "+b.name);
            if(expected)Require(b.GetComponentsInChildren<ButtonDesignGraphic>(true).Length==1,"No duplicate style: "+b.name);
        }
    }
    static Button Find(string name,bool prefix=false)
    {
        var matches=Object.FindObjectsOfType<Button>().Where(b=>prefix?b.name.StartsWith(name):b.name==name).ToArray();
        return matches.FirstOrDefault(Hits)??matches.First();
    }
    static bool Hits(Button b)
    {
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)b.transform;var canvas=b.GetComponentInParent<Canvas>().rootCanvas;
        var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        return hits.Count>0 && hits[0].gameObject.transform.IsChildOf(b.transform);
    }
    static void Check(Button b)
    {
        Require(b.GetComponent<ButtonDesign>()!=null,"Styled button: "+b.name);
        Canvas.ForceUpdateCanvases();
        foreach(var text in b.GetComponentsInChildren<TMP_Text>()){text.ForceMeshUpdate();Require(!text.isTextOverflowing,"Text fits: "+b.name);}
        var rect=(RectTransform)b.transform;var canvas=b.GetComponentInParent<Canvas>().rootCanvas;
        var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var data=new PointerEventData(EventSystem.current){position=point};var results=new List<RaycastResult>();EventSystem.current.RaycastAll(data,results);
        if(results.Count==0 || !results[0].gameObject.transform.IsChildOf(b.transform))
            File.AppendAllText(Report,"Raycast diagnostic "+b.name+" at "+point+", rect="+rect.rect+", active="+b.gameObject.activeInHierarchy+", graphic="+b.targetGraphic.IsActive()+": "+string.Join(", ",results.Take(8).Select(r=>r.gameObject.name+" parent="+r.gameObject.transform.parent.name+" order="+r.sortingOrder+" depth="+r.depth))+"\n");
        Require(results.Count>0 && results[0].gameObject.transform.IsChildOf(b.transform),"Button receives raycast: "+b.name);
    }
    static void Click(Button b){Check(b);Send(b,ExecuteEvents.pointerClickHandler);}
    static void Send<T>(Button b,ExecuteEvents.EventFunction<T> handler) where T:IEventSystemHandler
    {ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},handler);}
    static void Next(int step,float delay){SessionState.SetInt(Key,step);SessionState.SetFloat(Key+"Due",Time.realtimeSinceStartup+delay);}
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS: "+message+"\n");}
}
