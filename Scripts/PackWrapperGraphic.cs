using UnityEngine;
using UnityEngine.UI;

/// <summary>箔の折り目、圧着シール、縁飾りをUGUIの頂点だけで描画します。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PackWrapperGraphic : MaskableGraphic
{
    public enum Part
    {
        Body,
        Seal,
        Glow,
        Frame,
        Shadow,
        Outline
    }

    public Part part;
    public int palette;
    private static Color Gold => new Color(.83f, .65f, .36f);
    private Color Base => palette == 0 ? new Color(.085f, .13f, .17f) : palette == 1 ? new Color(.24f, .16f, .065f) : new Color(.16f, .09f, .20f);
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (part == Part.Shadow)
        {
            for (int i = 18; i >= 1; i--)
                Border(vh, Expand(r, i), 2f, new Color(0f, 0f, 0f, (19 - i) * .007f));
            Quad(vh, r, new Color(0f, 0f, 0f, .9f), new Color(0f, 0f, 0f, .9f));
            return;
        }

        if (part == Part.Outline)
        {
            Border(vh, r, 4f, new Color(.015f, .02f, .025f, 1f));
            Border(vh, Inset(r, 4f), 2f, new Color(.88f, .72f, .43f, 1f));
            Border(vh, Inset(r, 6f), .8f, new Color(1f, .92f, .72f, .85f));
            return;
        }

        if (part == Part.Glow)
        {
            for (int i = 16; i >= 1; i--)
                Border(vh, Expand(r, i), 2, new Color(1f, .63f, .12f, (17 - i) * .006f * color.a));
            Border(vh, r, 2, new Color(1f, .87f, .48f, color.a));
            return;
        }

        if (part == Part.Seal)
        {
            Quad(vh, r, Shade(Base, .65f), Shade(Base, 1.8f));
            for (float x = r.xMin; x < r.xMax; x += 5)
            {
                Quad(vh, new Rect(x, r.yMin + 3, 1.3f, r.height - 6), Gold * .35f, Gold);
                Triangle(vh, new Vector2(x, r.yMax - 1), new Vector2(x + 2.5f, r.yMax + 2), new Vector2(x + 5, r.yMax - 1), Gold);
                Triangle(vh, new Vector2(x, r.yMin + 1), new Vector2(x + 2.5f, r.yMin - 2), new Vector2(x + 5, r.yMin + 1), Base);
            }

            Line(vh, new Vector2(r.xMin, r.yMin + 5), new Vector2(r.xMax, r.yMin + 5), 1, Gold * .8f);
            Line(vh, new Vector2(r.xMin, r.yMax - 5), new Vector2(r.xMax, r.yMax - 5), 1, Gold);
            return;
        }

        if (part != Part.Frame)
        {
            Quad(vh, Expand(r, 3), new Color(0, 0, 0, .8f), new Color(0, 0, 0, .8f));
            Quad(vh, r, Shade(Base, .6f), Shade(Base, 1.5f));
            // Fine vertical foil grain; deterministic, independent of game randomness.
            for (int i = 0; i < 70; i++)
            {
                float x = Mathf.Lerp(r.xMin, r.xMax, i / 69f);
                float shine = .015f + (Mathf.Sin(i * 2.71f) + 1) * .018f;
                Quad(vh, new Rect(x, r.yMin, 1, r.height), new Color(1, .86f, .60f, shine), new Color(1, .90f, .7f, shine));
            }
        }

        Border(vh, Inset(r, 3), 2, Gold * .8f);
        Border(vh, Inset(r, 11), 1, Gold * .6f);
        // Broad folded side gussets with alternating dark and reflective creases.
        foreach (int side in new[]{-1, 1})
        {
            float edge = side < 0 ? r.xMin : r.xMax;
            float inner = edge - side * 19;
            Triangle(vh, new Vector2(edge, r.yMin), new Vector2(inner, r.center.y), new Vector2(edge, r.yMax), Gold * .4f);
            Line(vh, new Vector2(edge - side * 4, r.yMin + 8), new Vector2(inner, r.center.y), 1.5f, Gold);
            Line(vh, new Vector2(inner, r.center.y), new Vector2(edge - side * 4, r.yMax - 8), 1.2f, Gold * .75f);
        }

        Rect innerFrame = Inset(r, 22);
        Border(vh, innerFrame, .8f, new Color(.83f, .65f, .36f, .55f));
        foreach (int sx in new[]{-1, 1})
            foreach (int sy in new[]{-1, 1})
            {
                Vector2 corner = new Vector2(sx < 0 ? innerFrame.xMin : innerFrame.xMax, sy < 0 ? innerFrame.yMin : innerFrame.yMax);
                Vector2 a = corner - new Vector2(sx * 28, 0), b = corner - new Vector2(0, sy * 28);
                Line(vh, a, b, 2, Gold);
                Line(vh, a - new Vector2(sx * 8, 0), b - new Vector2(0, sy * 8), .8f, Gold * .65f);
                Diamond(vh, corner - new Vector2(sx * 10, sy * 10), 3, Gold);
            }

        Diamond(vh, new Vector2(0, innerFrame.yMax), 4, Gold);
        Diamond(vh, new Vector2(0, innerFrame.yMin), 4, Gold);
    }

    private static Rect Expand(Rect r, float n) => new Rect(r.x - n, r.y - n, r.width + 2 * n, r.height + 2 * n);
    private static Color Shade(Color c, float value) => new Color(c.r * value, c.g * value, c.b * value, 1);
    private static Rect Inset(Rect r, float n) => Expand(r, -n);
    private static void Border(VertexHelper vh, Rect r, float w, Color c)
    {
        Quad(vh, new Rect(r.xMin, r.yMin, r.width, w), c, c);
        Quad(vh, new Rect(r.xMin, r.yMax - w, r.width, w), c, c);
        Quad(vh, new Rect(r.xMin, r.yMin, w, r.height), c, c);
        Quad(vh, new Rect(r.xMax - w, r.yMin, w, r.height), c, c);
    }

    private static void Quad(VertexHelper vh, Rect r, Color bottom, Color top)
    {
        int i = vh.currentVertCount;
        vh.AddVert(new Vector3(r.xMin, r.yMin), bottom, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMin), bottom, Vector2.zero);
        vh.AddVert(new Vector3(r.xMax, r.yMax), top, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), top, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero);
        vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
    }

    private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 d = b - a;
        Vector2 n = new Vector2(-d.y, d.x).normalized * width * .5f;
        Triangle(vh, a - n, a + n, b + n, color);
        Triangle(vh, a - n, b + n, b - n, color);
    }

    private static void Diamond(VertexHelper vh, Vector2 p, float size, Color color)
    {
        Triangle(vh, p + Vector2.up * size, p + Vector2.right * size, p + Vector2.down * size, color);
        Triangle(vh, p + Vector2.up * size, p + Vector2.down * size, p + Vector2.left * size, color);
    }
}
