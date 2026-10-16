using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Warm metal headings and readable ivory text for the game's dark panels.</summary>
public static class GameScreenTextStyle
{
    private static Material headingMaterial, bodyMaterial;
    private static readonly Color Ivory = new Color(0.94f, 0.90f, 0.81f);
    private static readonly Color Gold = new Color(0.76f, 0.60f, 0.34f);

    public static void Apply(TMP_Text text, bool heading = false, bool ornament = false)
    {
        var theme = ButtonDesignTheme.Current;
        if (theme != null && theme.font != null) text.font = theme.font;
        if (text.font != null)
        {
            Material material = heading ? headingMaterial : bodyMaterial;
            if (material == null)
            {
                material = new Material(text.font.material) { name = heading ? "Screen Heading" : "Screen Body",
                    hideFlags = HideFlags.HideAndDontSave };
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, heading ? 0.12f : 0.08f);
                material.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.075f, 0.055f, 0.035f, 1f));
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.65f));
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
                material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.5f);
                material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.2f);
                if (heading) headingMaterial = material; else bodyMaterial = material;
            }
            text.fontSharedMaterial = material;
            text.UpdateMeshPadding();
        }
        text.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal;
        text.color = heading ? Color.white : Ivory;
        text.enableVertexGradient = heading;
        text.colorGradient = new VertexGradient(new Color(1f, 0.94f, 0.76f), new Color(1f, 0.94f, 0.76f),
            new Color(0.75f, 0.56f, 0.28f), new Color(0.75f, 0.56f, 0.28f));
        text.characterSpacing = heading ? 2f : 0.5f;
        text.richText = true;
        text.raycastTarget = false;
        if (ornament && text.transform.Find("HeadingOrnament") == null)
        {
            var root = new GameObject("HeadingOrnament", typeof(RectTransform));
            root.layer = text.gameObject.layer;
            var rect = (RectTransform)root.transform;
            rect.SetParent(text.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, -7f);
            rect.sizeDelta = new Vector2(Mathf.Min(360f, text.rectTransform.rect.width * 0.65f), 6f);
            Decoration(rect, "LeftRule", new Vector2(0f, 0.5f), new Vector2(0.46f, 0.5f));
            Decoration(rect, "RightRule", new Vector2(0.54f, 0.5f), new Vector2(1f, 0.5f));
            var jewel = Decoration(rect, "Diamond", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            jewel.sizeDelta = new Vector2(5f, 5f);
            jewel.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
    }

    private static RectTransform Decoration(RectTransform parent, string name, Vector2 min, Vector2 max)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.layer = parent.gameObject.layer;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.sizeDelta = new Vector2(0f, 1f);
        var image = obj.GetComponent<Image>();
        image.color = Gold;
        image.raycastTarget = false;
        return rect;
    }
}
