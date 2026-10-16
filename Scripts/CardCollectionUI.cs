using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Pack-specific collection with unknown list values and three explicit performance previews.</summary>
public partial class CardCollectionUI : MonoBehaviour
{
    private GameManager game;
    private GameObject screen, detail;
    private GameObject titleMenu;
    private bool titleMenuWasActive;
    private RectTransform content;
    private ScrollRect scroll;
    private GridLayoutGroup cardGrid;
    private Button back;
    private readonly List<GameObject> packMarkers = new List<GameObject>();
    private readonly List<GameObject> patternMarkers = new List<GameObject>();
    private List<CardData> cards;
    private CardUIView preview;
    private TMP_Text patternStatus;
    public bool IsDetailOpen => detail != null;
    private int packIndex, cardIndex, pattern;

    public void Configure(GameManager owner)
    {
        game = owner;
        var menu = game.startScreenRoot != null ? game.startScreenRoot.transform.Find("MenuPanel/CollectionButton") : null;
        var button = menu != null ? menu.GetComponent<Button>() : null;
        titleMenu = menu != null ? menu.parent.gameObject : null;
        if (button != null) button.onClick.AddListener(Open);
    }

    public void Open()
    {
        if (screen != null) return;
        if (titleMenu != null) { titleMenuWasActive = titleMenu.activeSelf; titleMenu.SetActive(false); }
        screen = Overlay("CardCollection", game.startScreenRoot.transform, 70, .88f);
        Label(screen.transform, "コレクション", new Vector2(.5f, .96f), new Vector2(800f, 65f), 40f, true);
        packMarkers.Clear();
        for (int i = 0; i < 3; i++)
        {
            int index = i;
            var button = MakeButton(screen.transform, GameManager.GetPackDisplayName(i), new Vector2(.25f + .25f * i, .78f), new Vector2(380f, 66f), () => SelectPack(index));
            packMarkers.Add(SelectionFrame(button.transform, "SelectedPack"));
        }
        var frame = Rect("CardListFrame", screen.transform, new Vector2(.5f, .5f), new Vector2(1660f, 620f));
        frame.anchorMin = new Vector2(.07f, .22f); frame.anchorMax = new Vector2(.93f, .72f);
        frame.sizeDelta = Vector2.zero; frame.anchoredPosition = Vector2.zero;
        var border = frame.gameObject.AddComponent<BattleHudGraphic>();
        border.color = new Color(.025f, .035f, .04f, .94f); border.raycastTarget = false;
        var viewport = Rect("Viewport", frame, new Vector2(.5f, .5f), Vector2.zero);
        Stretch(viewport); viewport.offsetMin = new Vector2(12f, 12f); viewport.offsetMax = new Vector2(-34f, -12f);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        content = Rect("Cards", viewport, new Vector2(.5f, 1f), Vector2.zero);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        cardGrid = grid;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
        grid.cellSize = new Vector2(220f, 308f); grid.spacing = new Vector2(24f, 24f);
        grid.padding = new RectOffset(24, 24, 24, 24); grid.childAlignment = TextAnchor.UpperCenter;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = frame.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 45f; scroll.movementType = ScrollRect.MovementType.Clamped;
        var track = Rect("Scrollbar", frame, Vector2.one, new Vector2(12f, 0f));
        track.anchorMin = new Vector2(1f, 0f); track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1f, .5f); track.anchoredPosition = new Vector2(-12f, 0f); track.sizeDelta = new Vector2(12f, -28f);
        track.gameObject.AddComponent<Image>().color = new Color(.10f, .09f, .07f);
        var handle = Rect("Handle", track, new Vector2(.5f, .5f), Vector2.zero); Stretch(handle);
        var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.81f, .65f, .39f);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage; scroll.verticalScrollbar = scrollbar;
        back = MakeButton(screen.transform, "もどる", new Vector2(.5f, .14f), new Vector2(240f, 68f), game.ShowTitle);
        BottomScreenButton.Apply(back);
        SelectPack(packIndex);
        BuildHistoryTabs(frame);
    }

    public void SelectPack(int index)
    {
        CloseDetail(); packIndex = index;
        cards = new List<CardData>(game.deckManager.GetPool(index));
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i).gameObject; child.SetActive(false); Destroy(child);
        }
        for (int i = 0; i < cards.Count; i++)
        {
            var obj = Instantiate(game.deckUIManager.cardUIPrefab, content);
            var view = obj.GetComponent<CardUIView>(); view.Setup(new CardInstance(cards[i]));
            view.SetStandaloneDetailEnabled(false);
            var click = obj.AddComponent<CollectionCardClick>(); click.Configure(this, i);
            view.GetComponent<CardDesignLayout>().EnableCollectionHover(click, 1.1f, 0f);
        }
        for (int i = 0; i < packMarkers.Count; i++) packMarkers[i].SetActive(i == index);
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.verticalNormalizedPosition = 1f;
    }

    public void ShowCard(int index, bool resetPattern = true)
    {
        if (cards == null || cards.Count == 0) return;
        CloseDetail(); cardIndex = (index + cards.Count) % cards.Count;
        if (resetPattern) pattern = 0;
        back.gameObject.SetActive(false);
        detail = Overlay("CollectionDetail", screen.transform, 72, .98f);
        Label(detail.transform, "カード詳細", new Vector2(.5f, .95f), new Vector2(800f, 75f), 42f, true);
        var cardObject = Instantiate(game.deckUIManager.cardUIPrefab, detail.transform);
        var cardRect = cardObject.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(.5f, .55f); cardRect.anchoredPosition = Vector2.zero;
        CardPreviewLayout.Apply(cardRect); preview = cardObject.GetComponent<CardUIView>();
        preview.SetStandaloneDetailEnabled(false);
        foreach (var graphic in cardObject.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        var information = Rect("PerformancePanel", detail.transform, new Vector2(.82f, .55f), new Vector2(330f, 480f));
        var informationFrame = information.gameObject.AddComponent<BattleHudGraphic>();
        informationFrame.color = new Color(.045f, .04f, .03f, .96f); informationFrame.raycastTarget = false;
        patternStatus = Label(detail.transform, "", new Vector2(.82f, .73f), new Vector2(310f, 60f), 28f);
        patternMarkers.Clear();
        for (int i = 0; i < 3; i++)
        {
            int selected = i;
            var button = MakeButton(detail.transform, "パターン" + (i + 1), new Vector2(.82f, .64f - i * .1f), new Vector2(240f, 68f), () => SelectPattern(selected));
            patternMarkers.Add(SelectionFrame(button.transform, "SelectedPattern"));
        }
        MakeButton(detail.transform, "〈", new Vector2(.3f, .54f), new Vector2(74f, 80f), () => ShowCard(cardIndex - 1, false));
        MakeButton(detail.transform, "〉", new Vector2(.7f, .54f), new Vector2(74f, 80f), () => ShowCard(cardIndex + 1, false));
        var close = MakeButton(detail.transform, "もどる", new Vector2(.5f, .14f), new Vector2(240f, 68f), CloseDetail);
        BottomScreenButton.Apply(close);
        SelectPattern(pattern);
    }

    public void SelectPattern(int index)
    {
        pattern = index;
        var card = CardInstance.CreateCollectionPreview(cards[cardIndex], index);
        preview.Setup(card);
        patternStatus.text = "表示中：パターン" + (index + 1);
        for (int i = 0; i < patternMarkers.Count; i++) patternMarkers[i].SetActive(i == index);
    }

    public void CloseDetail()
    {
        ClearHistoryCardData();
        if (detail != null) { detail.SetActive(false); Destroy(detail); detail = null; }
        if (back != null) back.gameObject.SetActive(true);
    }
    public void Close()
    {
        CloseDetail();
        if (screen != null)
        {
            screen.SetActive(false); Destroy(screen); screen = null;
            if (titleMenu != null) titleMenu.SetActive(titleMenuWasActive);
        }
    }
    private void OnDisable() { Close(); }
    private void LateUpdate()
    {
        ResizeHistoryCards();
        if (screen == null || content == null || cardGrid == null) return;
        float width = Mathf.Clamp((content.rect.width - cardGrid.padding.horizontal - cardGrid.spacing.x * 4f) / 5f, 80f, 220f);
        var size = new Vector2(width, width * 1.4f);
        if (cardGrid.cellSize != size) cardGrid.cellSize = size;
    }

    private static GameObject Overlay(string name, Transform parent, int order, float opacity)
    {
        var rect = Rect(name, parent, new Vector2(.5f, .5f), Vector2.zero); Stretch(rect);
        var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = order;
        rect.gameObject.AddComponent<GraphicRaycaster>();
        rect.gameObject.AddComponent<Image>().color = new Color(.025f, .035f, .045f, opacity);
        return rect.gameObject;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor; rect.sizeDelta = size; return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static TMP_Text Label(Transform parent, string value, Vector2 anchor, Vector2 size, float fontSize, bool heading = false)
    {
        var text = Rect(value, parent, anchor, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center;
        GameScreenTextStyle.Apply(text, heading, heading); return text;
    }
    private static Button MakeButton(Transform parent, string label, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(label, parent, anchor, size); rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
        var text = Label(rect, label, new Vector2(.5f, .5f), size, 25f); Stretch(text.rectTransform);
        ButtonDesign.Apply(button); return button;
    }
    private static GameObject SelectionFrame(Transform button, string name)
    {
        var rect = Rect(name, button, new Vector2(.5f, .5f), Vector2.zero);
        Stretch(rect); rect.offsetMin = new Vector2(-3f, -3f); rect.offsetMax = new Vector2(3f, 3f);
        var graphic = rect.gameObject.AddComponent<BattleHudGraphic>();
        graphic.color = new Color(.47f, .32f, .095f, .85f);
        graphic.metal = new Color(1f, .85f, .40f);
        graphic.glow = new Color(1f, .72f, .25f, .8f);
        graphic.raycastTarget = false; rect.SetSiblingIndex(1);
        return rect.gameObject;
    }
}

public class CollectionCardClick : MonoBehaviour, IPointerClickHandler
{
    private CardCollectionUI owner;
    private int index;
    public bool CanHover => owner != null && !owner.IsDetailOpen;
    public void Configure(CardCollectionUI collection, int cardIndex) { owner = collection; index = cardIndex; }
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) owner.ShowCard(index);
    }
}
