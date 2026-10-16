using UnityEngine;

/// <summary>Shared enlarged card size, fitted between the heading and footer controls.</summary>
[DisallowMultipleComponent]
public class CardPreviewLayout : MonoBehaviour
{
    private static readonly Vector2 PreferredSize = new Vector2(472.5f, 660f);
    private const float Margin = 20f;
    private RectTransform heading;

    public static void Apply(RectTransform card, RectTransform heading = null)
    {
        var layout = card.GetComponent<CardPreviewLayout>();
        if (layout == null) layout = card.gameObject.AddComponent<CardPreviewLayout>();
        layout.heading = heading;
        layout.Refresh();
    }

    private void LateUpdate() { Refresh(); }

    private void Refresh()
    {
        var card = (RectTransform)transform;
        var parent = transform.parent as RectTransform;
        if (parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f) return;
        Rect area = parent.rect;
        float bottom = area.yMin + area.height * BottomScreenButton.HeightAnchor + 34f + Margin;
        float top = area.yMax - Margin;
        if (heading != null)
        {
            Vector3 headingBottom = heading.TransformPoint(new Vector3(0f, heading.rect.yMin, 0f));
            top = Mathf.Min(top, parent.InverseTransformPoint(headingBottom).y - Margin);
        }
        float scale = Mathf.Clamp01(Mathf.Min((area.width - Margin * 2f) / PreferredSize.x,
            (top - bottom) / PreferredSize.y));
        Vector2 size = PreferredSize * scale;
        float desiredCenter = area.yMin + area.height * card.anchorMin.y;
        float center = Mathf.Clamp(desiredCenter, bottom + size.y * 0.5f, top - size.y * 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.localScale = Vector3.one;
        card.sizeDelta = size;
        card.anchoredPosition = new Vector2(0f, center - desiredCenter);
    }
}
