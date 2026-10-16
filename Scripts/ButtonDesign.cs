using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared, time-scale independent button presentation. Button owns all click actions.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Button))]
public class ButtonDesign : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
{
    [SerializeField]
    ButtonDesignGraphic surface;
    [SerializeField]
    TMP_Text[] labels;
    Button button;
    bool hovered, held, selected;
    float submitUntil;
    public ButtonDesignGraphic Surface => surface;
    // シーン内のボタンへ共通デザインを適用する。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
    }

    static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyScene(scene);
    }

    public static void ApplyScene(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var target in root.GetComponentsInChildren<Button>(true))
                if (ShouldStyle(target))
                    Apply(target);
    }

    public static bool ShouldStyle(Button target)
    {
        if (target.GetComponent<PackSelectionItem>() != null)
            return false;
        if (target.GetComponentInParent<CardUIView>(true) != null)
            return false;
        // These are invisible dismiss/selection hit areas, not visible UI controls.
        if (target.name == "Deselect" || target.name == "SentakukaijoButton" || target.name == "OutsideClickArea")
            return false;
        if (target.transform.parent != null && target.transform.parent.name == "MenuPanel" && (target.name == "StartButton" || target.name == "HelpButton" || target.name == "CollectionButton" || target.name == "CreditsButton"))
            return false;
        return true;
    }

    public static ButtonDesign Apply(Button target)
    {
        if (!ShouldStyle(target))
            return null;
        var design = target.GetComponent<ButtonDesign>();
        if (design == null)
            design = target.gameObject.AddComponent<ButtonDesign>();
        design.Configure();
        return design;
    }

    void Awake()
    {
        button = GetComponent<Button>();
    }

    void OnEnable()
    {
        hovered = held = selected = false;
        submitUntil = 0;
        UpdateState(true);
    }

    // 既存のクリック処理を保持したまま、外観とラベルだけを設定する。
    void Configure()
    {
        button = GetComponent<Button>();
        labels = GetComponentsInChildren<TMP_Text>(true).Where(t => t.GetComponentInParent<Button>(true) == button).ToArray();
        var oldFrame = GetComponent<BattleHudGraphic>();
        bool pileSymbol = oldFrame != null && oldFrame.shape == BattleHudGraphic.Shape.CardBack;
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == surface || graphic.GetComponentInParent<Button>(true) != button || graphic is TMP_Text || graphic is Text)
                continue;
            // Keep existing pile emblems and pause icons; replace their surrounding frame only.
            if (graphic is BattleHudGraphic hud && (hud.shape == BattleHudGraphic.Shape.Gear || hud.shape == BattleHudGraphic.Shape.Gem || (hud.shape == BattleHudGraphic.Shape.CardBack && graphic.gameObject != gameObject)))
            {
                hud.raycastTarget = false;
                continue;
            }

            graphic.enabled = false;
        }

        if (surface == null)
        {
            var go = new GameObject("ButtonDesign", typeof(RectTransform), typeof(CanvasRenderer), typeof(ButtonDesignGraphic));
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            surface = go.GetComponent<ButtonDesignGraphic>();
        }

        surface.raycastTarget = true;
        var rect = surface.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        button.targetGraphic = surface;
        button.transition = Selectable.Transition.None;
        if (pileSymbol && transform.Find("PileSymbol") == null)
        {
            var symbolObject = new GameObject("PileSymbol", typeof(RectTransform), typeof(CanvasRenderer), typeof(BattleHudGraphic));
            symbolObject.layer = gameObject.layer;
            symbolObject.transform.SetParent(transform, false);
            var sr = symbolObject.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(.25f, .47f);
            sr.anchorMax = new Vector2(.75f, .88f);
            sr.offsetMin = sr.offsetMax = Vector2.zero;
            var symbol = symbolObject.GetComponent<BattleHudGraphic>();
            symbol.shape = BattleHudGraphic.Shape.CardBack;
            symbol.color = new Color(.06f, .07f, .075f);
            symbol.metal = new Color(.83f, .70f, .51f);
            symbol.ornament = false;
            symbol.raycastTarget = false;
        }

        var theme = ButtonDesignTheme.Current;
        float height = ((RectTransform)transform).rect.height;
        foreach (var text in labels)
        {
            text.raycastTarget = false;
            if (theme != null)
            {
                text.font = theme.font;
                text.fontSharedMaterial = theme.textMaterial;
            }

            text.color = new Color(1, .96f, .86f);
            text.fontStyle = FontStyles.Bold;
            text.enableAutoSizing = true;
            text.fontSizeMin = 10;
            text.fontSizeMax = Mathf.Clamp(height * .40f, 14, 36);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (labels.Length == 1)
            {
                var tr = text.rectTransform;
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(Mathf.Min(height * .28f, 28), height * .13f);
                tr.offsetMax = -tr.offsetMin;
                text.alignment = TextAlignmentOptions.Center;
            }
        }

        // Existing press/blink components are visual-only. Keep all click listeners intact.
        var press = GetComponent<ButtonPressAnimator>();
        if (press != null)
            press.enabled = false;
        var blink = GetComponent<ButtonBlinkAnimator>();
        if (blink != null)
            blink.enabled = false;
        var scale = GetComponent<UIHoverScale>();
        if (scale != null)
            scale.enabled = false;
        UpdateState(true);
    }

    void LateUpdate()
    {
        UpdateState(false);
    }

    // ポーズ中も動くよう、外観の補間にはスケールされない時間を使う。
    void UpdateState(bool immediate)
    {
        if (button == null)
            button = GetComponent<Button>();
        if (surface == null)
            return;
        bool usable = button.enabled && button.IsInteractable();
        float targetHover = usable && (hovered || selected) ? 1 : 0,
            targetPressed = usable && (held || Time.unscaledTime < submitUntil) ? 1 : 0,
            targetDisabled = usable ? 0 : 1;
        float transitionStep = immediate ? 1 : Time.unscaledDeltaTime / 0.09f;
        float nextHover = Mathf.MoveTowards(surface.hover, targetHover, transitionStep),
            nextPressed = Mathf.MoveTowards(surface.pressed, targetPressed, transitionStep),
            nextDisabled = Mathf.MoveTowards(surface.disabled, targetDisabled, transitionStep);
        if (!Mathf.Approximately(surface.hover, nextHover) || !Mathf.Approximately(surface.pressed, nextPressed) || !Mathf.Approximately(surface.disabled, nextDisabled))
        {
            surface.hover = nextHover;
            surface.pressed = nextPressed;
            surface.disabled = nextDisabled;
            surface.SetVerticesDirty();
        }

        Color textColor = Color.Lerp(new Color(1, .96f, .86f), new Color(.57f, .56f, .53f), nextDisabled);
        textColor = Color.Lerp(textColor, textColor * .82f, nextPressed);
        textColor.a = 1;
        if (labels != null)
            foreach (var text in labels)
                if (text != null)
                    text.color = textColor;
        if (!usable)
        {
            held = false;
            submitUntil = 0;
        }
    }

    void OnDisable()
    {
        hovered = held = selected = false;
        submitUntil = 0;
        if (surface != null)
        {
            surface.hover = surface.pressed = 0;
            surface.SetVerticesDirty();
        }
    }

    // 入力イベントは外観用の状態だけを更新する。実際の操作はButtonに任せる。
    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        selected = false;
    }

    public void OnPointerExit(PointerEventData e)
    {
        hovered = false;
        held = false;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left)
        {
            held = true;
            selected = false;
        }
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left)
            held = false;
    }

    public void OnSelect(BaseEventData e)
    {
        selected = true;
    }

    public void OnDeselect(BaseEventData e)
    {
        selected = false;
        held = false;
    }

    public void OnSubmit(BaseEventData e)
    {
        if (button != null && button.IsInteractable())
            submitUntil = Time.unscaledTime + .12f;
    }
}
