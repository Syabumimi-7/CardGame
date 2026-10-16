using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening; // DOTweenを使用

public class PanelHoverController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // --- 4種類に増えたアニメーションのリスト ---
    public enum AnimationType
    {
        None,       // アニメーションなし（即時）
        PopScale,   // ボワンと拡大・縮小
        SlideLeft,  // 左へスライド
        Fade        // 透明度が変わる（フェードイン・フェードアウト）
    }

    [System.Serializable]
    public class PanelSetting
    {
        [Tooltip("対象となるパネルUI")]
        public GameObject panel;

        [Tooltip("【表示】される時のアニメーション")]
        public AnimationType showAnimation = AnimationType.PopScale;

        [Tooltip("【非表示】になる時のアニメーション")]
        public AnimationType hideAnimation = AnimationType.Fade;

        // 内部で元の状態を記憶・操作するための変数
        [HideInInspector] public Vector2 defaultAnchoredPosition;
        [HideInInspector] public Vector3 defaultScale;
        [HideInInspector] public RectTransform rectTransform;
        [HideInInspector] public CanvasGroup canvasGroup; // フェード用に新しく追加
        [HideInInspector] public bool isInitialized = false;
    }

    [Header("ホバー時に表示させたいパネルのリスト")]
    public PanelSetting[] panelsToShow;

    [Header("ホバー時に非表示にしたいパネルのリスト")]
    public PanelSetting[] panelsToHide;

    [Header("アニメーション共通設定")]
    [Tooltip("アニメーションにかかる時間（秒）")]
    public float duration = 0.3f;

    [Tooltip("「左にスライド」の移動距離（ピクセル単位）")]
    public float slideOffset = 300f;

    private void Awake()
    {
        InitializePanels(panelsToShow);
        InitializePanels(panelsToHide);
    }

    public void ReinitializePanels()
    {
        if (panelsToShow != null)
            foreach (var setting in panelsToShow) setting.isInitialized = false;
        if (panelsToHide != null)
            foreach (var setting in panelsToHide) setting.isInitialized = false;
        InitializePanels(panelsToShow);
        InitializePanels(panelsToHide);
        ResetHoverState();
    }

    private void OnEnable() { ResetHoverState(); }
    private void OnDisable() { ResetHoverState(); }

    private void ResetHoverState()
    {
        ResetPanels(panelsToShow, false);
        ResetPanels(panelsToHide, true);
    }

    private void ResetPanels(PanelSetting[] settings, bool active)
    {
        if (settings == null) return;
        foreach (var setting in settings)
        {
            if (setting.panel == null || !setting.isInitialized) continue;
            setting.panel.transform.DOKill();
            if (setting.canvasGroup != null) setting.canvasGroup.DOKill();
            ResetToDefaultState(setting);
            setting.panel.SetActive(active);
        }
    }

    // パネルの情報を初期化・記憶する処理
    private void InitializePanels(PanelSetting[] settings)
    {
        if (settings == null) return;
        foreach (var setting in settings)
        {
            if (setting.panel != null && !setting.isInitialized)
            {
                setting.rectTransform = setting.panel.GetComponent<RectTransform>();
                if (setting.rectTransform != null)
                {
                    setting.defaultAnchoredPosition = setting.rectTransform.anchoredPosition;
                }
                setting.defaultScale = setting.panel.transform.localScale;

                // フェードのために CanvasGroup を取得（なければ自動で追加）
                setting.canvasGroup = setting.panel.GetComponent<CanvasGroup>();
                if (setting.canvasGroup == null)
                {
                    setting.canvasGroup = setting.panel.AddComponent<CanvasGroup>();
                }

                setting.isInitialized = true;
            }
        }
    }

    // マウスカーソルが乗った時
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled) return;
        foreach (var setting in panelsToShow) ShowPanel(setting);
        foreach (var setting in panelsToHide) HidePanel(setting);
    }

    // マウスカーソルが離れた時
    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isActiveAndEnabled) return;
        foreach (var setting in panelsToShow) HidePanel(setting);
        foreach (var setting in panelsToHide) ShowPanel(setting);
    }

    // --- 安全にアニメーションを切り替えるためのリセット処理 ---
    private void ResetToDefaultState(PanelSetting setting)
    {
        // 異なるアニメーションが混ざっても崩れないよう、一度基本状態に戻します
        setting.panel.transform.localScale = setting.defaultScale;
        if (setting.rectTransform != null)
        {
            setting.rectTransform.anchoredPosition = setting.defaultAnchoredPosition;
        }
        if (setting.canvasGroup != null)
        {
            setting.canvasGroup.alpha = 1f; // 透明度を元に戻す
        }
    }

    // --- パネルを表示する処理 ---
    private void ShowPanel(PanelSetting setting)
    {
        if (setting.panel == null) return;

        // 実行中のアニメーションをすべて停止
        setting.panel.transform.DOKill();
        if (setting.rectTransform != null) setting.rectTransform.DOKill();
        if (setting.canvasGroup != null) setting.canvasGroup.DOKill();

        setting.panel.SetActive(true);
        ResetToDefaultState(setting); // 一度基本状態に戻す

        // 設定された表示アニメーションを実行
        switch (setting.showAnimation)
        {
            case AnimationType.None:
                break; // リセットされているのでそのまま表示

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
                    setting.canvasGroup.alpha = 0f; // 透明からスタート
                    setting.canvasGroup.DOFade(1f, duration).SetEase(Ease.OutQuad); // 1(不透明)へフェード
                }
                break;
        }
    }

    // --- パネルを非表示にする処理 ---
    private void HidePanel(PanelSetting setting)
    {
        if (setting.panel == null) return;

        // 実行中のアニメーションをすべて停止
        setting.panel.transform.DOKill();
        if (setting.rectTransform != null) setting.rectTransform.DOKill();
        if (setting.canvasGroup != null) setting.canvasGroup.DOKill();

        // 設定された非表示アニメーションを実行
        switch (setting.hideAnimation)
        {
            case AnimationType.None:
                setting.panel.SetActive(false);
                break;

            case AnimationType.PopScale:
                setting.panel.transform.DOScale(Vector3.zero, duration)
                    .SetEase(Ease.InBack)
                    .OnComplete(() => setting.panel.SetActive(false));
                break;

            case AnimationType.SlideLeft:
                if (setting.rectTransform != null)
                {
                    Vector2 targetPos = setting.defaultAnchoredPosition + new Vector2(-slideOffset, 0);
                    setting.rectTransform.DOAnchorPos(targetPos, duration)
                        .SetEase(Ease.InCubic)
                        .OnComplete(() => setting.panel.SetActive(false));
                }
                else
                {
                    setting.panel.SetActive(false);
                }
                break;

            case AnimationType.Fade:
                if (setting.canvasGroup != null)
                {
                    // 0(透明)へフェードし、完了したらSetActiveをfalseにする
                    setting.canvasGroup.DOFade(0f, duration)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(() => setting.panel.SetActive(false));
                }
                else
                {
                    setting.panel.SetActive(false);
                }
                break;
        }
    }
}
