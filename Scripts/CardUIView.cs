using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CardUIView : MonoBehaviour, IPointerClickHandler
{
    public TextMeshProUGUI cardNameText;
    public Image cardImage;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI descriptionText;
    [HideInInspector] public CardInstance Card { get; private set; }
    [HideInInspector] public BattleHandUI Owner { get; private set; }
    private Vector3 baseScale;
    private Vector2 baseAnchoredPosition;
    private bool initialized;
    private bool standaloneSelected;
    private bool standaloneDetailEnabled = true;

    private void Awake() { ApplyDesign(); }

    public void ApplyDesign()
    {
        CardDesignLayout design = GetComponent<CardDesignLayout>();
        if (design == null) design = gameObject.AddComponent<CardDesignLayout>();
        design.Apply(this);
    }

    public void Setup(CardInstance card, BattleHandUI owner = null)
    {
        Card = card;
        Owner = owner;
        if (!initialized)
        {
            baseScale = transform.localScale;
            RectTransform rect = transform as RectTransform;
            if (rect != null) baseAnchoredPosition = rect.anchoredPosition;
            initialized = true;
        }
        Refresh();
    }

    public void Refresh()
    {
        ApplyDesign();
        if (Card == null || Card.Data == null) return;
        if (cardNameText != null) cardNameText.text = Card.Data.cardName;
        if (costText != null) costText.text = Card.IsPerformanceDetermined ? Card.EffectiveCost.ToString() : "?";
        if (descriptionText != null) descriptionText.text = Card.GetDescription();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (Owner != null) Owner.DeselectCard();
            else { standaloneSelected = false; SetSelected(false, 1.25f, 0.18f); }
            return;
        }
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (Owner != null) Owner.OnCardClicked(this);
        else if (standaloneDetailEnabled) CardDetailOverlay.Show(this);
    }

    public void SetSelected(bool selected, float scale, float duration, float verticalOffset = 0f)
    {
        standaloneSelected = selected;
        UIHoverScale hover = GetComponent<UIHoverScale>();
        if (hover != null) hover.enabled = false;
        transform.DOKill();
        Sequence sequence = DOTween.Sequence().SetTarget(transform);
        sequence.Join(transform.DOScale(selected ? baseScale * scale : baseScale, duration)
            .SetEase(Ease.OutBack)
        );
        RectTransform rect = transform as RectTransform;
        // 一覧の位置はLayoutGroupが管理するため、手札だけを移動させます。
        if (rect != null && Owner != null)
            sequence.Join(rect.DOAnchorPos(selected ? baseAnchoredPosition + Vector2.up * verticalOffset : baseAnchoredPosition, duration).SetEase(Ease.OutCubic));
        sequence.OnComplete(() =>
            {
                if (!selected && hover != null) hover.enabled = true;
            });
    }


    public void SetBaseScale(Vector3 scale)
    {
        baseScale = scale;
        UIHoverScale hover = GetComponent<UIHoverScale>();
        if (hover != null) hover.SetNormalScale(scale);
        RectTransform rect = transform as RectTransform;
        if (rect != null) baseAnchoredPosition = rect.anchoredPosition;
        initialized = true;
        if (!standaloneSelected) transform.localScale = baseScale;
    }

    public void ResetSelection()
    {
        standaloneSelected = false;
        SetSelected(false, 1f, 0.18f);
    }

    public void SetStandaloneDetailEnabled(bool enabled)
    {
        standaloneDetailEnabled = enabled;
    }


    public void Shake()
    {
        transform.DOKill();
        transform.localScale = baseScale;
        transform.DOShakePosition(0.35f, new Vector3(14f, 0f, 0f), 18).SetUpdate(true);
    }
}
