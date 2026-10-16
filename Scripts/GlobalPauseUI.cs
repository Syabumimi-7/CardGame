using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Always available pause menu, independent of the current game screen.</summary>
public class GlobalPauseUI : MonoBehaviour
{
    private GameManager game;
    private Button returnToTitleButton;
    private RectTransform overlay, menu, settings, confirmation;
    private HelpScreenUI help;
    private float savedTimeScale;
    private bool savedBattlePause;
    private List<Tween> pausedTweens;
    private GameObject selectionBeforePause;
    private class VolumeHint
    {
        public RectTransform handle;
        public GameObject tooltip;
        public bool dragging;
    }

    private readonly List<VolumeHint> volumeHints = new List<VolumeHint>();
    public bool IsPaused { get; private set; }

    // メニュー・設定・確認画面と、常時表示するポーズボタンを組み立てる。
    public void Configure(GameManager owner)
    {
        game = owner;
        help = gameObject.AddComponent<HelpScreenUI>();
        help.Configure(game, false);
        var root = Rect("GlobalPause", transform, Vector2.zero, new Vector2(1600, 900));
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        root.gameObject.AddComponent<GraphicRaycaster>();
        overlay = Rect("PauseOverlay", root, Vector2.zero, Vector2.zero);
        Stretch(overlay);
        overlay.gameObject.AddComponent<Image>().color = new Color(.008f, .014f, .023f, .94f);
        // ButtonDesign excludes this name so the invisible hit area stays undecorated.
        var dismissArea = Rect("OutsideClickArea", overlay, Vector2.zero, Vector2.zero);
        Stretch(dismissArea);
        var dismissImage = dismissArea.gameObject.AddComponent<Image>();
        dismissImage.color = Color.clear;
        var dismissButton = dismissArea.gameObject.AddComponent<Button>();
        dismissButton.targetGraphic = dismissImage;
        dismissButton.transition = Selectable.Transition.None;
        dismissButton.navigation = new Navigation{mode = Navigation.Mode.None};
        dismissButton.onClick.AddListener(() =>
        {
            if (menu.gameObject.activeSelf && !help.IsOpen)
                Resume();
        });
        menu = Panel("PauseMenu", overlay, new Vector2(620, 640));
        Label("ポーズ", menu, new Vector2(0, 250), new Vector2(520, 66), 40, true);
        MakeButton("再開", menu, new Vector2(0, 135), Resume);
        MakeButton("ヘルプ", menu, new Vector2(0, 30), OpenHelp);
        MakeButton("設定", menu, new Vector2(0, -75), OpenSettings);
        returnToTitleButton = MakeButton("ゲームをやめる", menu, new Vector2(0, -180), ConfirmReturnToTitle);
        settings = Panel("Settings", overlay, new Vector2(920, 580));
        Label("設定", settings, new Vector2(0, 220), new Vector2(800, 65), 40, true);
        VolumeSlider("BGM", settings, 80, GameAudio.MusicVolume, GameAudio.SetMusicVolume);
        VolumeSlider("効果音", settings, -70, GameAudio.SoundVolume, GameAudio.SetSoundVolume);
        MakeButton("ポーズへもどる", settings, new Vector2(0, -225), CloseSettings);
        settings.gameObject.SetActive(false);
        confirmation = Panel("QuitConfirmation", overlay, new Vector2(860, 400));
        Label("タイトルへ戻りますか？", confirmation, new Vector2(0, 100), new Vector2(800, 65), 34, true);
        Label("進行中の冒険は終了します", confirmation, new Vector2(0, 20), new Vector2(800, 50), 25);
        MakeButton("はい", confirmation, new Vector2(-205, -100), ReturnToTitle);
        MakeButton("いいえ", confirmation, new Vector2(205, -100), CancelReturnToTitle);
        confirmation.gameObject.SetActive(false);
        overlay.gameObject.SetActive(false);
        // The launcher stays above collections, help, rewards, and transition fades.
        var launcher = Rect("PauseLauncher", root, Vector2.zero, Vector2.zero);
        Stretch(launcher);
        var launcherCanvas = launcher.gameObject.AddComponent<Canvas>();
        launcherCanvas.overrideSorting = true;
        launcherCanvas.sortingOrder = 130;
        launcher.gameObject.AddComponent<GraphicRaycaster>();
        var button = MakeButton("", launcher, Vector2.zero, () =>
        {
            if (!IsPaused)
                Pause();
        });
        button.name = "Pause";
        var gear = Rect("Gear", button.transform, Vector2.zero, new Vector2(44, 44)).gameObject.AddComponent<BattleHudGraphic>();
        gear.shape = BattleHudGraphic.Shape.Gear;
        gear.color = new Color(.94f, .84f, .62f);
        gear.metal = new Color(.94f, .84f, .62f);
        gear.raycastTarget = false;
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-24, -20);
        rect.sizeDelta = new Vector2(64, 64);
    }

    // 再開時に復元する時間・戦闘・Tween・選択中UIの状態を保存する。
    public void Pause()
    {
        if (IsPaused)
            return;
        var titleMenu = game.startScreenRoot.transform.Find("MenuPanel");
        bool onTitleMenu = titleMenu != null && titleMenu.gameObject.activeInHierarchy;
        returnToTitleButton.gameObject.SetActive(!onTitleMenu);
        float menuHeight = onTitleMenu ? 535f : 640f;
        float verticalShift = (menuHeight - menu.sizeDelta.y) * .5f;
        foreach (RectTransform item in menu)
            item.anchoredPosition += new Vector2(0, verticalShift);
        menu.sizeDelta = new Vector2(menu.sizeDelta.x, menuHeight);
        savedTimeScale = Time.timeScale;
        selectionBeforePause = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        savedBattlePause = game.battleManager.IsPaused;
        IsPaused = true;
        game.battleManager.IsPaused = true;
        Time.timeScale = 0;
        pausedTweens = DOTween.PlayingTweens();
        if (pausedTweens != null)
            foreach (var tween in pausedTweens)
                tween.Pause();
        menu.gameObject.SetActive(true);
        overlay.gameObject.SetActive(true);
        SelectFirst(menu);
    }

    // Pauseで保存した状態を復元する。
    public void Resume()
    {
        if (!IsPaused)
            return;
        help.Close();
        settings.gameObject.SetActive(false);
        confirmation.gameObject.SetActive(false);
        GameAudio.SaveVolumeSettings();
        overlay.gameObject.SetActive(false);
        IsPaused = false;
        game.battleManager.IsPaused = savedBattlePause;
        Time.timeScale = savedTimeScale;
        if (pausedTweens != null)
            foreach (var tween in pausedTweens)
                if (tween.IsActive())
                    tween.Play();
        pausedTweens = null;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(selectionBeforePause != null && selectionBeforePause.activeInHierarchy ? selectionBeforePause : null);
    }

    // ポーズ中の各画面への移動。
    public void OpenHelp()
    {
        if (!IsPaused)
            Pause();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        help.OpenFromPause(overlay);
    }

    private void OpenSettings()
    {
        menu.gameObject.SetActive(false);
        settings.gameObject.SetActive(true);
        SelectFirst(settings);
    }

    private void CloseSettings()
    {
        GameAudio.SaveVolumeSettings();
        settings.gameObject.SetActive(false);
        menu.gameObject.SetActive(true);
        SelectFirst(menu);
    }

    public void ConfirmReturnToTitle()
    {
        if (!IsPaused)
            Pause();
        menu.gameObject.SetActive(false);
        confirmation.gameObject.SetActive(true);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(confirmation.GetComponentsInChildren<Button>()[1].gameObject);
    }

    private void CancelReturnToTitle()
    {
        confirmation.gameObject.SetActive(false);
        menu.gameObject.SetActive(true);
        SelectFirst(menu);
    }

    private void ReturnToTitle()
    {
        Resume();
        var battle = game.battleManager;
        battle.CancelBattle();
        if (battle.handUI != null)
        {
            battle.handUI.StopAllCoroutines();
            battle.handUI.ResetForNewBattle();
        }

        foreach (var pile in game.battleUIRoot.GetComponentsInChildren<CardPileViewer>(true))
            pile.Close();
        foreach (var detail in FindObjectsOfType<CardDetailOverlay>(true))
            detail.Close();
        game.ShowTitle();
    }

    // Escapeは現在開いている画面から順に戻る。
    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;
        if (!IsPaused)
        {
            Pause();
            return;
        }

        if (help.IsOpen)
        {
            help.Close();
            SelectFirst(menu);
            return;
        }

        if (settings.gameObject.activeSelf)
        {
            CloseSettings();
            return;
        }

        if (confirmation.gameObject.activeSelf)
        {
            CancelReturnToTitle();
            return;
        }

        Resume();
    }

    private void OnDisable()
    {
        Resume();
    }

    private void LateUpdate()
    {
        foreach (var hint in volumeHints)
        {
            bool active = hint.handle.gameObject.activeInHierarchy;
            bool hovered = active && RectTransformUtility.RectangleContainsScreenPoint(hint.handle, Input.mousePosition, null);
            if (hovered && Input.GetMouseButtonDown(0))
                hint.dragging = true;
            if (!active || !Input.GetMouseButton(0))
                hint.dragging = false;
            hint.tooltip.SetActive(active && (hovered || hint.dragging));
        }
    }

    private static void SelectFirst(Transform panel)
    {
        var first = panel.GetComponentInChildren<Selectable>();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
    }

    // 以下はUI部品の生成。設定値の反映は渡されたコールバックで行う。
    private void VolumeSlider(string title, Transform parent, float y, int initial, UnityAction<float> change)
    {
        Label(title, parent, new Vector2(-300, y + 34), new Vector2(180, 44), 28);
        var root = Rect(title + "Slider", parent, new Vector2(0, y - 20), new Vector2(720, 44));
        root.gameObject.AddComponent<Image>().color = new Color(.04f, .055f, .07f);
        var fillArea = Rect("FillArea", root, Vector2.zero, new Vector2(688, 10));
        var fill = Rect("Fill", fillArea, Vector2.zero, Vector2.zero);
        Stretch(fill);
        fill.gameObject.AddComponent<Image>().color = new Color(.82f, .65f, .34f);
        var handleArea = Rect("HandleArea", root, Vector2.zero, new Vector2(688, 44));
        var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(28, 44));
        var graphic = handle.gameObject.AddComponent<Image>();
        graphic.color = new Color(1, .86f, .52f);
        var tooltip = Panel("CurrentVolume", handle, new Vector2(76, 40));
        tooltip.anchoredPosition = new Vector2(0, 52);
        tooltip.GetComponent<BattleHudGraphic>().raycastTarget = false;
        var value = Label(initial.ToString(), tooltip, Vector2.zero, new Vector2(76, 40), 24);
        tooltip.gameObject.SetActive(false);
        volumeHints.Add(new VolumeHint{handle = handle, tooltip = tooltip.gameObject});
        var slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = graphic;
        slider.minValue = 0;
        slider.maxValue = 100;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(initial);
        slider.onValueChanged.AddListener(v =>
        {
            value.text = Mathf.RoundToInt(v).ToString();
            change(v);
        });
        Label("0", parent, new Vector2(-400, y - 20), new Vector2(60, 28), 18);
        Label("100", parent, new Vector2(400, y - 20), new Vector2(60, 28), 18);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static RectTransform Panel(string name, Transform parent, Vector2 size)
    {
        var panel = Rect(name, parent, Vector2.zero, size);
        var frame = panel.gameObject.AddComponent<BattleHudGraphic>();
        frame.color = new Color(.025f, .035f, .045f);
        frame.metal = new Color(.78f, .61f, .3f);
        return panel;
    }

    private static TMP_Text Label(string value, Transform parent, Vector2 position, Vector2 size, float fontSize, bool heading = false)
    {
        var text = Rect(value, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        GameScreenTextStyle.Apply(text, heading);
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        return text;
    }

    private static Button MakeButton(string label, Transform parent, Vector2 position, UnityAction action)
    {
        var root = Rect(label, parent, position, new Vector2(380, 76));
        root.gameObject.AddComponent<Image>();
        var button = root.gameObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        var text = Label(label, root, Vector2.zero, new Vector2(380, 76), 28);
        Stretch(text.rectTransform);
        ButtonDesign.Apply(button);
        return button;
    }
}
