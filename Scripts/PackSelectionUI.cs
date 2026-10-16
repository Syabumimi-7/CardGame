using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Unity UIだけで構成したパック選択画面。開封用の各部品は独立した子オブジェクトです。</summary>
public class PackSelectionUI : MonoBehaviour
{
    private GameManager owner;
    private PackSelectionItem[] packs;
    private RectTransform composition;
    private bool starting;

    public static PackSelectionUI Install(GameManager game)
    {
        if (game.startScreenRoot == null) return null;
        Transform panel = game.startScreenRoot.transform.Find("GameStartPanel");
        if (panel == null) return null;
        var ui = panel.GetComponent<PackSelectionUI>();
        if (ui == null) ui = panel.gameObject.AddComponent<PackSelectionUI>();
        ui.owner = game;
        ui.Build();
        return ui;
    }

    private void Build()
    {
        Transform existing = transform.Find("PackSelectionDesign");
        if (existing == null)
        {
            // Keep the old menu available in the scene, but replace its presentation and input.
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            foreach (Graphic graphic in GetComponents<Graphic>()) graphic.enabled = false;
            var root = Rect("PackSelectionDesign", transform, Vector2.zero, Vector2.zero);
            Stretch(root);
            var shade = root.gameObject.AddComponent<Image>();
            shade.color = new Color(.025f, .035f, .045f, .68f);
            composition = Rect("Composition", root, Vector2.zero, new Vector2(1600, 900));
            Label("Heading", composition, "パックを選択", new Vector2(0, 354), new Vector2(900, 70), 42);
            var line = Rect("HeadingRule", composition, new Vector2(0, 303), new Vector2(640, 1));
            line.gameObject.AddComponent<Image>().color = new Color(.83f, .66f, .39f, .55f);
            for (int i = 0; i < 3; i++) CreatePack(composition, i);
            var back = Rect("BackButton", composition, new Vector2(-622, -366), new Vector2(238, 68));
            back.gameObject.AddComponent<Image>();
            var button = back.gameObject.AddComponent<Button>();
            Label("Label", back, "もどる", Vector2.zero, new Vector2(218, 54), 28);
            ButtonDesign.Apply(button);
        }
        else composition = (RectTransform)existing.Find("Composition");
        var backdrop = composition.parent.GetComponent<Image>();
        if (backdrop != null) backdrop.color = new Color(.025f, .035f, .045f, .68f);

        foreach (var wrapper in composition.GetComponentsInChildren<PackWrapperGraphic>(true))
        {
            if (wrapper.GetComponent<CanvasRenderer>() == null) wrapper.gameObject.AddComponent<CanvasRenderer>();
            wrapper.SetVerticesDirty();
        }

        packs = composition.GetComponentsInChildren<PackSelectionItem>(true);
        for (int i = 0; i < 3; i++)
        {
            var nameLabel = composition.Find("Pack" + (i + 1) + "/PackVisual/NamePlate/PackName").GetComponent<TMP_Text>();
            nameLabel.text = GameManager.GetPackDisplayName(i);
            var area = composition.Find("Pack" + (i + 1) + "/PackVisual/IllustrationArea") as RectTransform;
            if (area == null) continue;
            Stretch(area);
            area.SetSiblingIndex(2);
            if (area.GetComponent<RectMask2D>() == null) area.gameObject.AddComponent<RectMask2D>();
            var oldPortrait = area.GetComponent<Image>();
            if (oldPortrait != null) oldPortrait.enabled = false;
            var portraitRect = area.Find("Portrait") as RectTransform;
            if (portraitRect == null) portraitRect = Rect("Portrait", area, Vector2.zero, Vector2.zero);
            var portrait = portraitRect.GetComponent<Image>();
            if (portrait == null) portrait = portraitRect.gameObject.AddComponent<Image>();
            portrait.sprite = Resources.Load<Sprite>("PackPortraits/Player" + (i + 1));
            portrait.color = Color.white;
            portrait.preserveAspect = false;
            portrait.raycastTarget = false;
            // Fill the wrapper with a centered crop, retaining the portrait's proportions.
            float aspect = portrait.sprite != null ? portrait.sprite.rect.width / portrait.sprite.rect.height : 1f;
            Vector2 size = new Vector2(300f, 540f);
            portraitRect.sizeDelta = aspect >= size.x / size.y
                ? new Vector2(size.y * aspect, size.y) : new Vector2(size.x, size.x / aspect);
            var visual = area.parent;
            var shadow = visual.Find("PackShadow") as RectTransform;
            if (shadow == null)
            {
                shadow = Rect("PackShadow", visual, new Vector2(0f, -9f), new Vector2(300f, 540f));
                var graphic = shadow.gameObject.AddComponent<PackWrapperGraphic>();
                graphic.part = PackWrapperGraphic.Part.Shadow;
                graphic.raycastTarget = false;
            }
            shadow.SetAsFirstSibling();
            area.SetSiblingIndex(visual.Find("Body").GetSiblingIndex() + 1);
            var frame = visual.Find("PortraitFrame") as RectTransform;
            if (frame == null)
            {
                frame = Rect("PortraitFrame", visual, Vector2.zero, new Vector2(300f, 494f));
                var graphic = frame.gameObject.AddComponent<PackWrapperGraphic>();
                graphic.part = PackWrapperGraphic.Part.Frame;
                graphic.palette = i;
                graphic.raycastTarget = false;
            }
            frame.SetSiblingIndex(area.GetSiblingIndex() + 1);
            var outline = visual.Find("PackOutline") as RectTransform;
            if (outline == null)
            {
                outline = Rect("PackOutline", visual, Vector2.zero, new Vector2(300f, 540f));
                var graphic = outline.gameObject.AddComponent<PackWrapperGraphic>();
                graphic.part = PackWrapperGraphic.Part.Outline;
                graphic.raycastTarget = false;
            }
            outline.SetAsLastSibling();
        }
        for (int i = 0; i < packs.Length; i++) packs[i].Bind(this, i);
        Button backButton = composition.Find("BackButton").GetComponent<Button>();
        backButton.onClick.RemoveListener(Return);
        backButton.onClick.AddListener(Return);
        RefreshLayout();
    }

    private static void CreatePack(Transform parent, int index)
    {
        var root = Rect("Pack" + (index + 1), parent, new Vector2((index - 1) * 430, -10), new Vector2(300, 540));
        var hit = root.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var button = root.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;
        root.gameObject.AddComponent<PackSelectionItem>();
        var visual = Rect("PackVisual", root, Vector2.zero, new Vector2(300, 540));
        var glow = Rect("Glow", visual, Vector2.zero, new Vector2(300, 540));
        var glowGraphic = glow.gameObject.AddComponent<PackWrapperGraphic>();
        glowGraphic.part = PackWrapperGraphic.Part.Glow;
        glowGraphic.raycastTarget = false;
        Wrapper("Body", visual, Vector2.zero, new Vector2(300, 494), index, PackWrapperGraphic.Part.Body);
        Wrapper("TopSeal", visual, new Vector2(0, 254), new Vector2(300, 32), index, PackWrapperGraphic.Part.Seal);
        Wrapper("BottomSeal", visual, new Vector2(0, -254), new Vector2(300, 32), index, PackWrapperGraphic.Part.Seal);
        // Reserved separately for future illustrations, descriptions and opening/dealing animation.
        Rect("IllustrationArea", visual, new Vector2(0, 57), new Vector2(248, 320));
        var plaque = Rect("NamePlate", visual, new Vector2(0, -135), new Vector2(252, 58));
        var frame = plaque.gameObject.AddComponent<ButtonDesignGraphic>();
        frame.raycastTarget = false;
        Label("PackName", plaque, GameManager.GetPackDisplayName(index), Vector2.zero, new Vector2(228, 48), 27);
        Label("Description", visual, string.Empty, new Vector2(0, -193), new Vector2(242, 58), 18);
        Rect("ContentsOrigin", root, new Vector2(0, 232), Vector2.zero);
    }

    private static void Wrapper(string name, Transform parent, Vector2 position, Vector2 size, int index, PackWrapperGraphic.Part part)
    {
        var graphic = Rect(name, parent, position, size).gameObject.AddComponent<PackWrapperGraphic>();
        graphic.palette = index;
        graphic.part = part;
        graphic.raycastTarget = false;
    }

    public void Hover(PackSelectionItem current)
    {
        if (starting || packs == null) return;
        foreach (var pack in packs) pack.SetEmphasis(pack == current, current != null && pack != current);
    }

    public void Choose(int index)
    {
        if (starting || owner == null) return;
        starting = true;
        owner.StartGame(index);
    }

    public PackSelectionItem GetPack(int index)
    {
        return packs != null && index >= 0 && index < packs.Length ? packs[index] : null;
    }

    private void Return()
    {
        if (!starting && owner != null) owner.ShowTitle();
    }

    private void OnEnable()
    {
        starting = false;
        if (packs != null) foreach (var pack in packs) pack.ResetVisual();
        RefreshLayout();
    }
    private void LateUpdate() { RefreshLayout(); }
    public void RefreshLayout()
    {
        if (composition == null) return;
        Rect area = ((RectTransform)transform).rect;
        float scale = Mathf.Min(area.width / 1600f, area.height / 900f);
        composition.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Label(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        var theme = ButtonDesignTheme.Current;
        if (theme != null) { text.font = theme.font; text.fontSharedMaterial = theme.textMaterial; }
        text.text = value; text.fontSize = fontSize;
        text.color = new Color(.97f, .90f, .76f);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        if (name == "Heading") GameScreenTextStyle.Apply(text, true);
    }
}
