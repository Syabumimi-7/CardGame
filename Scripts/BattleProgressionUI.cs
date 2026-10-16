using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>連戦終了後の選択、報酬、カード交換、ゲームオーバー／クリアUIを管理します。</summary>
public class BattleProgressionUI : MonoBehaviour
{
    [Header("進行設定")]
    [Min(0f)] public float gameOverReturnDelay = 2.5f;

    private GameManager gameManager;
    private DeckManager deckManager;
    private BattleManager battleManager;
    private GameObject battleUIRoot;
    private RectTransform overlayRoot;
    private TextMeshProUGUI battleCountText;
    private TMP_FontAsset font;
    private readonly List<GameObject> panels = new List<GameObject>();
    private GameObject cardGridPanel;
    private TextMeshProUGUI remainingText;
    private Button acquireButton;
    private CardData selectedReward;
    private GameObject selectedRewardObject;
    private int acquiredCount;
    private bool rewardsBusy;
    private bool recovering, advancingBattle;
    private Sequence rewardEntrySequence;
    private GameObject quitConfirmationPanel;
    private GameObject selectionBeforeQuit;
    public bool IsQuitConfirmationOpen => quitConfirmationPanel != null;

    private int MaxDeckSize => battleManager != null && battleManager.balanceData != null ? battleManager.balanceData.maxDeckSize : 20;
    private int CandidateCount => battleManager != null && battleManager.balanceData != null ? battleManager.balanceData.rewardCardCandidateCount : 10;
    private int MaxRewards => battleManager != null && battleManager.balanceData != null ? battleManager.balanceData.maxRewardCardSelect : 3;
    private int RecoveryAmount => battleManager != null && battleManager.balanceData != null ? battleManager.balanceData.hpRecoveryAmount : 20;

    public void Configure(GameManager owner, DeckManager deck, BattleManager battle, GameObject battleRoot)
    {
        gameManager = owner;
        deckManager = deck;
        battleManager = battle;
        battleUIRoot = battleRoot;
        if (battleManager != null)
        {
            battleManager.progressionUI = this;
            font = battleManager.turnText != null ? battleManager.turnText.font : null;
        }
        EnsureRoots();
    }

    public void ResetRun()
    {
        acquiredCount = 0;
        selectedReward = null;
        HideAllOverlays();
        RefreshBattleCount();
    }

    public void HandleBattleFinished(bool won)
    {
        // Keep battle music running through victory and reward selection.
        if (!won || battleManager.currentBattleCount >= 10) GameAudio.PlayTitle();
        if (won)
        {
            if (battleManager.currentBattleCount >= 10) ShowGameClear();
            else ShowVictoryChoices();
        }
        else
        {
            ShowMessagePanel("GAME OVER", "タイトルへ戻ります…");
            StartCoroutine(ReturnToTitleAfterDelay());
        }
    }

    public void RefreshBattleCount()
    {
        EnsureRoots();
        if (battleCountText != null && battleManager != null)
            battleCountText.text = $"現在 {battleManager.currentBattleCount} 連戦目";
    }

    public void HideAllOverlays()
    {
        CancelPendingActions();
        if (rewardsBusy && gameManager != null) gameManager.CancelPackOpening();
        rewardEntrySequence?.Kill();
        rewardEntrySequence = null;
        rewardsBusy = false;
        // Confirmation modals are siblings of the main panel, not its children.
        foreach (GameObject panel in panels)
        {
            if (panel == null) continue;
            panel.SetActive(false);
            Destroy(panel);
        }
        panels.Clear();
        quitConfirmationPanel = null;
        selectionBeforeQuit = null;
        cardGridPanel = null;
        selectedReward = null;
        selectedRewardObject = null;
    }

    public void CancelPendingActions()
    {
        // Timers from an abandoned run must not heal, advance, or return a newer run to the title.
        StopAllCoroutines();
        recovering = advancingBattle = false;
    }

    private void LockCurrentChoices()
    {
        foreach (var panel in panels)
            if (panel != null && panel.activeInHierarchy)
                foreach (var button in panel.GetComponentsInChildren<Button>()) button.interactable = false;
    }

    private IEnumerator ReturnToTitleAfterDelay()
    {
        yield return new WaitForSeconds(gameOverReturnDelay);
        gameManager.ShowTitle();
    }

    private void ShowVictoryChoices()
    {
        GameObject panel = CreatePanel("VictoryPanel", new Color(0.04f, 0.09f, 0.07f, 0.96f));
        CreateText(panel.transform, "勝利！", 58, new Vector2(0.5f, 0.68f), new Vector2(700, 90));
        CreateButton(panel.transform, "次のバトルへ", new Vector2(0.5f, 0.48f), new Vector2(360, 78), ShowRewardTypeSelection);
        CreateButton(panel.transform, "ゲームをやめる", new Vector2(0.5f, 0.34f), new Vector2(360, 70), () => ShowQuitConfirmation(gameManager.ShowTitle));
    }

    public void ShowQuitConfirmation(UnityEngine.Events.UnityAction confirm)
    {
        if (IsQuitConfirmationOpen || confirm == null) return;
        selectionBeforeQuit = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        GameObject panel = CreatePanel("QuitConfirmationPanel", new Color(0.02f, 0.025f, 0.03f, 0.98f), false);
        quitConfirmationPanel = panel;
        // Above the pause menu (90), below screen transitions (100).
        Canvas canvas = panel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 95;
        panel.AddComponent<GraphicRaycaster>();
        CreateText(panel.transform, "本当にゲームをやめますか？", 42, new Vector2(0.5f, 0.58f), new Vector2(900, 90));
        CreateButton(panel.transform, "はい", new Vector2(0.42f, 0.14f), new Vector2(240, 68), () =>
        {
            CancelQuitConfirmation();
            confirm();
        });
        Button cancel = CreateButton(panel.transform, "いいえ", new Vector2(0.58f, 0.14f), new Vector2(240, 68), CancelQuitConfirmation);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(cancel.gameObject);
    }

    public void CancelQuitConfirmation()
    {
        if (!IsQuitConfirmationOpen) return;
        GameObject panel = quitConfirmationPanel;
        quitConfirmationPanel = null;
        panels.Remove(panel);
        panel.SetActive(false);
        Destroy(panel);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(selectionBeforeQuit != null && selectionBeforeQuit.activeInHierarchy
                ? selectionBeforeQuit : null);
        selectionBeforeQuit = null;
    }

    private void ShowRewardTypeSelection()
    {
        // 報酬画面でも確定済みの性能を表示し、前の戦闘の一時的な強化だけを解除します。
        deckManager.ResetCardsForBattle();
        GameObject panel = CreatePanel("RewardTypePanel", new Color(0.04f, 0.07f, 0.1f, 0.97f));
        CreateText(panel.transform, "報酬を選択", 52, new Vector2(0.5f, 0.72f), new Vector2(700, 90));
        CreateButton(panel.transform, "新たなカードを獲得", new Vector2(0.5f, 0.56f), new Vector2(420, 78), ShowCardRewards);
        CreateButton(panel.transform, "カードを一枚削除", new Vector2(0.5f, 0.42f), new Vector2(420, 78), ShowCardRemoval);
        CreateButton(panel.transform, $"HPを{RecoveryAmount}回復", new Vector2(0.5f, 0.28f), new Vector2(420, 78), RecoverAndContinue);
    }

    private void RecoverAndContinue()
    {
        if (recovering || advancingBattle || rewardsBusy) return;
        recovering = true;
        LockCurrentChoices();
        GameAudio.PlayRecovery();
        StartCoroutine(RecoverAndContinueRoutine());
    }

    private IEnumerator RecoverAndContinueRoutine()
    {
        PlayerStatus player = battleManager.playerStatus;
        player.Heal(RecoveryAmount);
        yield return battleManager.AnimateHealthBars();
        recovering = false;
        StartNextBattle();
    }

    private void ShowCardRewards()
    {
        if (rewardsBusy || recovering || advancingBattle) return;
        rewardsBusy = true;
        acquiredCount = 0;
        // Prepare the destination without replacing the menu that is about to fade.
        GameObject panel = CreatePanel("CardRewardPanel", Color.clear, false);
        panel.SetActive(false);
        cardGridPanel = panel;
        bool started = gameManager.PlayRewardPack(panel, () =>
        {
            foreach (GameObject previous in panels)
            {
                if (previous == null || previous == panel) continue;
                previous.SetActive(false);
                Destroy(previous);
            }
            panels.Clear();
            panels.Add(panel);
            panel.SetActive(true);
        }, () => PopulateCardRewards(panel));
        if (!started)
        {
            rewardsBusy = false;
            panels.Remove(panel);
            Destroy(panel);
            cardGridPanel = null;
            Debug.LogError("報酬のパック開封を開始できませんでした。パック選択UIを確認してください。");
        }
    }

    private void PopulateCardRewards(GameObject panel)
    {
        CanvasGroup panelGroup = panel.AddComponent<CanvasGroup>();
        panelGroup.interactable = false;
        CreateText(panel.transform, "獲得するカードを選択", 44, new Vector2(0.5f, 0.92f), new Vector2(800, 70));
        remainingText = CreateText(panel.transform, string.Empty, 30, new Vector2(0.18f, 0.08f), new Vector2(320, 55));
        remainingText.enableWordWrapping = false;
        remainingText.enableAutoSizing = true;
        remainingText.fontSizeMin = 20f;
        remainingText.fontSizeMax = 30f;
        CreateButton(panel.transform, "現在のデッキ", new Vector2(0.61f, 0.08f), new Vector2(240, 64), ShowCurrentDeck);
        CreateButton(panel.transform, "次のバトルへ", new Vector2(0.82f, 0.08f), new Vector2(290, 64), StartNextBattle);

        GameObject gridObject = CreateUIObject("RewardGrid", panel.transform);
        RectTransform gridRect = gridObject.GetComponent<RectTransform>();
        SetRect(gridRect, new Vector2(0.08f, 0.2f), new Vector2(0.92f, 0.84f), Vector2.zero, Vector2.zero);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        CardOverviewGrid.Configure(grid, CandidateCount);

        List<CardData> pool = deckManager.GetPool(gameManager.SelectedPoolType);
        var lottery = new CardRarityLottery(pool);
        Sequence entrySequence = DOTween.Sequence().SetLink(panel);
        rewardEntrySequence = entrySequence;
        for (int i = 0; i < CandidateCount && pool != null && pool.Count > 0; i++)
        {
            CardData data = lottery.Draw();
            if (data == null) break;
            GameObject cardObject = Instantiate(gameManager.deckUIManager.cardUIPrefab, gridObject.transform);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            if (cardRect != null) cardRect.sizeDelta = grid.cellSize;
            CardUIView view = cardObject.GetComponent<CardUIView>();
            if (view != null)
            {
                view.Setup(new CardInstance(data));
                view.SetStandaloneDetailEnabled(false);
            }
            RewardCardClickHandler click = cardObject.AddComponent<RewardCardClickHandler>();
            click.Setup(this, data);
            cardObject.transform.localScale = Vector3.zero;
            CardAcquisitionPresentation.AddCardEntry(entrySequence, cardObject.transform, i);
        }
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
        foreach (CardResponsiveLayout layout in gridObject.GetComponentsInChildren<CardResponsiveLayout>())
            layout.UpdateLayout();
        entrySequence.OnComplete(() =>
        {
            rewardsBusy = false;
            if (panelGroup != null) panelGroup.interactable = true;
            rewardEntrySequence = null;
        });
        // Keep empty/custom reward pools operable as well.
        if (gridObject.transform.childCount == 0)
        {
            entrySequence.Kill();
            rewardEntrySequence = null;
            rewardsBusy = false;
            panelGroup.interactable = true;
        }
        UpdateRemainingText();
    }

    public void SelectRewardCard(CardData data, GameObject cardObject)
    {
        if (rewardsBusy || data == null || acquiredCount >= MaxRewards) return;
        selectedReward = data;
        selectedRewardObject = cardObject;

        GameObject modal = CreatePanel("CardConfirmPanel", new Color(0.02f, 0.02f, 0.025f, 0.98f), false);
        CreateCardPreview(modal.transform, new CardInstance(data), new Vector2(0.5f, 0.57f));
        acquireButton = CreateButton(modal.transform, "獲得", new Vector2(0.42f, 0.17f), new Vector2(220, 68), AcquireSelectedReward);
        CreateButton(modal.transform, "もどる", new Vector2(0.58f, 0.17f), new Vector2(220, 68), () => CancelRewardSelection(modal));
        if (acquiredCount >= MaxRewards) acquireButton.gameObject.SetActive(false);
    }

    private void AcquireSelectedReward()
    {
        if (selectedReward == null || acquiredCount >= MaxRewards) return;
        if (deckManager.currentDeck.Count >= MaxDeckSize)
        {
            ShowDeckReplacement();
            return;
        }

        AddSelectedReward();
        CloseTopModal();
    }

    private void ShowDeckReplacement()
    {
        GameObject panel = CreatePanel("DeckReplacementPanel", new Color(0.035f, 0.025f, 0.04f, 0.99f), false);
        CreateText(panel.transform, "入れ替えるカードを選択", 42, new Vector2(0.5f, 0.9f), new Vector2(800, 70));
        CreateButton(panel.transform, "獲得をやめる", new Vector2(0.5f, 0.08f), new Vector2(280, 62), () => Destroy(panel));

        GameObject gridObject = CreateUIObject("DeckGrid", panel.transform);
        RectTransform rect = gridObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        CardOverviewGrid.Configure(grid, deckManager.currentDeck.Count);

        foreach (CardInstance card in new List<CardInstance>(deckManager.currentDeck))
        {
            if (card == null || card.Data == null) continue;
            GameObject cardObject = Instantiate(gameManager.deckUIManager.cardUIPrefab, gridObject.transform);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            if (cardRect != null) cardRect.sizeDelta = grid.cellSize;
            CardUIView view = cardObject.GetComponent<CardUIView>();
            if (view != null)
            {
                view.Setup(card);
                view.SetStandaloneDetailEnabled(false);
            }
            DeckReplacementCardClickHandler click = cardObject.AddComponent<DeckReplacementCardClickHandler>();
            click.Setup(this, card, panel);
        }
    }

    public void ShowReplacementCard(CardInstance card, GameObject replacementPanel, GameObject sourceCard)
    {
        if (card == null || card.Data == null) return;
        GameObject modal = CreatePanel("ReplacementConfirmPanel", new Color(0.015f, 0.015f, 0.02f, 0.99f), false);
        CreateCardPreview(modal.transform, card, new Vector2(0.5f, 0.57f));
        CreateButton(modal.transform, "このカードを削除", new Vector2(0.42f, 0.17f), new Vector2(280, 68), () => ReplaceCard(card, replacementPanel));
        CreateButton(modal.transform, "もどる", new Vector2(0.58f, 0.17f), new Vector2(220, 68), () =>
        {
            CardUIView view = sourceCard != null ? sourceCard.GetComponent<CardUIView>() : null;
            if (view != null) view.ResetSelection();
            Destroy(modal);
        });
    }

    private void ReplaceCard(CardInstance removedCard, GameObject replacementPanel)
    {
        if (selectedReward == null || removedCard == null) return;
        deckManager.currentDeck.Remove(removedCard);
        AddSelectedReward();
        Destroy(replacementPanel);
        if (overlayRoot != null)
        {
            Transform replacementModal = overlayRoot.Find("ReplacementConfirmPanel");
            if (replacementModal != null) Destroy(replacementModal.gameObject);
        }
        CloseTopModal();
    }

    private void AddSelectedReward()
    {
        deckManager.currentDeck.Add(new CardInstance(selectedReward));
        acquiredCount++;
        if (selectedRewardObject != null)
        {
            CanvasGroup group = selectedRewardObject.GetComponent<CanvasGroup>();
            if (group == null) group = selectedRewardObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            RewardCardClickHandler click = selectedRewardObject.GetComponent<RewardCardClickHandler>();
            if (click != null) click.enabled = false;
        }
        selectedRewardObject = null;
        selectedReward = null;
        UpdateRemainingText();
    }

    private void ShowCardRemoval()
    {
        if (rewardsBusy || recovering || advancingBattle) return;
        GameObject panel = CreatePanel("CardRemovalPanel", new Color(0.035f, 0.025f, 0.04f, 0.99f));
        CreateText(panel.transform, "削除するカードを一枚選択", 42, new Vector2(0.5f, 0.91f), new Vector2(800, 70));
        CreateButton(panel.transform, "削除せずに次のバトルに進む", new Vector2(0.5f, 0.08f), new Vector2(460, 64), StartNextBattle);

        GameObject gridObject = CreateUIObject("RemovalDeckGrid", panel.transform);
        RectTransform rect = gridObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        CardOverviewGrid.Configure(grid, deckManager.currentDeck.Count);

        foreach (CardInstance card in new List<CardInstance>(deckManager.currentDeck))
        {
            if (card == null || card.Data == null) continue;
            GameObject cardObject = Instantiate(gameManager.deckUIManager.cardUIPrefab, gridObject.transform);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            if (cardRect != null) cardRect.sizeDelta = grid.cellSize;
            CardUIView view = cardObject.GetComponent<CardUIView>();
            if (view != null)
            {
                view.Setup(card);
                view.SetStandaloneDetailEnabled(false);
            }
            RemovalCardClickHandler click = cardObject.AddComponent<RemovalCardClickHandler>();
            click.Setup(this, card, cardObject);
        }
    }

    public void ShowRemovalConfirmation(CardInstance card, GameObject sourceCard)
    {
        if (card == null || card.Data == null) return;
        GameObject modal = CreatePanel("RemovalConfirmPanel", new Color(0.015f, 0.015f, 0.02f, 0.99f), false);
        TextMeshProUGUI heading = CreateText(modal.transform, "このカードを削除しますか？", 36, new Vector2(0.5f, 0.88f), new Vector2(700, 60));
        CreateCardPreview(modal.transform, card, new Vector2(0.5f, 0.56f), heading.rectTransform);
        CreateButton(modal.transform, "削除する", new Vector2(0.42f, 0.15f), new Vector2(220, 68), () => RemoveCardAndContinue(card));
        CreateButton(modal.transform, "もどる", new Vector2(0.58f, 0.15f), new Vector2(220, 68), () =>
        {
            CardUIView view = sourceCard != null ? sourceCard.GetComponent<CardUIView>() : null;
            if (view != null) view.ResetSelection();
            Destroy(modal);
        });
    }

    private void RemoveCardAndContinue(CardInstance card)
    {
        if (card == null || !deckManager.currentDeck.Remove(card)) return;
        StartNextBattle();
    }

    private void CancelRewardSelection(GameObject modal)
    {
        if (selectedRewardObject != null)
        {
            CardUIView view = selectedRewardObject.GetComponent<CardUIView>();
            if (view != null) view.ResetSelection();
        }
        selectedReward = null;
        selectedRewardObject = null;
        if (modal != null) Destroy(modal);
    }

    private void UpdateRemainingText()
    {
        if (remainingText != null) remainingText.text = $"あと <color=#F1CE83><b>{Mathf.Max(0, MaxRewards - acquiredCount)}</b></color> 枚獲得できます";
    }

    private void ShowCurrentDeck()
    {
        GameObject panel = CreatePanel("CurrentDeckPanel", new Color(0.025f, 0.035f, 0.05f, 0.99f), false);
        CreateText(panel.transform, $"現在のデッキ（{deckManager.currentDeck.Count}/{MaxDeckSize}）", 42, new Vector2(0.5f, 0.91f), new Vector2(800, 70));
        CreateButton(panel.transform, "もどる", new Vector2(0.5f, 0.08f), new Vector2(240, 64), () => Destroy(panel));

        GameObject gridObject = CreateUIObject("CurrentDeckGrid", panel.transform);
        RectTransform rect = gridObject.GetComponent<RectTransform>();
        SetRect(rect, new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.82f), Vector2.zero, Vector2.zero);
        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        CardOverviewGrid.Configure(grid, deckManager.currentDeck.Count);

        foreach (CardInstance card in deckManager.currentDeck)
        {
            if (card == null || card.Data == null) continue;
            GameObject cardObject = Instantiate(gameManager.deckUIManager.cardUIPrefab, gridObject.transform);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            if (cardRect != null) cardRect.sizeDelta = grid.cellSize;
            CardUIView view = cardObject.GetComponent<CardUIView>();
            if (view != null) view.Setup(card);
        }
    }

    private void StartNextBattle()
    {
        if (rewardsBusy || recovering || advancingBattle) return;
        advancingBattle = true;
        LockCurrentChoices();
        battleManager.currentBattleCount++;
        gameManager.StartBattleWithTransition();
    }

    private void ShowGameClear()
    {
        GameObject panel = CreatePanel("GameClearPanel", new Color(0.08f, 0.055f, 0.01f, 0.97f));
        CreateText(panel.transform, "GAME CLEAR!", 72, new Vector2(0.5f, 0.65f), new Vector2(900, 110));
        CreateText(panel.transform, "10連戦制覇、おめでとうございます！", 38, new Vector2(0.5f, 0.51f), new Vector2(900, 70));
        CreateButton(panel.transform, "タイトルにもどる", new Vector2(0.5f, 0.32f), new Vector2(340, 74), gameManager.ShowTitle);
    }

    private void ShowMessagePanel(string title, string message)
    {
        GameObject panel = CreatePanel("MessagePanel", new Color(0.06f, 0.015f, 0.015f, 0.97f));
        CreateText(panel.transform, title, 68, new Vector2(0.5f, 0.58f), new Vector2(900, 100));
        CreateText(panel.transform, message, 32, new Vector2(0.5f, 0.43f), new Vector2(800, 70));
    }

    private void EnsureRoots()
    {
        if (battleUIRoot == null || overlayRoot != null) return;
        GameObject overlay = CreateUIObject("ProgressionUI", battleUIRoot.transform);
        overlayRoot = overlay.GetComponent<RectTransform>();
        SetRect(overlayRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        battleCountText = CreateText(overlay.transform, string.Empty, 28, new Vector2(0.5f, 0.94f), new Vector2(420, 52));
        battleCountText.raycastTarget = false;
        // The refined scene supplies its own framed counter; other scenes retain this fallback.
        if (battleUIRoot.GetComponentInChildren<BattleHudUI>(true) != null)
            battleCountText.gameObject.SetActive(false);
    }

    private GameObject CreatePanel(string name, Color color, bool replaceActive = true)
    {
        EnsureRoots();
        if (replaceActive) HideAllOverlays();
        GameObject panel = CreateUIObject(name, overlayRoot);
        RectTransform rect = panel.GetComponent<RectTransform>();
        SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image image = panel.AddComponent<Image>();
        image.color = color;
        panels.RemoveAll(existing => existing == null);
        panels.Add(panel);
        return panel;
    }

    private TextMeshProUGUI CreateText(Transform parent, string value, float size, Vector2 anchor, Vector2 dimensions)
    {
        GameObject obj = CreateUIObject("Text", parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        if (parent.GetComponent<Button>() == null)
            GameScreenTextStyle.Apply(text, size >= 36f, size >= 42f);
        return text;
    }

    private CardUIView CreateCardPreview(Transform parent, CardInstance card, Vector2 anchor, RectTransform heading = null)
    {
        GameObject obj = Instantiate(gameManager.deckUIManager.cardUIPrefab, parent);
        obj.name = "CardPreview";
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = Vector2.zero;
        CardPreviewLayout.Apply(rect, heading);
        obj.transform.localScale = Vector3.one;
        CardUIView view = obj.GetComponent<CardUIView>();
        if (view != null)
        {
            view.Setup(card);
            view.SetStandaloneDetailEnabled(false);
            view.enabled = false;
        }
        UIHoverScale hover = obj.GetComponent<UIHoverScale>();
        if (hover != null) hover.enabled = false;
        foreach (Graphic graphic in obj.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        return view;
    }

    private Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 dimensions, UnityEngine.Events.UnityAction action, bool anchored = true)
    {
        GameObject obj = CreateUIObject(label, parent);
        RectTransform rect = obj.GetComponent<RectTransform>();
        if (anchored)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = Vector2.zero;
        }
        rect.sizeDelta = dimensions;
        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.16f, 0.38f, 0.3f, 0.98f);
        Button button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        TextMeshProUGUI text = CreateText(obj.transform, label, 26, new Vector2(0.5f, 0.5f), dimensions - new Vector2(16, 8));
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        if (anchored && anchor.y <= 0.17f)
            BottomScreenButton.Apply(button, anchor.x);
        ButtonDesign.Apply(button);
        return button;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void CloseTopModal()
    {
        if (overlayRoot == null) return;
        Transform modal = overlayRoot.Find("CardConfirmPanel");
        if (modal != null) Destroy(modal.gameObject);
    }
}

public class RewardCardClickHandler : MonoBehaviour, IPointerClickHandler
{
    private BattleProgressionUI owner;
    private CardData data;

    public void Setup(BattleProgressionUI progression, CardData cardData)
    {
        owner = progression;
        data = cardData;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) owner?.SelectRewardCard(data, gameObject);
    }
}

public class DeckReplacementCardClickHandler : MonoBehaviour, IPointerClickHandler
{
    private BattleProgressionUI owner;
    private CardInstance card;
    private GameObject replacementPanel;

    public void Setup(BattleProgressionUI progression, CardInstance cardInstance, GameObject panel)
    {
        owner = progression;
        card = cardInstance;
        replacementPanel = panel;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            owner?.ShowReplacementCard(card, replacementPanel, gameObject);
    }
}

public class RemovalCardClickHandler : MonoBehaviour, IPointerClickHandler
{
    private BattleProgressionUI owner;
    private CardInstance card;
    private GameObject sourceCard;

    public void Setup(BattleProgressionUI progression, CardInstance cardInstance, GameObject source)
    {
        owner = progression;
        card = cardInstance;
        sourceCard = source;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            owner?.ShowRemovalConfirmation(card, sourceCard);
    }
}
