using UnityEngine;
using UnityEngine.UI;

/// <summary>CardDesign.png の装飾を、テクスチャを使わず標準UIの頂点で描画します。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class CardDesignGraphic : MaskableGraphic
{
    public CardRarity rarity;
    [Range(0, 1)]
    public float highlight;
    static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out var c);
        return c;
    }

    static readonly Color Gold = Hex("#D4AF37");
    public static Color RarityColor(CardRarity value) => value == CardRarity.Uncommon ? Hex("#2E7D32") : value == CardRarity.Rare ? Gold : value == CardRarity.SuperRare ? Hex("#A855F7") : Hex("#8A8A8A");
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color metal = RarityColor(rarity);
        for (int i = 10; i >= 1; i--)
            Ring(vh, new Rect(-i, -i, 200 + 2 * i, 280 + 2 * i), 7 + i, 1.5f, new Color(1, .72f, .16f, highlight * (11 - i) * .025f));
        Fill(vh, new Rect(0, 0, 200, 280), 6, Hex("#10141A"), Hex("#272C30"));
        Ring(vh, new Rect(1, 1, 198, 278), 5, 1, Hex("#06090D"));
        Ring(vh, new Rect(3, 3, 194, 274), 4, 3, metal, true);
        Ring(vh, new Rect(7, 7, 186, 266), 2, .7f, Color.Lerp(metal, Color.white, .48f));
        // Header (36 px), illustration frame, and light effect panel share one layout.
        Fill(vh, new Rect(9, 234, 182, 36), 3, Color.Lerp(Hex("#111317"), metal, .13f), Color.Lerp(Hex("#27262A"), metal, .26f));
        Ring(vh, new Rect(9, 234, 182, 36), 3, .8f, Hex("#AE986C"));
        Fill(vh, new Rect(9, 80, 182, 152), 1, Hex("#24282D"), Hex("#30363D"));
        Ring(vh, new Rect(9, 80, 182, 152), 1, .7f, Hex("#74664D"));
        Fill(vh, new Rect(9, 9, 182, 70), 3, Hex("#D9CEB8"), Hex("#F4EEDD"));
        Ring(vh, new Rect(9, 9, 182, 70), 3, 1, Hex("#A38D63"));
        Ring(vh, new Rect(12, 12, 176, 64), 2, .45f, Hex("#C5B89B"));
        foreach (float x in new[]{13f, 187f})
            foreach (float y in new[]{13f, 75f, 238f, 266f})
            {
                float sx = x < 100 ? 1 : -1, sy = (y == 13 || y == 238) ? 1 : -1;
                Line(vh, new Vector2(x, y), new Vector2(x + sx * 6, y), .8f, Hex("#A38D63"));
                Line(vh, new Vector2(x, y), new Vector2(x, y + sy * 5), .8f, Hex("#A38D63"));
            }

        if (highlight > .001f)
            Ring(vh, new Rect(2, 2, 196, 276), 5, 1.5f, new Color(1, .84f, .38f, highlight));
    }

    public static void DrawCostBadge(VertexHelper vh)
    {
        Vector2 center = new Vector2(30, 250);
        Disc(vh, center + Vector2.down, 24, Hex("#05090D"), Hex("#05090D"));
        Disc(vh, center, 23, Hex("#8C6325"), Hex("#F4D886"));
        Disc(vh, center, 21, Hex("#061122"), Hex("#122133"));
        Disc(vh, center, 19.5f, Hex("#062875"), Hex("#128BE6"));
        Circle(vh, center, 19.5f, .6f, Hex("#A2D9FF"));
    }

    static Vector2[] Points(Rect r, float cut) => new[]{new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut, r.yMin), new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax, r.yMax - cut), new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut, r.yMax), new Vector2(r.xMin, r.yMax - cut), new Vector2(r.xMin, r.yMin + cut)};
    static void Fill(VertexHelper vh, Rect r, float cut, Color bottom, Color top)
    {
        var p = Points(r, cut);
        for (int i = 0; i < 8; i++)
            Tri(vh, r.center, p[i], p[(i + 1) % 8], Color.Lerp(bottom, top, .5f), Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, p[i].y)), Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, p[(i + 1) % 8].y)));
    }

    Color BorderColor(Vector2 p, Color c)
    {
        if (rarity == CardRarity.SuperRare)
            return Color.Lerp(Hex("#67E8F9"), Hex("#A855F7"), Mathf.Clamp01(.5f + (p.x / 200 - p.y / 280) * .8f));
        return Color.Lerp(c, Color.white, Mathf.Clamp01(p.y / 280) * .40f);
    }

    void Ring(VertexHelper vh, Rect r, float cut, float width, Color c, bool metallic = false)
    {
        var outer = Points(r, cut);
        var inner = Points(new Rect(r.x + width, r.y + width, r.width - 2 * width, r.height - 2 * width), Mathf.Max(0, cut - width * .5f));
        for (int i = 0; i < 8; i++)
        {
            int j = (i + 1) % 8;
            Color a = metallic ? BorderColor(outer[i], c) : c, b = metallic ? BorderColor(outer[j], c) : c;
            Tri(vh, outer[i], outer[j], inner[j], a, b, b);
            Tri(vh, outer[i], inner[j], inner[i], a, b, a);
        }
    }

    static void Disc(VertexHelper vh, Vector2 center, float radius, Color bottom, Color top)
    {
        for (int i = 0; i < 64; i++)
        {
            Vector2 a = center + Direction(i) * radius, b = center + Direction(i + 1) * radius;
            Tri(vh, center, a, b, Color.Lerp(bottom, top, .5f), Color.Lerp(bottom, top, (a.y - center.y + radius) / (2 * radius)), Color.Lerp(bottom, top, (b.y - center.y + radius) / (2 * radius)));
        }
    }

    static Vector2 Direction(int i) => new Vector2(Mathf.Cos(i * Mathf.PI / 32), Mathf.Sin(i * Mathf.PI / 32));
    static void Circle(VertexHelper vh, Vector2 c, float radius, float width, Color col)
    {
        for (int i = 0; i < 64; i++)
            Line(vh, c + Direction(i) * radius, c + Direction(i + 1) * radius, width, col);
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * width * .5f;
        Tri(vh, a - n, b - n, b + n, c, c, c);
        Tri(vh, a - n, b + n, a + n, c, c, c);
    }

    static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
    {
        int n = vh.currentVertCount;
        vh.AddVert(a, ca, Vector2.zero);
        vh.AddVert(b, cb, Vector2.zero);
        vh.AddVert(c, cc, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
    }
}
