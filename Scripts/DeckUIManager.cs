using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// デッキのカードを画面に生成し、アニメーションさせるクラスです。
/// </summary>
public class DeckUIManager : MonoBehaviour
{
    [Header("UI設定")]
    public GameObject cardUIPrefab; // UIカードのプレハブ
    public Transform cardParent;    // カードを並べる親オブジェクト(HorizontalLayoutGroupなどをつけると便利です)

    private List<GameObject> spawnedCards = new List<GameObject>();
    private int displayVersion;

    /// <summary>
    /// デッキのカードを生成し、アニメーションで表示します。
    /// </summary>
    public void DisplayDeckWithAnimation(List<CardInstance> deck)
    {
        DisplayDeckWithAnimation(deck, null);
    }

    public void DisplayDeckWithAnimation(List<CardInstance> deck, Action onComplete)
    {
        EnsureInteractiveCanvas();
        // 以前のカードが残っていたら削除
        ClearCards();
        int version = ++displayVersion;
        RectTransform overviewRect = cardParent as RectTransform;
        if (overviewRect != null)
            overviewRect.anchorMin = new Vector2(overviewRect.anchorMin.x,
                Mathf.Max(overviewRect.anchorMin.y, 0.21f));
        GridLayoutGroup grid = cardParent.GetComponent<GridLayoutGroup>();
        if (grid != null) CardOverviewGrid.Configure(grid, deck.Count);

        CanvasGroup parentGroup = cardParent.GetComponent<CanvasGroup>();
        if (parentGroup == null) parentGroup = cardParent.gameObject.AddComponent<CanvasGroup>();
        parentGroup.alpha = 0f;
        parentGroup.interactable = false;
        parentGroup.blocksRaycasts = false;

        // 順番にアニメーションさせるためのSequenceを作成
        Sequence sequence = DOTween.Sequence();

        for (int i = 0; i < deck.Count; i++)
        {
            // 1. カードを生成
            GameObject cardObj = Instantiate(cardUIPrefab, cardParent);
            // 最初のCanvas更新より前に隠し、Prefab本来の630x880が一瞬映るのを防ぎます。
            cardObj.transform.localScale = Vector3.zero;
            spawnedCards.Add(cardObj);

            // 2. データを反映
            CardUIView view = cardObj.GetComponent<CardUIView>();
            if (view != null)
            {
                view.Setup(deck[i]);
                view.SetStandaloneDetailEnabled(true);
                if (view.cardImage != null) view.cardImage.raycastTarget = true;
                AddDetailClickArea(view);
            }

            CanvasGroup group = cardObj.GetComponent<CanvasGroup>();
            if (group == null) group = cardObj.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

        }

        // GridLayoutとカード内のレスポンシブ文字を、画面へ出す前に確定させます。
        Canvas.ForceUpdateCanvases();
        RectTransform parentRect = cardParent as RectTransform;
        if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        foreach (GameObject cardObj in spawnedCards)
            if (cardObj != null)
                foreach (CardResponsiveLayout layout in cardObj.GetComponentsInChildren<CardResponsiveLayout>(true))
                    layout.UpdateLayout();
        Canvas.ForceUpdateCanvases();

        parentGroup.alpha = 1f;
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            GameObject cardObj = spawnedCards[i];
            if (cardObj == null) continue;
            CanvasGroup group = cardObj.GetComponent<CanvasGroup>();
            AddEntryAnimation(sequence, cardObj.transform, group, i);
        }

        float completionDelay = deck.Count == 0 ? 0f : (deck.Count - 1) * 0.2f + 0.4f;
        StartCoroutine(CompleteDisplayAfterDelay(completionDelay, version, onComplete));
    }

    private static void AddDetailClickArea(CardUIView view)
    {
        GameObject clickArea = new GameObject("CardDetailClickArea", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        clickArea.layer = view.gameObject.layer;
        clickArea.transform.SetParent(view.transform, false);
        clickArea.transform.SetAsLastSibling();
        RectTransform rect = clickArea.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        Image image = clickArea.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.001f);
        image.raycastTarget = true;
        Button button = clickArea.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => CardDetailOverlay.Show(view));
        clickArea.AddComponent<DeckPreviewCardInput>().Setup(view);
    }

    private void EnsureInteractiveCanvas()
    {
        if (cardParent == null) return;
        Canvas canvas = cardParent.GetComponent<Canvas>();
        if (canvas == null) canvas = cardParent.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 50;
        if (cardParent.GetComponent<GraphicRaycaster>() == null)
            cardParent.gameObject.AddComponent<GraphicRaycaster>();
    }

    /// <summary>
    /// カードの出現アニメーション
    /// </summary>
    private void AddEntryAnimation(Sequence seq, Transform cardTransform, CanvasGroup group, int index)
    {
        // 初期状態：透明（大きさをゼロにする）
        cardTransform.localScale = Vector3.zero;

        // アニメーション設定：0.2秒ごとに順番に、少し弾むように（OutBack）大きくなる
        CardAcquisitionPresentation.AddCardEntry(seq, cardTransform, index)
            .OnComplete(() =>
            {
                if (group == null) return;
                group.interactable = true;
                group.blocksRaycasts = true;
            });
    }

    private IEnumerator CompleteDisplayAfterDelay(float delay, int version, Action onComplete)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (version != displayVersion) yield break;

        // Sequence内の子TweenのOnCompleteに依存せず、10枚すべての表示が終わった
        // この時点で必ずカード入力を有効化します。
        foreach (GameObject card in spawnedCards)
        {
            if (card == null) continue;
            CanvasGroup group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        if (cardParent != null)
        {
            CanvasGroup parentGroup = cardParent.GetComponent<CanvasGroup>();
            if (parentGroup != null)
            {
                parentGroup.interactable = true;
                parentGroup.blocksRaycasts = true;
            }
        }
        onComplete?.Invoke();
    }

    /// <summary>
    /// 画面に表示されているカードUIをすべて破棄（リセット）します。（新しく追加！）
    /// </summary>
    public void ClearCards()
    {
        displayVersion++;
        foreach (GameObject card in spawnedCards)
        {
            if (card != null)
            {
                Destroy(card); // オブジェクトを完全に削除します
            }
        }
        spawnedCards.Clear(); // 記憶していたリストも空にします
    }
}

/// <summary>一覧の領域と枚数から、カードを読みやすい大きさで配置します。</summary>
public class CardOverviewGrid : MonoBehaviour
{
    private GridLayoutGroup grid;
    private int cardCount;

    public static void Configure(GridLayoutGroup layout, int count)
    {
        CardOverviewGrid sizing = layout.GetComponent<CardOverviewGrid>();
        if (sizing == null) sizing = layout.gameObject.AddComponent<CardOverviewGrid>();
        sizing.grid = layout;
        sizing.cardCount = Mathf.Max(1, count);
        Canvas.ForceUpdateCanvases();
        sizing.Refresh();
    }

    private void LateUpdate() { Refresh(); }

    private void Refresh()
    {
        if (grid == null) return;
        Rect area = ((RectTransform)grid.transform).rect;
        float width = area.width - grid.padding.horizontal;
        float height = area.height - grid.padding.vertical;
        if (width <= 0f || height <= 0f) return;

        const float aspect = 1.4f;
        const float maxWidth = 240f;
        Vector2 spacing = new Vector2(22f, 18f);
        int bestColumns = Mathf.Min(5, cardCount);
        float bestWidth = 0f;
        for (int columns = bestColumns; columns <= cardCount; columns++)
        {
            int rows = Mathf.CeilToInt((float)cardCount / columns);
            float candidate = Mathf.Min(maxWidth,
                (width - spacing.x * (columns - 1)) / columns,
                (height - spacing.y * (rows - 1)) / rows / aspect);
            if (candidate <= bestWidth) continue;
            bestWidth = candidate;
            bestColumns = columns;
        }
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = bestColumns;
        grid.cellSize = new Vector2(bestWidth, bestWidth * aspect);
        grid.spacing = spacing;
        grid.childAlignment = TextAnchor.MiddleCenter;
    }
}

public class DeckPreviewCardInput : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CardUIView view;

    public void Setup(CardUIView cardView) { view = cardView; }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (view == null) return;
        view.transform.DOKill();
        view.transform.DOScale(1.06f, 0.12f).SetEase(Ease.OutQuad);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (view == null) return;
        view.transform.DOKill();
        view.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutQuad);
    }
}
