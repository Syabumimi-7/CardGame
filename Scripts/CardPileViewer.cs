using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>山札または墓地のカードをグリッドで表示します。</summary>
public class CardPileViewer : MonoBehaviour, IPointerClickHandler
{
    public enum PileType { DrawPile, DiscardPile }

    public BattleManager battleManager;
    public PileType pileType;
    public GameObject panel;
    public RectTransform content;
    public CardUIView cardPrefab;
    public TextMeshProUGUI titleText;
    public Button closeButton;

    [Header("カード一覧レイアウト")]
    [Tooltip("一覧内で表示するカード1枚の大きさ")]
    public Vector2 cardSize = new Vector2(220f, 330f);
    [Tooltip("カード同士の横・縦の間隔")]
    public Vector2 cardSpacing = new Vector2(20f, 20f);
    [Min(1)] public int columnCount = 5;

    private readonly List<GameObject> entries = new List<GameObject>();

    private void Awake()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        GridLayoutGroup grid = content != null ? content.GetComponent<GridLayoutGroup>() : null;
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columnCount;
            grid.cellSize = cardSize;
            grid.spacing = cardSpacing;
            grid.padding = new RectOffset(24, 24, 24, 24);
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        if (content != null)
        {
            // ScrollRect の表示領域外を確実に切り取り、カードが背景パネルから
            // はみ出して他のバトル UI と重ならないようにします。
            RectTransform viewport = content.parent as RectTransform;
            if (viewport != null)
            {
                // 既存シーンではViewportが1920x1080固定だったため、内側の黒い
                // ScrollViewより大きくなっていました。親いっぱいに合わせます。
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.anchoredPosition = Vector2.zero;
                viewport.sizeDelta = Vector2.zero;
                viewport.pivot = new Vector2(0.5f, 0.5f);
                if (viewport.GetComponent<RectMask2D>() == null)
                    viewport.gameObject.AddComponent<RectMask2D>();

                GameObject scrollObject = viewport.parent != null ? viewport.parent.gameObject : viewport.gameObject;
                ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
                if (scroll == null) scroll = scrollObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = content;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.inertia = true;
                scroll.scrollSensitivity = 45f;
            }

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, content.sizeDelta.y);
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        ConfigureButtonArea();
        if (titleText != null) GameScreenTextStyle.Apply(titleText, true);
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>Keep the back button outside the scroll hit area and above the card grid.</summary>
    public void ConfigureButtonArea()
    {
        if (closeButton == null) return;
        RectTransform buttonRect = (RectTransform)closeButton.transform;
        BottomScreenButton.Apply(closeButton);
        buttonRect.SetAsLastSibling();
        TextMeshProUGUI label = closeButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = "もどる";
        RectTransform scrollRect = content != null && content.parent != null ? content.parent.parent as RectTransform : null;
        if (scrollRect != null && scrollRect.parent == buttonRect.parent)
        {
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            Canvas canvas = closeButton.GetComponentInParent<Canvas>();
            RectTransform root = GameViewport.ContentFor(canvas);
            float bottom = 94f;
            if (root != null)
            {
                Vector3 footerTop = root.TransformPoint(new Vector3(0f,
                    root.rect.yMin + root.rect.height * BottomScreenButton.HeightAnchor + 54f, 0f));
                bottom = ((RectTransform)scrollRect.parent).InverseTransformPoint(footerTop).y
                    - ((RectTransform)scrollRect.parent).rect.yMin;
            }
            scrollRect.offsetMin = new Vector2(24f, bottom);
            scrollRect.offsetMax = new Vector2(-24f, -94f);
        }
        ButtonDesign.Apply(closeButton);
    }

    public void Open()
    {
        if (battleManager == null || panel == null || content == null) return;
        CloseEntries();

        IReadOnlyList<CardInstance> cards = pileType == PileType.DrawPile
            ? battleManager.DrawPile
            : battleManager.DiscardPile;
        if (titleText != null)
            titleText.text = pileType == PileType.DrawPile ? $"山札 ({cards.Count})" : $"墓地 ({cards.Count})";

        foreach (CardInstance card in cards)
        {
            CardUIView view = Instantiate(cardPrefab, content);
            RectTransform cardRect = view.GetComponent<RectTransform>();
            if (cardRect != null) cardRect.sizeDelta = cardSize;
            view.Setup(card);
            entries.Add(view.gameObject);
        }

        panel.SetActive(true);
        ConfigureButtonArea();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        content.anchoredPosition = Vector2.zero;
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        CloseEntries();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerPress == gameObject) Close();
    }

    private void CloseEntries()
    {
        foreach (GameObject entry in entries)
            if (entry != null) Destroy(entry);
        entries.Clear();
    }
}
