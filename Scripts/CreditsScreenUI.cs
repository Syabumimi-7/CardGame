using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Material credits grouped by use, with readable source links.</summary>
public class CreditsScreenUI : MonoBehaviour
{
    private GameManager game;
    private GameObject screen, menu;
    private bool menuWasActive;
    private RectTransform composition, content;
    private ScrollRect scroll;
    private TMP_Text section;
    private readonly List<GameObject> markers = new List<GameObject>();
    private readonly List<GameObject> cards = new List<GameObject>();

    public void Configure(GameManager owner)
    {
        game = owner;
        var target = game.startScreenRoot.transform.Find("MenuPanel/CreditsButton");
        if (target == null) return;
        menu = target.parent.gameObject;
        var button = target.GetComponent<Button>();
        if (button == null) return;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(Open);
    }

    public void Open()
    {
        if (screen != null) return;
        menuWasActive = menu != null && menu.activeSelf;
        if (menu != null) menu.SetActive(false);
        var root = Rect("CreditsScreen", game.startScreenRoot.transform, Vector2.zero, Vector2.zero);
        Stretch(root); screen = root.gameObject;
        root.gameObject.AddComponent<Image>().color = new Color(.018f, .025f, .032f, .99f);
        var canvas = root.gameObject.AddComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = 70;
        root.gameObject.AddComponent<GraphicRaycaster>();
        composition = Rect("CreditsLayout", root, Vector2.zero, new Vector2(1600, 900));
        Label("クレジット", composition, new Vector2(0, 390), new Vector2(1100, 65), 42, true);
        string[] names = { "音楽・効果音", "キャラクター・アニメーション", "ステージ" };
        markers.Clear();
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            var tab = MakeButton(names[i], composition, new Vector2((i - 1) * 480, 320), new Vector2(450, 58), () => SelectCategory(index));
            var marker = Rect("SelectedCategory", tab.transform, Vector2.zero, Vector2.zero);
            Stretch(marker); marker.offsetMin = new Vector2(-3, -3); marker.offsetMax = new Vector2(3, 3);
            var gold = marker.gameObject.AddComponent<BattleHudGraphic>();
            gold.color = new Color(.47f, .32f, .095f, .85f); gold.metal = new Color(1f, .85f, .40f);
            gold.glow = new Color(1f, .72f, .25f, .8f); gold.raycastTarget = false;
            marker.SetSiblingIndex(1); markers.Add(marker.gameObject);
        }
        section = Label("", composition, new Vector2(0, 261), new Vector2(1400, 35), 21);
        var frame = Rect("CreditsFrame", composition, new Vector2(0, -48), new Vector2(1480, 574));
        var border = frame.gameObject.AddComponent<BattleHudGraphic>();
        border.color = new Color(.028f, .037f, .046f); border.metal = new Color(.64f, .50f, .29f); border.raycastTarget = false;
        var viewport = Rect("Viewport", frame, Vector2.zero, Vector2.zero); Stretch(viewport);
        viewport.offsetMin = new Vector2(18, 14); viewport.offsetMax = new Vector2(-38, -14);
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Rect("Materials", viewport, Vector2.zero, Vector2.zero);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
        grid.cellSize = new Vector2(690, 286); grid.spacing = new Vector2(22, 20);
        grid.padding = new RectOffset(8, 8, 8, 8); grid.childAlignment = TextAnchor.UpperCenter;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = frame.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
        scroll.scrollSensitivity = 55; scroll.movementType = ScrollRect.MovementType.Clamped;
        var track = Rect("Scrollbar", frame, new Vector2(720, 0), new Vector2(8, 538));
        track.gameObject.AddComponent<Image>().color = new Color(.10f, .10f, .09f);
        var handle = Rect("Handle", track, Vector2.zero, Vector2.zero); Stretch(handle);
        var image = handle.gameObject.AddComponent<Image>(); image.color = new Color(.78f, .61f, .3f);
        var bar = track.gameObject.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle; bar.targetGraphic = image; scroll.verticalScrollbar = bar;
        MakeButton("もどる", composition, new Vector2(0, -390), new Vector2(300, 58), game.ShowTitle);
        Resize(); SelectCategory(0);
    }

    public void Close()
    {
        if (screen == null) return;
        screen.SetActive(false); Destroy(screen); screen = null;
        if (menu != null) menu.SetActive(menuWasActive);
        markers.Clear(); cards.Clear();
    }

    public void SelectCategory(int category)
    {
        if (screen == null || category < 0 || category > 2) return;
        foreach (var card in cards) { card.SetActive(false); Destroy(card); }
        cards.Clear();
        for (int i = 0; i < markers.Count; i++) markers[i].SetActive(i == category);
        var entries = CreditsMaterials.ForCategory(category);
        section.text = new[] { "音楽・効果音", "キャラクター・アニメーション", "ステージ素材" }[category] + $"　／　{entries.Length}件";
        foreach (var entry in entries) AddCard(entry);
        Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
    }

    private void AddCard(CreditsMaterials.Entry entry)
    {
        var card = Rect("MaterialCredit", content, Vector2.zero, new Vector2(690, 286)); cards.Add(card.gameObject);
        var frame = card.gameObject.AddComponent<BattleHudGraphic>();
        frame.color = new Color(.045f, .052f, .06f); frame.metal = new Color(.35f, .29f, .20f); frame.raycastTarget = false;
        var usage = Label("使用箇所：" + entry.use, card, new Vector2(0, 104), new Vector2(638, 48), 22);
        usage.color = new Color(.86f, .73f, .48f);
        var name = Label(entry.name, card, new Vector2(0, 43), new Vector2(638, 74), 25, true);
        var author = Label("作者・提供元：" + entry.author, card, new Vector2(0, -31), new Vector2(638, 62), 22);
        var link = Rect("SourceURL", card, new Vector2(0, -104), new Vector2(638, 52));
        var hit = link.gameObject.AddComponent<Image>(); hit.color = Color.clear;
        var button = link.gameObject.AddComponent<Button>(); button.targetGraphic = hit;
        button.onClick.AddListener(() => Application.OpenURL(entry.url));
        var text = Label(entry.url, link, Vector2.zero, new Vector2(638, 52), 20);
        text.color = new Color(.55f, .78f, .93f); text.fontStyle = FontStyles.Underline;
        foreach (var label in new[] { usage, name, author, text })
        {
            label.alignment = TextAlignmentOptions.MidlineLeft; label.enableWordWrapping = true;
            label.enableAutoSizing = true; label.fontSizeMin = label.fontSize - 3; label.fontSizeMax = label.fontSize;
            label.overflowMode = TextOverflowModes.Overflow;
        }
    }

    private void Update() { if (screen != null) Resize(); }
    private void Resize()
    {
        var size = ((RectTransform)screen.transform).rect.size;
        composition.localScale = Vector3.one * Mathf.Max(.01f, Mathf.Min(size.x / 1600, size.y / 900));
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static TMP_Text Label(string value, Transform parent, Vector2 position, Vector2 size, float fontSize, bool heading = false)
    {
        var text = Rect("Label", parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        GameScreenTextStyle.Apply(text, heading); text.text = value; text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center; return text;
    }
    private static Button MakeButton(string label, Transform parent, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var rect = Rect(label, parent, position, size); rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
        var text = Label(label, rect, Vector2.zero, size, 25); Stretch(text.rectTransform);
        ButtonDesign.Apply(button); return button;
    }
}
