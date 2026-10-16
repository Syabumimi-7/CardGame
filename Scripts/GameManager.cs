using UnityEngine;
using System;

/// <summary>タイトル、デッキ確認、連戦画面の切り替えを管理します。</summary>
public class GameManager : MonoBehaviour
{
    public enum InitialPoolType { Balance, AttackHeavy, DefenseHeavy }

    public static string GetPackDisplayName(int index)
    {
        switch (index)
        {
            case 0: return "電撃の勇者";
            case 1: return "拳の勇者";
            case 2: return "毒ネズミ";
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    [Header("マネージャー")]
    public DeckManager deckManager;
    public DeckUIManager deckUIManager;
    public BattleManager battleManager;

    [Header("画面ルート")]
    public GameObject startScreenRoot;
    public GameObject deckPreviewRoot;
    public GameObject battleUIRoot;

    [Header("初期設定")]
    public InitialPoolType defaultPoolType = InitialPoolType.Balance;

    public int SelectedPoolType { get; private set; }
    private bool runFinished;
    private bool runActive;
    public void RecordRunFinished(bool won, string enemyName)
    {
        if (!runActive || runFinished || (won && battleManager.currentBattleCount < 10)) return;
        runFinished = true;
        runActive = false;
        PlayHistory.Add(PlayHistory.Capture(SelectedPoolType, battleManager.currentBattleCount,
            won, enemyName, deckManager.currentDeck));
    }
    public BattleProgressionUI ProgressionUI { get; private set; }
    private ScreenTransitionUI screenTransition;
    private PackSelectionUI packSelection;
    private PackOpeningPresentation packOpening;
    private CardCollectionUI collectionUI;
    private HelpScreenUI helpUI;
    private CreditsScreenUI creditsUI;
    private Camera sceneCamera;
    private Pose battleCameraPose;
    private float battleCameraFieldOfView;
    public bool IsHelpOpen => helpUI != null && helpUI.IsOpen;
    public GlobalPauseUI PauseUI { get; private set; }

    public void OpenPauseHelp(Transform parent) { if (PauseUI != null) PauseUI.OpenHelp(); }
    public void CloseHelp() { if (helpUI != null) helpUI.Close(); }

    private void Awake()
    {
        sceneCamera = Camera.main;
        if (sceneCamera != null)
        {
            battleCameraPose = new Pose(sceneCamera.transform.position, sceneCamera.transform.rotation);
            battleCameraFieldOfView = sceneCamera.fieldOfView;
        }
        GameAudio.Install(gameObject);
        EnsureCreditsMenuEntry();
        creditsUI = gameObject.AddComponent<CreditsScreenUI>();
        creditsUI.Configure(this);
        collectionUI = gameObject.AddComponent<CardCollectionUI>();
        collectionUI.Configure(this);
        helpUI = gameObject.AddComponent<HelpScreenUI>();
        helpUI.Configure(this);
        packSelection = PackSelectionUI.Install(this);
        packOpening = gameObject.AddComponent<PackOpeningPresentation>();
        ProgressionUI = GetComponent<BattleProgressionUI>();
        if (ProgressionUI == null) ProgressionUI = gameObject.AddComponent<BattleProgressionUI>();
        ProgressionUI.Configure(this, deckManager, battleManager, battleUIRoot);
        PauseUI = gameObject.AddComponent<GlobalPauseUI>();
        PauseUI.Configure(this);
        ShowTitle();
        screenTransition = ScreenTransitionUI.Create(startScreenRoot, deckPreviewRoot, battleUIRoot);
    }

    private void EnsureCreditsMenuEntry()
    {
        Transform menu = startScreenRoot != null ? startScreenRoot.transform.Find("MenuPanel") : null;
        Transform collection = menu != null ? menu.Find("CollectionButton") : null;
        if (collection == null || menu.Find("CreditsButton") != null) return;
        GameObject credits = Instantiate(collection.gameObject, menu);
        credits.name = "CreditsButton";
        var sourceHover = collection.GetComponent<PanelHoverController>();
        var creditsHover = credits.GetComponent<PanelHoverController>();
        if (sourceHover != null && creditsHover != null)
        {
            creditsHover.duration = sourceHover.duration;
            creditsHover.slideOffset = sourceHover.slideOffset;
            var freshSettings = new PanelHoverController.PanelSetting[creditsHover.panelsToShow.Length];
            for (int i = 0; i < freshSettings.Length; i++)
            {
                var source = sourceHover.panelsToShow[i];
                var panel = creditsHover.panelsToShow[i].panel;
                freshSettings[i] = new PanelHoverController.PanelSetting { panel = panel,
                    showAnimation = source.showAnimation, hideAnimation = source.hideAnimation };
                var sourceImage = source.panel.GetComponent<UnityEngine.UI.Image>();
                var image = panel.GetComponent<UnityEngine.UI.Image>();
                if (sourceImage != null && image != null)
                {
                    image.color = sourceImage.color;
                    image.material = sourceImage.material;
                    image.sprite = sourceImage.sprite;
                    image.type = sourceImage.type;
                    // Existing title bars receive their gradient in Start, after this menu is built.
                    foreach (var generator in FindObjectsOfType<GradientImageGenerator>(true))
                        if (generator.targetImages.Contains(sourceImage) && !generator.targetImages.Contains(image))
                            generator.targetImages.Add(image);
                }
            }
            creditsHover.panelsToShow = freshSettings;
        }
        foreach (TMPro.TMP_Text label in credits.GetComponentsInChildren<TMPro.TMP_Text>(true))
            label.text = label.text.Replace("コレクション", "クレジット");
        var button = credits.GetComponent<UnityEngine.UI.Button>();
        if (button != null) button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        // Keep the first entry in place and fit the fourth entry below Collection.
        string[] names = { "StartButton", "HelpButton", "CollectionButton", "CreditsButton" };
        var first = menu.Find(names[0]) as RectTransform;
        if (first == null) return;
        float firstY = first.anchoredPosition.y;
        for (int i = 0; i < names.Length; i++)
        {
            var rect = menu.Find(names[i]) as RectTransform;
            if (rect == null) continue;
            rect.anchoredPosition = new Vector2(first.anchoredPosition.x, firstY - i * 150f);
            var hover = rect.GetComponent<PanelHoverController>();
            if (hover == null || rect.gameObject != credits) continue;
            foreach (var setting in hover.panelsToShow)
            {
                var bar = setting.panel != null ? setting.panel.transform as RectTransform : null;
                if (bar == null) continue;
                bar.anchorMin = Vector2.zero;
                bar.anchorMax = Vector2.one;
                bar.offsetMin = bar.offsetMax = Vector2.zero;
                bar.localScale = Vector3.one;
            }
            hover.ReinitializePanels();
        }
    }

    public void StartGameFromButton() { StartGame((int)defaultPoolType); }

    public void StartGame(int poolType)
    {
        if (packOpening != null && packOpening.IsPlaying) return;
        if (deckManager == null || deckUIManager == null || battleManager == null)
        {
            Debug.LogError("ゲーム開始に必要なManager参照が未設定です。");
            return;
        }

        CancelLegacyPanelTransitions();
        SelectedPoolType = Mathf.Clamp(poolType, 0, 2);
        runFinished = false;
        runActive = true;
        deckManager.CreateInitialDeck(SelectedPoolType);
        battleManager.currentBattleCount = 1;
        if (battleManager.playerStatus != null)
            battleManager.playerStatus.currentHP = battleManager.playerStatus.maxHP;

        ProgressionUI.ResetRun();
        GameObject battleStartButton = FindDescendant(deckPreviewRoot, "BattleStartButton");
        if (battleStartButton != null)
        {
            BottomScreenButton.Apply(battleStartButton.GetComponent<UnityEngine.UI.Button>());
            battleStartButton.SetActive(false);
        }
        // The start button's legacy click handler hides this child panel.
        // Activating the preview root alone does not restore it on the next run.
        GameObject battleStartPanel = FindDescendant(deckPreviewRoot, "BattleStartPanel");
        if (battleStartPanel != null) battleStartPanel.SetActive(true);
        Action displayCards = () =>
            {
                deckUIManager.DisplayDeckWithAnimation(deckManager.currentDeck, () =>
                {
                    if (battleStartButton != null) battleStartButton.SetActive(true);
                });
            };
        PackSelectionItem selected = packSelection != null ? packSelection.GetPack(SelectedPoolType) : null;
        if (selected != null && selected.gameObject.activeInHierarchy)
            packOpening.Play(selected, startScreenRoot, deckPreviewRoot,
                () => ShowOnly(deckPreviewRoot), displayCards);
        else TransitionTo(() => ShowOnly(deckPreviewRoot), displayCards);
    }

    public void OnClickStartBattle()
    {
        if (battleManager == null) return;
        StartBattleWithTransition();
    }

    public bool PlayRewardPack(GameObject destination, Action switchScreen, Action showCards)
    {
        PackSelectionItem selected = packSelection != null ? packSelection.GetPack(SelectedPoolType) : null;
        if (selected == null || packOpening == null || packOpening.IsPlaying) return false;
        packOpening.Play(selected, battleUIRoot, destination, switchScreen, showCards, true);
        return true;
    }

    public void CancelPackOpening() { if (packOpening != null) packOpening.Cancel(); }

    public void StartBattleWithTransition()
    {
        if (battleManager == null) return;
        Action showBattle = () =>
        {
            if (deckUIManager != null) deckUIManager.ClearCards();
            if (ProgressionUI != null) ProgressionUI.HideAllOverlays();
            ShowBattle();
            battleManager.SelectedPackIndex = SelectedPoolType;
        };
        if (screenTransition != null)
        {
            var popup = battleManager.GetComponent<DamagePopupUI>();
            if (popup == null) popup = battleManager.gameObject.AddComponent<DamagePopupUI>();
            popup.Initialize(battleManager);
            if (popup.AreVisualsPrepared)
                screenTransition.Play(() => { showBattle(); battleManager.StartBattle(); });
            else
                screenTransition.PlayPrepared(showBattle, popup.PrepareForBattle(), () => battleManager.StartBattle());
        }
        else TransitionTo(() => { showBattle(); battleManager.StartBattle(); });
    }

    public void ShowTitle()
    {
        runActive = false;
        if (ProgressionUI != null) ProgressionUI.CancelPendingActions();
        if (PauseUI != null) PauseUI.Resume();
        if (packOpening != null) packOpening.Cancel();
        Action show = () =>
        {
            if (creditsUI != null) creditsUI.Close();
            if (helpUI != null) helpUI.Close();
            if (collectionUI != null) collectionUI.Close();
            if (deckUIManager != null) deckUIManager.ClearCards();
            if (battleManager != null) battleManager.CancelBattle();
            if (ProgressionUI != null) ProgressionUI.HideAllOverlays();
            ShowOnly(startScreenRoot);
            SetTitleCamera();
            ResetTitlePanels();
        };
        if (screenTransition == null) show(); else TransitionTo(show);
    }

    public void ShowBattle()
    {
        if (sceneCamera != null)
        {
            sceneCamera.transform.SetPositionAndRotation(battleCameraPose.position, battleCameraPose.rotation);
            sceneCamera.fieldOfView = battleCameraFieldOfView;
        }
        ShowOnly(battleUIRoot);
        if (ProgressionUI != null) ProgressionUI.RefreshBattleCount();
    }

    private void ShowOnly(GameObject target)
    {
        SetActive(startScreenRoot, target == startScreenRoot);
        SetActive(deckPreviewRoot, target == deckPreviewRoot);
        SetActive(battleUIRoot, target == battleUIRoot);
    }

    private void SetTitleCamera()
    {
        if (sceneCamera == null) return;
        float centerX = 0f;
        if (battleManager != null && battleManager.enemyManager != null && battleManager.enemyManager.enemySpawnPoint != null)
            centerX = battleManager.enemyManager.enemySpawnPoint.position.x;
        Vector3 position = battleCameraPose.position;
        position.x = centerX;
        Vector3 forward = battleCameraPose.rotation * Vector3.forward;
        // Face along the stage's front axis, retaining the authored height, tilt and lens.
        Vector3 front = new Vector3(0, forward.y, new Vector2(forward.x, forward.z).magnitude);
        sceneCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(front, Vector3.up));
        sceneCamera.fieldOfView = battleCameraFieldOfView;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    private void TransitionTo(Action switchScreen, Action afterTransition = null)
    {
        if (screenTransition != null) screenTransition.Play(switchScreen, afterTransition);
        else
        {
            switchScreen?.Invoke();
            afterTransition?.Invoke();
        }
    }

    private void CancelLegacyPanelTransitions()
    {
        if (startScreenRoot == null) return;
        foreach (PanelToggleController controller in startScreenRoot.GetComponentsInChildren<PanelToggleController>(true))
            controller.CancelPendingActions();
    }

    private static GameObject FindDescendant(GameObject root, string objectName)
    {
        if (root == null) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == objectName) return child.gameObject;
        return null;
    }

    private void ResetTitlePanels()
    {
        if (startScreenRoot == null) return;

        Transform menuPanel = startScreenRoot.transform.Find("MenuPanel");
        if (menuPanel != null)
        {
            menuPanel.gameObject.SetActive(true);
            menuPanel.localScale = Vector3.one;
            CanvasGroup canvasGroup = menuPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        Transform gameStartPanel = startScreenRoot.transform.Find("GameStartPanel");
        if (gameStartPanel != null) gameStartPanel.gameObject.SetActive(false);
    }
}
