using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PackSelectionItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    private PackSelectionUI owner;
    private int index;
    private Transform visual;
    private CanvasGroup group;
    private PackWrapperGraphic glow;
    private bool highlighted, muted, held;
    private float emphasis;
    private float brightness = 1f;
    private Graphic[] graphics;

    public void Bind(PackSelectionUI screen, int packIndex)
    {
        owner = screen; index = packIndex;
        visual = transform.Find("PackVisual");
        group = visual.GetComponent<CanvasGroup>();
        if (group == null) group = visual.gameObject.AddComponent<CanvasGroup>();
        glow = visual.Find("Glow").GetComponent<PackWrapperGraphic>();
        graphics = visual.GetComponentsInChildren<Graphic>(true);
        var button = GetComponent<Button>();
        button.onClick.RemoveListener(Choose);
        button.onClick.AddListener(Choose);
        ResetVisual();
    }
    private void Choose() { owner.Choose(index); }
    public void SetEmphasis(bool active, bool dimmed) { highlighted = active; muted = dimmed; }
    public void ResetVisual()
    {
        highlighted = muted = held = false; emphasis = 0;
        if (visual != null) visual.localScale = Vector3.one;
        if (group != null) group.alpha = 1;
        brightness = 1f;
        if (graphics != null) foreach (var graphic in graphics) graphic.canvasRenderer.SetColor(Color.white);
        if (glow != null) { glow.color = new Color(1, 1, 1, 0); glow.SetVerticesDirty(); }
    }
    private void Update()
    {
        if (visual == null) return;
        emphasis = Mathf.MoveTowards(emphasis, highlighted ? 1 : 0, Time.unscaledDeltaTime / .16f);
        float scale = 1 + emphasis * .07f - (held ? .035f : 0);
        visual.localScale = Vector3.one * scale;
        brightness = Mathf.MoveTowards(brightness, muted ? .65f : 1f, Time.unscaledDeltaTime * 5f);
        foreach (var graphic in graphics)
            if (graphic != glow) graphic.canvasRenderer.SetColor(new Color(brightness, brightness, brightness, 1f));
        glow.color = new Color(1, 1, 1, emphasis);
    }
    public void OnPointerEnter(PointerEventData e) { owner?.Hover(this); }
    public void OnPointerExit(PointerEventData e) { held = false; owner?.Hover(null); }
    public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) held = true; }
    public void OnPointerUp(PointerEventData e) { held = false; }
    public void OnSelect(BaseEventData e) { owner?.Hover(this); }
    public void OnDeselect(BaseEventData e) { owner?.Hover(null); }
    private void OnDisable() { ResetVisual(); }
}
