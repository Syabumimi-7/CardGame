using UnityEngine;
using TMPro;

/// <summary>
/// 親（カード）のサイズを常に監視し、変化に合わせて
/// 子要素の位置（Pos X, Pos Y）とサイズ（Width, Height）、文字サイズを
/// パーセンテージ（比率）で連動・維持させるクラスです。
/// </summary>
[ExecuteAlways] // シーン画面でのサイズ変更にもリアルタイムで反映させます
public class CardResponsiveLayout : MonoBehaviour
{
    [Header("基準デザイン設定")]
    [Tooltip("デザイン作成時の親（カード）の幅(Width)")]
    [SerializeField] private float baseParentWidth = 200f;

    [Tooltip("デザイン作成時の親（カード）の高さ(Height)")]
    [SerializeField] private float baseParentHeight = 300f;

    [Tooltip("基準の幅のときに設定したいフォントサイズ")]
    [SerializeField] private float baseFontSize = 24f;

    // 基準時の子の状態を記憶する変数
    [SerializeField, HideInInspector] private Vector2 basePosition;
    [SerializeField, HideInInspector] private Vector2 baseSize;
    [SerializeField, HideInInspector] private bool isInitialized = false;

    private RectTransform rectTransform;
    private RectTransform parentRectTransform;
    private TextMeshProUGUI tmpText;

    // 前回の親のサイズを記憶して変化を検知するための変数
    private Vector2 lastParentSize;
    private bool updatingLayout;

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && !updatingLayout) UpdateLayout();
    }

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
        Canvas.preWillRenderCanvases += UpdateLayout;
        UpdateLayout();
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= UpdateLayout;
    }

    private void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        tmpText = GetComponent<TextMeshProUGUI>();

        if (transform.parent != null)
        {
            parentRectTransform = transform.parent.GetComponent<RectTransform>();
        }
    }

    private void Start()
    {
        // ゲーム開始時にまだ基準が保存されていなければ保存する
        if (!isInitialized) SaveBaseState();
    }

    // 毎フレーム呼ばれる処理で、親のサイズ変更を監視します
    private void Update()
    {
        if (parentRectTransform == null) return;

        // 現在の親のサイズを取得
        Vector2 currentParentSize = parentRectTransform.rect.size;

        // 前回のサイズと違っていたら（サイズが変更されたら）、レイアウトを更新する
        if (currentParentSize != lastParentSize)
        {
            UpdateLayout();
            lastParentSize = currentParentSize; // 新しいサイズを記憶
        }
    }

    /// <summary>
    /// 現在の配置状態を基準状態として記憶させます。
    /// インスペクターの歯車マークから実行します。
    /// </summary>
    [ContextMenu("現在の状態を基準として保存")]
    public void SaveBaseState()
    {
        if (rectTransform == null || parentRectTransform == null) Initialize();

        if (rectTransform != null && parentRectTransform != null)
        {
            // 現在の相対位置とサイズを記憶
            basePosition = rectTransform.anchoredPosition;
            baseSize = rectTransform.sizeDelta;

            if (parentRectTransform.rect.width > 0) baseParentWidth = parentRectTransform.rect.width;
            if (parentRectTransform.rect.height > 0) baseParentHeight = parentRectTransform.rect.height;

            if (tmpText != null) baseFontSize = tmpText.fontSize;

            lastParentSize = parentRectTransform.rect.size;
            isInitialized = true;
        }
    }

    /// <summary>
    /// 親のサイズ比率に合わせて位置・サイズ・フォントサイズを再計算します。
    /// </summary>
    public void UpdateLayout()
    {
        if (!isActiveAndEnabled) return;
        if (rectTransform == null || parentRectTransform == null) Initialize();
        if (!isInitialized) SaveBaseState();
        if (rectTransform == null || parentRectTransform == null) return;

        float currentParentWidth = parentRectTransform.rect.width;
        float currentParentHeight = parentRectTransform.rect.height;

        if (currentParentWidth <= 0 || currentParentHeight <= 0) return;

        // 親の拡大・縮小比率を計算
        float scaleX = currentParentWidth / baseParentWidth;
        float scaleY = currentParentHeight / baseParentHeight;

        updatingLayout = true;
        try
        {
            // 位置（Pos X, Pos Y）を比率に合わせて更新
            rectTransform.anchoredPosition = new Vector2(basePosition.x * scaleX, basePosition.y * scaleY);

            // サイズ（Width, Height）を比率に合わせて更新
            rectTransform.sizeDelta = new Vector2(baseSize.x * scaleX, baseSize.y * scaleY);

            // TextMeshProのフォントサイズも幅の比率に合わせて更新
            if (tmpText != null)
            {
                tmpText.fontSize = baseFontSize * scaleX;
            }
        }
        finally
        {
            updatingLayout = false;
        }
    }
}
