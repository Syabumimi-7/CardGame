using UnityEngine;
using UnityEngine.UI;

/// <summary>Screen footer controls share the card detail back button's position and size.</summary>
[DisallowMultipleComponent]
public class BottomScreenButton : MonoBehaviour
{
    public const float HeightAnchor = 0.14f;
    private float horizontalAnchor = 0.5f;

    public static void Apply(Button button, float horizontalAnchor = 0.5f)
    {
        if (button == null) return;
        var layout = button.GetComponent<BottomScreenButton>();
        if (layout == null) layout = button.gameObject.AddComponent<BottomScreenButton>();
        layout.horizontalAnchor = horizontalAnchor;
        layout.Refresh();
    }

    private void LateUpdate() { Refresh(); }

    private void Refresh()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        RectTransform parent = transform.parent as RectTransform;
        if (canvas == null || parent == null) return;
        RectTransform root = GameViewport.ContentFor(canvas);
        if (root == null) return;
        var rect = (RectTransform)transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        float x = root.rect.xMin + root.rect.width * horizontalAnchor;
        // Confirmation pairs remain separated even when the viewport becomes narrower.
        if (Mathf.Approximately(horizontalAnchor, 0.42f)) x = root.rect.center.x - 132f;
        if (Mathf.Approximately(horizontalAnchor, 0.58f)) x = root.rect.center.x + 132f;
        Vector3 position = root.TransformPoint(new Vector3(
            x,
            root.rect.yMin + root.rect.height * HeightAnchor, 0f));
        rect.localPosition = parent.InverseTransformPoint(position);
        rect.sizeDelta = new Vector2(240f * Mathf.Abs(root.lossyScale.x / parent.lossyScale.x),
            68f * Mathf.Abs(root.lossyScale.y / parent.lossyScale.y));
    }
}
