using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>手札の生成、選択、ドロー・捨て札アニメーションを管理します。</summary>
public class BattleHandUI : MonoBehaviour
{
    [Header("参照")]
    public BattleManager battleManager;
    public CardUIView cardPrefab;
    public RectTransform handParent;
    public RectTransform deckIcon;
    public RectTransform graveyardIcon;
    public Button endTurnButton;
    public TextMeshProUGUI warningText;

    [Header("手札サイズ")]
    [Tooltip("枚数にかかわらず維持するカード1枚の大きさ")]
    public Vector2 baseCardSize = new Vector2(225f, 337.5f);
    [Tooltip("カード同士の間隔")]
    [Min(0f)] public float cardSpacing = 16f;

    [Header("アニメーション")]
    public float selectedScale = 1.25f;
    [Min(0f)] public float selectedLift = 32f;
    public float selectDuration = 0.18f;
    public float drawDuration = 0.35f;
    public float drawInterval = 0.1f;
    public float discardDuration = 0.3f;

    private readonly List<CardUIView> views = new List<CardUIView>();
    private CardUIView selected;
    private float lastHandWidth;
    public bool IsAnimating { get; private set; }

    private void Update()
    {
        if (battleManager != null && battleManager.IsPaused) return;
        if (Input.GetMouseButtonDown(1)) DeselectCard();
    }

    public IEnumerator ShowDrawnCards(IReadOnlyList<CardInstance> cards)
    {
        IsAnimating = true;
        ClearImmediate();

        HorizontalLayoutGroup layout = handParent != null ? handParent.GetComponent<HorizontalLayoutGroup>() : null;
        var drawEntries = new List<(CardUIView view, Vector3 destination)>();

        foreach (CardInstance card in cards)
        {
            CardUIView view = Instantiate(cardPrefab, handParent);
            RectTransform cardRect = view.GetComponent<RectTransform>();
            if (cardRect != null) cardRect.sizeDelta = baseCardSize;
            view.transform.localScale = Vector3.one;

            views.Add(view);
            view.Setup(card, this);
            UIHoverScale hover = view.GetComponent<UIHoverScale>();
            if (hover != null) hover.enabled = false;
        }

        PrepareDrawLayout();
        // LayoutGroupに最終位置を計算させてから止め、Tweenとの位置競合を防ぎます。
        foreach (CardUIView view in views)
        {
            // Setup時点ではLayoutGroupによる配置前なので、ここで選択演出の基準位置を更新します。
            view.SetBaseScale(view.transform.localScale);
            drawEntries.Add((view, view.transform.position));
        }
        if (layout != null) layout.enabled = false;

        // 順番待ちのカードも最初のyieldより前に隠します。
        foreach (var entry in drawEntries)
        {
            entry.view.transform.position = deckIcon != null ? deckIcon.position : entry.destination;
            entry.view.transform.localScale = Vector3.zero;
        }

        foreach (var entry in drawEntries)
        {
            CardUIView view = entry.view;
            view.transform.position = deckIcon != null ? deckIcon.position : entry.destination;
            view.transform.localScale = Vector3.zero;
            view.transform.DOMove(entry.destination, drawDuration).SetEase(Ease.OutCubic);
            view.transform.DOScale(Vector3.one, drawDuration).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(drawInterval);
        }

        yield return new WaitForSeconds(drawDuration);
        if (layout != null)
        {
            layout.enabled = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(handParent);
        }

        IsAnimating = false;
        RefreshHandLayout();
        foreach (CardUIView view in views)
        {
            UIHoverScale hover = view.GetComponent<UIHoverScale>();
            if (hover != null) hover.enabled = true;
        }
    }

    public IEnumerator ShowAdditionalCards(IReadOnlyList<CardInstance> cards)
    {
        IsAnimating = true;
        var added = new List<CardUIView>();
        foreach (var card in cards)
        {
            if (views.Exists(view => view.Card == card)) continue;
            var view = Instantiate(cardPrefab, handParent);
            var rect = view.GetComponent<RectTransform>();
            if (rect != null) rect.sizeDelta = baseCardSize;
            views.Add(view); view.Setup(card, this); added.Add(view);
            var hover = view.GetComponent<UIHoverScale>(); if (hover != null) hover.enabled = false;
        }
        PrepareDrawLayout();
        var layout = handParent != null ? handParent.GetComponent<HorizontalLayoutGroup>() : null;
        var entries = new List<(CardUIView view, Vector3 destination)>();
        foreach (var view in added) { view.SetBaseScale(view.transform.localScale); entries.Add((view, view.transform.position)); }
        if (layout != null) layout.enabled = false;
        foreach (var entry in entries)
        {
            entry.view.transform.position = deckIcon != null ? deckIcon.position : entry.destination;
            entry.view.transform.localScale = Vector3.zero;
        }
        foreach (var entry in entries)
        {
            entry.view.transform.DOMove(entry.destination, drawDuration).SetEase(Ease.OutCubic);
            entry.view.transform.DOScale(Vector3.one, drawDuration).SetEase(Ease.OutBack);
            yield return new WaitForSeconds(drawInterval);
        }
        yield return new WaitForSeconds(drawDuration);
        if (layout != null) { layout.enabled = true; LayoutRebuilder.ForceRebuildLayoutImmediate(handParent); }
        foreach (var view in added) { var hover = view.GetComponent<UIHoverScale>(); if (hover != null) hover.enabled = true; }
        IsAnimating = false;
    }

    public void OnCardClicked(CardUIView view)
    {
        if (view == null || !views.Contains(view) || IsAnimating || battleManager == null || !battleManager.CanPlayerAct) return;

        if (!battleManager.TryPlayCard(view.Card))
        {
            view.Shake();
            ShowWarning("コストが足りません！");
        }
    }

    public void NotifyCardPlayed(CardInstance card)
    {
        CardUIView view = views.Find(item => item.Card == card);
        if (view == null) return;

        selected = null;
        views.Remove(view);
        // 1枚の捨て札演出で、残りのカードやターン終了操作をロックしません。
        LayoutElement layoutElement = view.GetComponent<LayoutElement>();
        if (layoutElement == null) layoutElement = view.gameObject.AddComponent<LayoutElement>();
        layoutElement.ignoreLayout = true;

        CanvasGroup canvasGroup = view.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = view.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // PointerExitによるhover TweenのDOKillが、捨て札Tweenを止めないようにします。
        UIHoverScale hoverScale = view.GetComponent<UIHoverScale>();
        if (hoverScale != null) hoverScale.enabled = false;

        // 手札のLayoutGroupから完全に切り離し、使用済みカードが手札に残るのを防ぎます。
        Transform animationParent = handParent != null ? handParent.parent : null;
        if (animationParent != null) view.transform.SetParent(animationParent, true);
        if (handParent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(handParent);

        Vector3 destination = graveyardIcon != null ? graveyardIcon.position : view.transform.position;
        view.transform.DOKill();
        Sequence discardSequence = DOTween.Sequence();
        discardSequence.Join(view.transform.DOMove(destination, discardDuration).SetEase(Ease.InCubic));
        discardSequence.Join(view.transform.DOScale(Vector3.zero, discardDuration));
        discardSequence.OnComplete(() => { if (view != null)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            } });

        // Tweenが外部から停止されても、UIが残り続けないよう破棄を保証します。
        Destroy(view.gameObject, discardDuration + 0.1f);
        RefreshHandLayout();
    }

    public IEnumerator DiscardAll()
    {
        IsAnimating = true;
        DeselectCard();
        foreach (CardUIView view in new List<CardUIView>(views))
        {
            if (view == null) continue;
            Vector3 destination = graveyardIcon != null ? graveyardIcon.position : view.transform.position;
            view.transform.DOMove(destination, discardDuration).SetEase(Ease.InCubic);
            view.transform.DOScale(Vector3.zero, discardDuration).OnComplete(() => Destroy(view.gameObject));
            yield return new WaitForSeconds(0.05f);
        }

        views.Clear();
        yield return new WaitForSeconds(discardDuration);
        IsAnimating = false;
    }

    public void DeselectCard()
    {
        if (selected != null) selected.SetSelected(false, selectedScale, selectDuration, selectedLift);
        selected = null;
    }

    public void OnEmptyAreaClicked() { DeselectCard(); }
    public void SetInteractable(bool value) { if (endTurnButton != null) endTurnButton.interactable = value; }
    public void RefreshCards()
    {
        foreach (CardUIView view in views)
            if (view != null) view.Refresh();
    }

    public void ResetForNewBattle()
    {
        IsAnimating = false;
        DeselectCard();
        ClearImmediate();
        if (handParent == null) return;
        for (int i = handParent.childCount - 1; i >= 0; i--)
        {
            Transform child = handParent.GetChild(i);
            child.DOKill();
            Destroy(child.gameObject);
        }
        HorizontalLayoutGroup layout = handParent.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = true;
        LayoutRebuilder.ForceRebuildLayoutImmediate(handParent);
    }

    private void ShowWarning(string message)
    {
        if (warningText == null) return;
        warningText.DOKill();
        warningText.text = message;
        warningText.alpha = 1f;
        warningText.DOFade(0f, 1.2f).SetDelay(0.6f);
    }

    private void ClearImmediate()
    {
        foreach (CardUIView view in views)
            if (view != null)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
        views.Clear();
        selected = null;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (Application.isPlaying) RefreshHandLayout();
    }

    private void LateUpdate()
    {
        if (!IsAnimating && handParent != null && !Mathf.Approximately(lastHandWidth, handParent.rect.width))
            RefreshHandLayout();
    }

    private void RefreshHandLayout()
    {
        foreach (CardUIView view in views)
        {
            if (view == null) continue;
            RectTransform cardRect = view.GetComponent<RectTransform>();
            if (cardRect != null) cardRect.sizeDelta = baseCardSize;
            view.SetBaseScale(Vector3.one);
        }

        HorizontalLayoutGroup layout = handParent != null ? handParent.GetComponent<HorizontalLayoutGroup>() : null;
        if (layout != null)
        {
            // Negative spacing overlaps fixed-size cards instead of shrinking them.
            float available = Mathf.Max(baseCardSize.x, handParent.rect.width - layout.padding.horizontal);
            layout.spacing = views.Count <= 1 ? cardSpacing : Mathf.Min(cardSpacing,
                (available - baseCardSize.x * views.Count) / (views.Count - 1));
        }
        if (handParent != null) lastHandWidth = handParent.rect.width;
    }

    private void PrepareDrawLayout()
    {
        if (handParent == null) return;
        var layout = handParent.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = true;
        RefreshHandLayout();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(handParent);
    }
}
