using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>カードPrefab全体を拡大表示し、背景または戻るボタンで閉じます。</summary>
public class CardDetailOverlay : MonoBehaviour, IPointerClickHandler
{
    private static CardDetailOverlay current;
    private readonly List<CanvasGroup> hiddenButtons = new List<CanvasGroup>();
    private readonly List<float> previousAlphas = new List<float>();
    private readonly List<bool> previousInteraction = new List<bool>();
    private readonly List<bool> previousRaycasts = new List<bool>();

    private void RestoreButtons()
    {
        for (int i = 0; i < hiddenButtons.Count; i++)
            if (hiddenButtons[i] != null)
            {
                hiddenButtons[i].alpha = previousAlphas[i];
                hiddenButtons[i].interactable = previousInteraction[i];
                hiddenButtons[i].blocksRaycasts = previousRaycasts[i];
            }
        hiddenButtons.Clear();
        previousAlphas.Clear();
        previousInteraction.Clear();
        previousRaycasts.Clear();
    }

    private void OnDestroy() { RestoreButtons(); }

    private void HideBackgroundButtons(Canvas canvas)
    {
        foreach (BottomScreenButton button in canvas.GetComponentsInChildren<BottomScreenButton>(true))
        {
            if (button.transform.IsChildOf(transform)) continue;
            CanvasGroup group = button.GetComponent<CanvasGroup>();
            if (group == null) group = button.gameObject.AddComponent<CanvasGroup>();
            hiddenButtons.Add(group);
            previousAlphas.Add(group.alpha);
            previousInteraction.Add(group.interactable);
            previousRaycasts.Add(group.blocksRaycasts);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
    }

    public static void Show(CardUIView source)
    {
        if (source == null || source.Card == null) return;
        if (current != null) current.Close();

        Canvas canvas = source.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        GameObject overlayObject = new GameObject("CardDetailOverlay", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasRenderer), typeof(Image), typeof(GraphicRaycaster));
        overlayObject.layer = 5;
        overlayObject.transform.SetParent(GameViewport.ContentFor(canvas), false);
        overlayObject.transform.SetAsLastSibling();
        Canvas overlayCanvas = overlayObject.GetComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 80;
        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        overlayRect.anchoredPosition = Vector2.zero;
        Image background = overlayObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);

        current = overlayObject.AddComponent<CardDetailOverlay>();
        current.HideBackgroundButtons(canvas);

        GameObject cardObject = Instantiate(source.gameObject, overlayObject.transform);
        cardObject.name = "CardPreview";
        RectTransform cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.56f);
        cardRect.anchoredPosition = Vector2.zero;
        CardPreviewLayout.Apply(cardRect);
        cardObject.transform.localScale = Vector3.one;

        CardUIView preview = cardObject.GetComponent<CardUIView>();
        if (preview != null)
        {
            preview.Setup(source.Card);
            preview.enabled = false;
        }
        UIHoverScale hover = cardObject.GetComponent<UIHoverScale>();
        if (hover != null) hover.enabled = false;
        foreach (MonoBehaviour behaviour in cardObject.GetComponents<MonoBehaviour>())
            if (behaviour is RewardCardClickHandler || behaviour is DeckReplacementCardClickHandler)
                behaviour.enabled = false;
        foreach (Graphic graphic in cardObject.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        CreateBackButton(overlayObject.transform, source.cardNameText != null ? source.cardNameText.font : null);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerPress == gameObject) Close();
    }

    public void Close()
    {
        RestoreButtons();
        if (current == this) current = null;
        Destroy(gameObject);
    }

    private static void CreateBackButton(Transform parent, TMP_FontAsset font)
    {
        GameObject buttonObject = new GameObject("BackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = 5;
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.14f);
        rect.sizeDelta = new Vector2(240f, 68f);
        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.38f, 0.3f, 1f);
        buttonObject.GetComponent<Button>().onClick.AddListener(() => { if (current != null) current.Close(); });

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = 5;
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = "もどる";
        text.fontSize = 28f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        BottomScreenButton.Apply(buttonObject.GetComponent<Button>());
        ButtonDesign.Apply(buttonObject.GetComponent<Button>());
    }
}
