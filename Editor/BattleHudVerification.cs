using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BattleHudVerification
{
    const string Key="BattleHudVerification";
    const string Report="StageReview/battle-hud-verification.txt";
    static CardUIView hoverCard;
    static Vector3 hoverBaseScale;
    static Vector3 hoverRootScale;
    static CardUIView[] crowdedCards;
    static int crowdedIndex;
    static BattleHudVerification() { EditorApplication.update+=Update;Application.logMessageReceived+=Log; }
    public static void Begin()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        // Play mode preserves the editor scene on exit; verification never saves or discards it.
        if(scene.path!=BattleHudAuthoring.ScenePath)throw new InvalidOperationException("Refined scene required.");
        Directory.CreateDirectory("StageReview");File.WriteAllText(Report,"Battle HUD verification\n");
        EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
        SessionState.SetInt(Key,1);SessionState.SetInt(Key+"Errors",0);SessionState.SetFloat(Key+"Due",0);
        EditorApplication.EnterPlaymode();
    }
    static void Log(string message,string stack,LogType type)
    {
        if(SessionState.GetInt(Key,0)==0 || type!=LogType.Error && type!=LogType.Exception && type!=LogType.Assert)return;
        SessionState.SetInt(Key+"Errors",SessionState.GetInt(Key+"Errors",0)+1);
        File.AppendAllText(Report,"ERROR: "+message+"\n");
    }
    public static void BeginTitle()
    {
        Begin();
        Application.runInBackground = true;
        SessionState.SetInt(Key, 90);
    }
    public static void BeginPackPreview()
    {
        Begin();
        Application.runInBackground = true;
        SessionState.SetInt(Key, 95);
    }
    public static void BeginCollection()
    {
        Begin(); Application.runInBackground = true;
        SessionState.SetInt(Key, 100);
    }
    static void Update()
    {
        int step=SessionState.GetInt(Key,0);
        if(step==0 || !EditorApplication.isPlaying)return;
        EditorApplication.QueuePlayerLoopUpdate();
        if(Time.realtimeSinceStartup<SessionState.GetFloat(Key+"Due",0))return;
        try
        {
            var game=Object.FindObjectOfType<GameManager>();var battle=game.battleManager;
            var hud=game.battleUIRoot.GetComponentInChildren<BattleHudUI>(true);
            if (step == 100)
            {
                game.startScreenRoot.transform.Find("MenuPanel/CollectionButton").GetComponent<Button>().onClick.Invoke();
                Next(101, .5f); return;
            }
            if (step == 101)
            {
                var collection = game.GetComponent<CardCollectionUI>();
                for (int p = 0; p < 3; p++)
                {
                    collection.SelectPack(p);
                    var panel = game.startScreenRoot.transform.Find("CardCollection");
                    var selectedPacks = panel.GetComponentsInChildren<BattleHudGraphic>().Where(g => g.name == "SelectedPack").ToArray();
                    Require(selectedPacks.Length == 1 && selectedPacks[0].transform.parent.name == "pack" + (p + 1),
                        "Collection highlights the entire selected pack button");
                    var grid = panel.GetComponentInChildren<GridLayoutGroup>();
                    var cards = grid.GetComponentsInChildren<CardUIView>();
                    Require(cards.Length == 30, "Collection shows thirty cards for pack " + (p + 1));
                    Require(cards.All(c => c.costText.text == "?" && c.descriptionText.text == new CardInstance(c.Card.Data).GetDescription() && !c.Card.IsPerformanceDetermined),
                        "Collection list keeps cost and effects unknown for pack " + (p + 1));
                    var scroll = panel.GetComponentInChildren<ScrollRect>();
                    Require(scroll.content.rect.height > scroll.viewport.rect.height && scroll.verticalScrollbar != null, "Collection supports vertical scrolling");
                }
                collection.SelectPack(0); Next(102, .4f); return;
            }
            if (step == 102)
            {
                ScreenCapture.CaptureScreenshot("StageReview/collection-list.png");
                Next(1021, .6f); return;
            }
            if (step == 1021)
            {
                var list = game.startScreenRoot.transform.Find("CardCollection").GetComponentInChildren<GridLayoutGroup>();
                hoverCard = list.GetComponentInChildren<CardUIView>();
                hoverBaseScale = hoverCard.GetComponent<CardDesignLayout>().Surface.transform.localScale;
                ExecuteEvents.Execute(hoverCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Next(1022, .3f); return;
            }
            if (step == 1022)
            {
                var design = hoverCard.GetComponent<CardDesignLayout>();
                Require(design.Surface.highlight > .99f && Vector3.Distance(design.Surface.transform.localScale,
                    hoverBaseScale * 1.1f) < .001f,
                    "Collection hover uses a modest enlargement and gold highlight");
                Require(hoverCard.GetComponent<Canvas>().overrideSorting && hoverCard.GetComponent<Canvas>().sortingOrder == 71,
                    "Collection hover brings the card to the front");
                Require(Vector3.Distance(design.Surface.rectTransform.TransformPoint(new Vector3(100f, 140f)),
                    hoverCard.transform.TransformPoint(((RectTransform)hoverCard.transform).rect.center)) < .001f,
                    "Collection hover preserves the card center without lifting");
                var clip = hoverCard.transform.Find("CollectionHoverClip").GetComponent<RectMask2D>();
                var viewport = hoverCard.GetComponentInParent<ScrollRect>().viewport;
                var clipBounds = new Vector3[4]; var viewportBounds = new Vector3[4];
                clip.rectTransform.GetWorldCorners(clipBounds); viewport.GetWorldCorners(viewportBounds);
                Require(clip.enabled && Enumerable.Range(0, 4).All(i => Vector3.Distance(clipBounds[i], viewportBounds[i]) < .01f),
                    "Collection hover is clipped to the list viewport inside its sorting canvas");
                ScreenCapture.CaptureScreenshot("StageReview/collection-hover.png");
                Next(1023, .6f); return;
            }
            if (step == 1023)
            {
                ExecuteEvents.Execute(hoverCard.gameObject,
                    new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
                Next(103, .5f); return;
            }
            if (step == 103)
            {
                var collection = game.GetComponent<CardCollectionUI>();
                var cardData = game.deckManager.GetPool(0)[0];
                for (int p = 0; p < 3; p++)
                {
                    collection.SelectPattern(p);
                    var card = game.startScreenRoot.transform.Find("CardCollection/CollectionDetail").GetComponentInChildren<CardUIView>();
                    var expected = CardInstance.CreateCollectionPreview(cardData, p);
                    Require(card.costText.text == expected.EffectiveCost.ToString() && card.descriptionText.text == expected.GetDescription(),
                        "Collection detail shows exact pattern " + (p + 1) + " cost and effects");
                    var detail = game.startScreenRoot.transform.Find("CardCollection/CollectionDetail");
                    Require(detail.GetComponentsInChildren<Transform>(true).Count(t => t.name == "SelectedPattern" && t.gameObject.activeSelf) == 1
                        && detail.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "表示中：パターン" + (p + 1)),
                        "Selected pattern has a full-button highlight and explicit status");
                    Require(detail.GetComponent<Canvas>().sortingOrder > hoverCard.GetComponent<Canvas>().sortingOrder,
                        "Collection detail remains above the hovered list card");
                }
                collection.SelectPattern(1);
                ScreenCapture.CaptureScreenshot("StageReview/collection-detail.png");
                Next(104, .6f); return;
            }
            if (step == 104)
            {
                var collection = game.GetComponent<CardCollectionUI>(); collection.ShowCard(1, false);
                Require(game.startScreenRoot.transform.Find("CardCollection").GetComponentsInChildren<CardUIView>()
                    .First(c => c.Card.IsPerformanceDetermined).Card.Data
                    == game.deckManager.GetPool(0)[1], "Collection can navigate to the next card");
                collection.CloseDetail();
                game.startScreenRoot.transform.Find("CardCollection").GetComponentsInChildren<Button>()
                    .Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "もどる").onClick.Invoke();
                Require(!game.startScreenRoot.transform.Find("MenuPanel").gameObject.activeInHierarchy,
                    "Collection return keeps title hidden until fade-out completes");
                Next(105, .1f); return;
            }
            if (step == 105)
            {
                Require(game.startScreenRoot.GetComponentInParent<Canvas>().transform.Find("ScreenTransition")
                    .GetComponent<CanvasGroup>().alpha > 0f, "Collection return uses shared screen fade");
                Next(106, .8f); return;
            }
            if (step == 106)
            {
                Require(game.startScreenRoot.transform.Find("MenuPanel").gameObject.activeInHierarchy, "Closing collection returns to title");
                Require(SessionState.GetInt(Key + "Errors", 0) == 0, "Collection produces no runtime errors");
                File.AppendAllText(Report, "PASS collection\n"); SessionState.SetInt(Key, 0); EditorApplication.ExitPlaymode(); return;
            }
            if (step == 95)
            {
                game.startScreenRoot.transform.Find("MenuPanel").gameObject.SetActive(false);
                var panel = game.startScreenRoot.transform.Find("GameStartPanel");
                panel.gameObject.SetActive(true);
                panel.localScale = Vector3.one;
                var group = panel.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = 1f;
                Next(96, .8f);
                return;
            }
            if (step == 96)
            {
                ScreenCapture.CaptureScreenshot("StageReview/pack-selection-ingame.png");
                Next(97, 2f);
                return;
            }
            if (step == 97)
            {
                File.AppendAllText(Report, "PASS pack preview against the actual stage\n");
                SessionState.SetInt(Key, 0);
                EditorApplication.ExitPlaymode();
                return;
            }
            if (step == 90)
            {
                var menu = game.startScreenRoot.transform.Find("MenuPanel");
                foreach (string name in new[] { "StartButton", "HelpButton", "CollectionButton", "CreditsButton" })
                {
                    var item = menu.Find(name);
                    var label = item.GetComponentInChildren<TMPro.TMP_Text>(true);
                    var hover = item.GetComponent<PanelHoverController>();
                    File.AppendAllText(Report, name + " item=" + ((RectTransform)item).anchoredPosition + " text=" + label.rectTransform.anchoredPosition
                        + " font=" + label.font.name + " size=" + label.fontSize + " bar=" + hover.panelsToShow[0].defaultScale + "\n");
                }
                ScreenCapture.CaptureScreenshot("StageReview/title-menu-normal.png");
                Next(91, .5f);
                return;
            }
            if (step == 91)
            {
                var credits = game.startScreenRoot.transform.Find("MenuPanel/CreditsButton");
                var collection = game.startScreenRoot.transform.Find("MenuPanel/CollectionButton");
                ExecuteEvents.Execute(credits.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(collection.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Next(910, .15f);
                return;
            }
            if (step == 910 || step == 920)
            {
                var credits = game.startScreenRoot.transform.Find("MenuPanel/CreditsButton");
                var collection = game.startScreenRoot.transform.Find("MenuPanel/CollectionButton");
                var creditHover = credits.GetComponent<PanelHoverController>();
                var sourceHover = collection.GetComponent<PanelHoverController>();
                var creditBar = creditHover.panelsToShow[0];
                var sourceBar = sourceHover.panelsToShow[0];
                File.AppendAllText(Report, "Bar comparison credits=" + creditBar.panel.GetComponent<Image>().color
                    + " material=" + creditBar.panel.GetComponent<Image>().material.name + " collection=" + sourceBar.panel.GetComponent<Image>().color
                    + " material=" + sourceBar.panel.GetComponent<Image>().material.name + "\n");
                Require(creditBar.panel.GetComponent<Image>().color == sourceBar.panel.GetComponent<Image>().color
                    && creditBar.panel.GetComponent<Image>().material == sourceBar.panel.GetComponent<Image>().material
                    && creditBar.panel.GetComponent<Image>().sprite == sourceBar.panel.GetComponent<Image>().sprite,
                    "Credits matches the existing yellow gradient, opacity and material");
                Require(creditHover.duration == sourceHover.duration && creditBar.showAnimation == sourceBar.showAnimation
                    && creditBar.hideAnimation == sourceBar.hideAnimation && Mathf.Abs(creditBar.canvasGroup.alpha - sourceBar.canvasGroup.alpha) < .02f,
                    step == 910 ? "Credits fade-in matches Collection" : "Credits fade-out matches Collection");
                Next(step == 910 ? 92 : 93, step == 910 ? .4f : .5f);
                return;
            }
            if (step == 92)
            {
                var menu = game.startScreenRoot.transform.Find("MenuPanel");
                var credits = menu.Find("CreditsButton");
                var collection = menu.Find("CollectionButton");
                var creditText = credits.GetComponentInChildren<TMPro.TMP_Text>(true);
                var collectionText = collection.GetComponentInChildren<TMPro.TMP_Text>(true);
                Require(creditText.font == collectionText.font && creditText.fontSharedMaterial == collectionText.fontSharedMaterial
                    && creditText.fontSize == collectionText.fontSize, "Credits matches the existing title font and material");
                var bar = credits.GetComponent<PanelHoverController>().panelsToShow[0];
                File.AppendAllText(Report, "Credits bar timeScale=" + Time.timeScale + " delta=" + Time.deltaTime + " tween=" + DG.Tweening.DOTween.IsTweening(bar.canvasGroup) + " self=" + bar.panel.activeSelf + " hierarchy=" + bar.panel.activeInHierarchy
                    + " alpha=" + bar.canvasGroup.alpha + " local=" + bar.panel.transform.IsChildOf(credits)
                    + " menu=" + menu.gameObject.activeInHierarchy + " credits=" + credits.gameObject.activeInHierarchy + "\n");
                Require(bar.panel.activeInHierarchy && bar.canvasGroup.alpha > .99f,
                    "Credits shows the yellow hover bar");
                var root = game.startScreenRoot.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
                creditText.ForceMeshUpdate();
                Require(creditText.textInfo.characterInfo.Take(creditText.textInfo.characterCount).Where(c => c.isVisible)
                    .All(c => root.rect.Contains(root.InverseTransformPoint(creditText.transform.TransformPoint(c.bottomLeft)))
                        && root.rect.Contains(root.InverseTransformPoint(creditText.transform.TransformPoint(c.topRight)))),
                    "Credits letters stay inside the screen");
                Require(creditText.text.StartsWith("　") && creditText.rectTransform.anchoredPosition == collectionText.rectTransform.anchoredPosition,
                    "Credits preserves the same text indentation and placement");
                ScreenCapture.CaptureScreenshot("StageReview/title-menu-credits-hover.png");
                Next(921, 1f);
                return;
            }
            if (step == 921)
            {
                foreach (string name in new[] { "CollectionButton", "CreditsButton" })
                    ExecuteEvents.Execute(game.startScreenRoot.transform.Find("MenuPanel/" + name).gameObject,
                        new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                Next(920, .15f);
                return;
            }
            if (step == 93)
            {
                File.AppendAllText(Report, "PASS title preview\n");
                SessionState.SetInt(Key, 0);
                EditorApplication.ExitPlaymode();
                return;
            }
            if(step==1){Application.runInBackground=true;Require(hud!=null,"Authored HUD present");game.StartGame(0);Next(2,5);}
            else if(step==2)
            {
                var cards=game.deckUIManager.cardParent.GetComponentsInChildren<CardUIView>();
                Require(cards.Length>0 && cards.All(c=>c.GetComponent<CardDesignLayout>()!=null && HasIllustration(c)),"Deck overview shows the matching card illustrations");
                ScreenCapture.CaptureScreenshot("StageReview/card-design-deck.png");Next(15,.5f);
            }
            else if(step==15){game.OnClickStartBattle();Next(20,1);}
            else if(step==20)
            {
                Require(hud.banner.alpha>0,"Turn banner appears");
                ScreenCapture.CaptureScreenshot("StageReview/battle-hud-banner.png");Next(3,6);
            }
            else if(step==3)
            {
                Require(battle.CanPlayerAct && battle.Hand.Count>0,$"Battle starts and hand is drawn (state={battle.State}, hand={battle.Hand.Count}, animating={battle.handUI.IsAnimating}, active={game.battleUIRoot.activeInHierarchy}, time={Time.time})");
                Require(hud.enemyName.text==battle.enemyManager.CurrentEnemy.Data.enemyName,"Enemy name binding");
                Require(battle.handUI.handParent.GetComponentsInChildren<CardUIView>().All(HasIllustration),"Hand cards show the matching illustrations");
                Require(hud.banner.alpha==0,"Turn banner fades out");
                Require(Hits(battle.endTurnButton),"End-turn button receives pointer input");
                Require(Hits(hud.pauseButton),"Pause button receives pointer input");
                var crowdedHand = Enumerable.Range(0, 20).Select(i => battle.Hand[i % battle.Hand.Count]).ToList();
                battle.handUI.StartCoroutine(VerifyDrawDestinations(battle.handUI, crowdedHand));
                Next(30, 4f);
            }
            else if (step == 30)
            {
                crowdedCards = battle.handUI.handParent.GetComponentsInChildren<CardUIView>();
                Require(crowdedCards.Length == 20 && !battle.handUI.IsAnimating, "Twenty-card hand finishes drawing");
                Require(crowdedCards.All(c => Vector2.Distance(((RectTransform)c.transform).rect.size, new Vector2(240f, 350f)) < .01f),
                    "Crowded hand preserves the enlarged card size");
                Require(battle.handUI.handParent.GetComponent<HorizontalLayoutGroup>().spacing < 0f,
                    "Crowded hand overlaps cards instead of shrinking them");
                crowdedIndex = 0;
                Next(31, .3f);
            }
            else if (step == 31)
            {
                hoverCard = crowdedCards[crowdedIndex];
                Require(CardAt(ExposedStripPoint(crowdedIndex)) == hoverCard, "Exposed strip reaches card " + crowdedIndex);
                hoverBaseScale = hoverCard.GetComponent<CardDesignLayout>().Surface.transform.localScale;
                hoverRootScale = hoverCard.transform.localScale;
                ExecuteEvents.Execute(hoverCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Next(32, .3f);
            }
            else if (step == 32)
            {
                var design = hoverCard.GetComponent<CardDesignLayout>();
                Require(hoverCard.GetComponent<Canvas>().overrideSorting && design.Surface.highlight > .99f,
                    "Hovered crowded card comes to the front: " + crowdedIndex);
                Require(Vector3.Distance(design.Surface.transform.localScale,
                    hoverBaseScale * 1.1f * battle.handUI.selectedScale) < .001f, "Hovered crowded card enlarges: " + crowdedIndex);
                Require(CardAt(design.Surface.rectTransform.TransformPoint(design.Surface.rectTransform.rect.center)) == hoverCard,
                    "Full hovered card is above neighboring cards: " + crowdedIndex);
                Require(Enumerable.Range(0, 25).All(i => CardAt(design.Surface.rectTransform.TransformPoint(
                    new Vector3(10f + (i % 5) * 45f, 10f + (i / 5) * 65f, 0f))) == hoverCard),
                    "Entire hovered card stays exposed: " + crowdedIndex);
                var corners = new Vector3[4]; design.Surface.rectTransform.GetWorldCorners(corners);
                var root = hoverCard.GetComponentInParent<Canvas>().rootCanvas.transform as RectTransform;
                Require(corners.All(c => root.rect.Contains(root.InverseTransformPoint(c))), "Hovered card fits inside screen: " + crowdedIndex);
                Require(Enumerable.Range(0, crowdedCards.Length).Where(i => i != crowdedIndex)
                    .All(i => CardAt(ExposedStripPoint(i)) == crowdedCards[i]), "Other cards remain reachable while hovered: " + crowdedIndex);
                if (crowdedIndex == 10)
                {
                    ScreenCapture.CaptureScreenshot("StageReview/battle-hand-overlap.png");
                    Next(320, .3f);
                    return;
                }
                FinishCrowdedHover(battle);
            }
            else if (step == 320)
            {
                FinishCrowdedHover(battle);
            }
            else if (step == 33)
            {
                hoverCard=battle.handUI.handParent.GetComponentInChildren<CardUIView>();
                hoverBaseScale=hoverCard.GetComponent<CardDesignLayout>().Surface.transform.localScale;
                hoverRootScale=hoverCard.transform.localScale;
                ExecuteEvents.Execute(hoverCard.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                battle.playerStatus.modifiers.Add(BattleEffectType.OutgoingDamageModifier,3,2);
                battle.playerStatus.AddPoison(2,5);hud.playerHealth.RefreshStatus();
                Next(4,1);
            }
            else if(step==4)
            {
                var design=hoverCard.GetComponent<CardDesignLayout>();
                Require(design.Surface.highlight>.99f,"Hover adds gold highlight");
                Require(Vector3.Distance(design.Surface.transform.localScale,hoverBaseScale*1.1f*battle.handUI.selectedScale)<.001f,"Hover enlarges the hand card");
                Require(hoverCard.transform.localScale==hoverRootScale,"Hover preserves root animation scale");
                Require(battle.handUI.handParent.GetComponentsInChildren<CardUIView>().All(c=>Quaternion.Angle(c.transform.localRotation,Quaternion.identity)<.01f),"Hand remains straight");
                Require(hud.playerStatusRows.childCount>=2,"Status rows show modifier and poison");
                ScreenCapture.CaptureScreenshot("StageReview/battle-hud.png");
                ExecuteEvents.Execute(hoverCard.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
                Next(40,.5f);
            }
            else if(step==40)
            {
                Require(hoverCard.GetComponent<CardDesignLayout>().Surface.highlight<.01f && Vector3.Distance(hoverCard.GetComponent<CardDesignLayout>().Surface.transform.localScale,hoverBaseScale)<.001f,"Pointer exit restores card appearance");
                hud.pauseButton.onClick.Invoke();
                Require(hud.IsPaused && battle.IsPaused && Time.timeScale==0 && !battle.CanPlayerAct,"Pause blocks gameplay");
                SessionState.SetFloat(Key+"PausedTime",Time.time);Next(5,1);
            }
            else if(step==5)
            {
                Require(Mathf.Approximately(Time.time,SessionState.GetFloat(Key+"PausedTime",-1)),"Scaled battle time stays frozen");
                Require(Hits(hud.resumeButton) && !Hits(battle.endTurnButton),"Pause overlay blocks underlying pointer input");
                ScreenCapture.CaptureScreenshot("StageReview/battle-hud-pause.png");
                Next(50,.5f);
            }
            else if(step==50)
            {
                hud.resumeButton.onClick.Invoke();
                Require(Time.timeScale==1 && battle.CanPlayerAct,"Resume restores input and time");
                var piles=game.battleUIRoot.GetComponentsInChildren<CardPileViewer>(true);
                foreach(var pile in piles){pile.Open();Require(pile.panel.activeSelf,"Pile opens: "+pile.pileType);pile.Close();}
                var playable=battle.Hand.FirstOrDefault(c=>c.CurrentPerformance.cost<=battle.playerStatus.currentCost);
                Require(playable!=null && battle.TryPlayCard(playable),"Card play remains connected");Next(6,3);
            }
            else if(step==6)
            {
                Require(battle.CanPlayerAct,"Card resolution returns control");
                battle.endTurnButton.onClick.Invoke();Require(battle.State==BattleTurnState.Enemy,"End turn starts enemy sequence");
                hud.Pause();SessionState.SetFloat(Key+"PausedTime",Time.time);Next(7,1);
            }
            else if(step==7)
            {
                Require(battle.State==BattleTurnState.Enemy && Mathf.Approximately(Time.time,SessionState.GetFloat(Key+"PausedTime",-1)),"Enemy sequence freezes while paused");
                hud.Resume();Next(8,14);
            }
            else if(step==8)
            {
                Require(battle.CanPlayerAct,"Enemy turn completes after resume");
                CardDetailOverlay.Show(battle.handUI.handParent.GetComponentInChildren<CardUIView>());
                hud.Pause();
                Next(80,.5f);
            }
            else if(step==80)
            {
                ScreenCapture.CaptureScreenshot("StageReview/battle-hud-pause-over-detail.png");Next(81,.5f);
            }
            else if(step==81)
            {
                Require(Hits(hud.resumeButton),"Pause menu appears above card details");
                hud.titleButton.onClick.Invoke();
                Require(game.ProgressionUI.IsQuitConfirmationOpen && hud.IsPaused && Time.timeScale == 0,
                    "Quit confirmation keeps the battle paused");
                var cancel = game.battleUIRoot.GetComponentsInChildren<Button>().Single(b => b.name == "いいえ");
                cancel.onClick.Invoke();
                Require(!game.ProgressionUI.IsQuitConfirmationOpen && hud.IsPaused && Time.timeScale == 0
                    && game.battleUIRoot.activeInHierarchy, "Declining quit preserves the pause menu and battle");
                hud.titleButton.onClick.Invoke();
                Next(82, .3f);
            }
            else if (step == 82)
            {
                var confirm = game.battleUIRoot.GetComponentsInChildren<Button>().Single(b => b.name == "はい");
                Require(Hits(confirm), "Quit confirmation appears above the pause menu");
                confirm.onClick.Invoke();Next(9,2);
            }
            else if(step==9)
            {
                Require(game.startScreenRoot.activeInHierarchy && !game.battleUIRoot.activeInHierarchy && Time.timeScale==1,"Pause menu returns to title and restores time");
                Require(Object.FindObjectOfType<CardDetailOverlay>()==null,"Card details close on return to title");
                game.StartGame(0);Next(10,5);
            }
            else if(step==10){game.OnClickStartBattle();Next(11,7);}
            else if(step==11)
            {
                Require(battle.CanPlayerAct && !hud.IsPaused,"A new game starts after returning to title");
                Require(SessionState.GetInt(Key+"Errors",0)==0,"No runtime errors");
                File.AppendAllText(Report,"PASS\n");SessionState.SetInt(Key,0);EditorApplication.ExitPlaymode();
            }
        }
        catch(Exception ex)
        {
            File.AppendAllText(Report,"FAIL: "+ex+"\n");SessionState.SetInt(Key,0);Time.timeScale=1;EditorApplication.ExitPlaymode();
        }
    }
    static void Next(int step,float delay){SessionState.SetInt(Key,step);SessionState.SetFloat(Key+"Due",Time.realtimeSinceStartup+delay);}
    static void FinishCrowdedHover(BattleManager battle)
    {
        ExecuteEvents.Execute(hoverCard.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
        crowdedIndex++;
        if (crowdedIndex < crowdedCards.Length) Next(31, .3f);
        else
        {
            battle.handUI.StartCoroutine(VerifyDrawDestinations(battle.handUI, battle.Hand.ToList()));
            Next(33, 2f);
        }
    }
    static System.Collections.IEnumerator VerifyDrawDestinations(BattleHandUI hand, IReadOnlyList<CardInstance> cards)
    {
        var draw = hand.ShowDrawnCards(cards);
        Dictionary<CardUIView, Vector3> landed = null;
        while (draw.MoveNext())
        {
            yield return draw.Current;
            var views = hand.handParent.GetComponentsInChildren<CardUIView>();
            if (views.Length == cards.Count && views.All(c => Vector3.Distance(c.transform.localScale, Vector3.one) < .001f))
                landed = views.ToDictionary(c => c, c => c.transform.position);
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(hand.handParent);
        Require(landed != null && landed.All(pair => pair.Key != null && Vector3.Distance(pair.Key.transform.position, pair.Value) < .01f),
            "Draw animation lands directly at final positions without a layout jump: " + cards.Count + " cards");
        var layout = hand.handParent.GetComponent<HorizontalLayoutGroup>();
        float spacing = cards.Count <= 1 ? hand.cardSpacing : Mathf.Min(hand.cardSpacing,
            (hand.handParent.rect.width - layout.padding.horizontal - hand.baseCardSize.x * cards.Count) / (cards.Count - 1));
        Require(Mathf.Approximately(layout.spacing, spacing), "Draw destinations use current card count: " + cards.Count);
    }
    static Vector3 ExposedStripPoint(int index)
    {
        var card = (RectTransform)crowdedCards[index].transform;
        float exposedWidth = index + 1 < crowdedCards.Length
            ? card.InverseTransformPoint(crowdedCards[index + 1].transform.position).x : card.rect.width;
        return card.TransformPoint(new Vector3(card.rect.xMin + exposedWidth * .5f, -168f + 5f, 0f));
    }
    static CardUIView CardAt(Vector3 worldPoint)
    {
        Canvas.ForceUpdateCanvases();
        var canvas = crowdedCards[0].GetComponentInParent<Canvas>().rootCanvas;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, worldPoint) };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        return hits.Count > 0 ? hits[0].gameObject.GetComponentInParent<CardUIView>() : null;
    }
    static bool HasIllustration(CardUIView card)=>card.Card!=null && card.Card.Data!=null && card.Card.Data.cardImage!=null
        && card.cardImage.sprite==card.Card.Data.cardImage && card.cardImage.enabled && card.cardImage.color.a==1;
    static bool Hits(Button button)
    {
        Canvas.ForceUpdateCanvases();
        var rect=button.transform as RectTransform;var canvas=button.GetComponentInParent<Canvas>().rootCanvas;
        Vector2 point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=point};var results=new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer,results);
        if(results.Count==0 || !results[0].gameObject.transform.IsChildOf(button.transform))
            File.AppendAllText(Report,"Raycast at "+button.name+": "+string.Join(", ",results.Take(4).Select(r=>r.gameObject.name+" order="+r.sortingOrder+" depth="+r.depth))+"\n");
        return results.Count>0 && results[0].gameObject.transform.IsChildOf(button.transform);
    }
    static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);File.AppendAllText(Report,"PASS: "+message+"\n");}
}
