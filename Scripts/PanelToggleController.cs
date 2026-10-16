using UnityEngine;
using DG.Tweening;

public class PanelToggleController : MonoBehaviour
{
    private Tween pendingAction;
    public enum AnimationType
    {
        None,
        PopScale,
        SlideLeft,
        Fade
    }

    [System.Serializable]
    public class ShowPanelSetting
    {
        [Tooltip("表示したいパネルUI")]
        public GameObject panel;
        [Tooltip("表示する時のアニメーション")]
        public AnimationType animationType = AnimationType.PopScale;

        [HideInInspector] public Vector2 defaultAnchoredPosition;
        [HideInInspector] public Vector3 defaultScale;
        [HideInInspector] public RectTransform rectTransform;
        [HideInInspector] public CanvasGroup canvasGroup;
        [HideInInspector] public bool isInitialized = false;
    }

    [System.Serializable]
    public class HidePanelSetting
    {
        [Tooltip("非表示にしたいパネルUI")]
        public GameObject panel;
        [Tooltip("非表示にする時のアニメーション")]
        public AnimationType animationType = AnimationType.Fade;

        [HideInInspector] public Vector2 defaultAnchoredPosition;
        [HideInInspector] public Vector3 defaultScale;
        [HideInInspector] public RectTransform rectTransform;
        [HideInInspector] public CanvasGroup canvasGroup;
        [HideInInspector] public bool isInitialized = false;
    }

    [Header("クリック時に表示させたいパネルのリスト")]
    public ShowPanelSetting[] panelsToShow;

    [Header("クリック時に非表示にしたいパネルのリスト")]
    public HidePanelSetting[] panelsToHide;

    [Header("タイミング設定")]
    [Tooltip("ボタンを押してから、パネルが切り替わるまでの待機時間（秒）")]
    public float delayBeforeAction = 0.35f; // 点滅アニメーションを待つための時間

    [Header("アニメーション共通設定")]
    [Tooltip("アニメーションにかかる時間（秒）")]
    public float duration = 0.3f;
    [Tooltip("「左にスライド」の移動距離（ピクセル単位）")]
    public float slideOffset = 300f;

    private void Awake()
    {
        foreach (var setting in panelsToShow) InitializePanelData(setting.panel, ref setting.defaultAnchoredPosition, ref setting.defaultScale, out setting.rectTransform, out setting.canvasGroup, ref setting.isInitialized);
        foreach (var setting in panelsToHide) InitializePanelData(setting.panel, ref setting.defaultAnchoredPosition, ref setting.defaultScale, out setting.rectTransform, out setting.canvasGroup, ref setting.isInitialized);
    }

    private void InitializePanelData(GameObject panel, ref Vector2 defaultAnchoredPosition, ref Vector3 defaultScale, out RectTransform rectTransform, out CanvasGroup canvasGroup, ref bool isInitialized)
    {
        rectTransform = null;
        canvasGroup = null;

        if (panel != null && !isInitialized)
        {
            rectTransform = panel.GetComponent<RectTransform>();
            if (rectTransform != null) defaultAnchoredPosition = rectTransform.anchoredPosition;
            
            defaultScale = panel.transform.localScale;

            canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = panel.AddComponent<CanvasGroup>();

            isInitialized = true;
        }
    }

    // --- ボタンがクリックされた時の処理 ---
    public void OnButtonClick()
    {
        pendingAction?.Kill();
        if (delayBeforeAction > 0f)
        {
            // 待機時間が設定されている場合、指定時間(delayBeforeAction)待ってから ExecutePanels を呼ぶ
            pendingAction = DOVirtual.DelayedCall(delayBeforeAction, ExecutePanels);
        }
        else
        {
            // 待機時間が0の場合はすぐに実行
            ExecutePanels();
        }
    }

    public void CancelPendingActions()
    {
        pendingAction?.Kill();
        pendingAction = null;
    }

    // --- 実際にパネルの表示・非表示を行う処理 ---
    private void ExecutePanels()
    {
        foreach (var setting in panelsToShow) ShowPanel(setting);
        foreach (var setting in panelsToHide) HidePanel(setting);
    }

    private void ShowPanel(ShowPanelSetting setting)
    {
        if (setting.panel == null) return;

        KillTweens(setting.panel, setting.rectTransform, setting.canvasGroup);
        setting.panel.SetActive(true);
        ResetToDefault(setting.panel, setting.rectTransform, setting.canvasGroup, setting.defaultAnchoredPosition, setting.defaultScale);

        switch (setting.animationType)
        {
            case AnimationType.None:
                break;
            case AnimationType.PopScale:
                setting.panel.transform.localScale = Vector3.zero;
                setting.panel.transform.DOScale(setting.defaultScale, duration).SetEase(Ease.OutBack);
                break;
            case AnimationType.SlideLeft:
                if (setting.rectTransform != null)
                {
                    setting.rectTransform.anchoredPosition = setting.defaultAnchoredPosition + new Vector2(slideOffset, 0);
                    setting.rectTransform.DOAnchorPos(setting.defaultAnchoredPosition, duration).SetEase(Ease.OutCubic);
                }
                break;
            case AnimationType.Fade:
                if (setting.canvasGroup != null)
                {
                    setting.canvasGroup.alpha = 0f;
                    setting.canvasGroup.DOFade(1f, duration).SetEase(Ease.OutQuad);
                }
                break;
        }
    }

    private void HidePanel(HidePanelSetting setting)
    {
        if (setting.panel == null) return;

        KillTweens(setting.panel, setting.rectTransform, setting.canvasGroup);

        switch (setting.animationType)
        {
            case AnimationType.None:
                setting.panel.SetActive(false);
                break;
            case AnimationType.PopScale:
                setting.panel.transform.DOScale(Vector3.zero, duration).SetEase(Ease.InBack).OnComplete(() => setting.panel.SetActive(false));
                break;
            case AnimationType.SlideLeft:
                if (setting.rectTransform != null)
                {
                    Vector2 targetPos = setting.defaultAnchoredPosition + new Vector2(-slideOffset, 0);
                    setting.rectTransform.DOAnchorPos(targetPos, duration).SetEase(Ease.InCubic).OnComplete(() => setting.panel.SetActive(false));
                }
                else setting.panel.SetActive(false);
                break;
            case AnimationType.Fade:
                if (setting.canvasGroup != null)
                {
                    setting.canvasGroup.DOFade(0f, duration).SetEase(Ease.OutQuad).OnComplete(() => setting.panel.SetActive(false));
                }
                else setting.panel.SetActive(false);
                break;
        }
    }

    private void KillTweens(GameObject panel, RectTransform rect, CanvasGroup cg)
    {
        panel.transform.DOKill();
        if (rect != null) rect.DOKill();
        if (cg != null) cg.DOKill();
    }

    private void ResetToDefault(GameObject panel, RectTransform rect, CanvasGroup cg, Vector2 defaultPos, Vector3 defaultScale)
    {
        panel.transform.localScale = defaultScale;
        if (rect != null) rect.anchoredPosition = defaultPos;
        if (cg != null) cg.alpha = 1f;
    }
}
