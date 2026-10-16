using UnityEngine;
using UnityEngine.UI;

/// <summary>Resolution-independent metal frames and fixed HUD symbols.</summary>
[AddComponentMenu("UI/Battle HUD Graphic")]
[RequireComponent(typeof(CanvasRenderer))]
public class BattleHudGraphic : MaskableGraphic
{
    public enum Shape
    {
        Frame,
        CardBack,
        Gem,
        Gear
    }

    public Shape shape;
    public Color metal = new Color(0.78f, 0.61f, 0.3f);
    public Color glow = new Color(0.15f, 0.62f, 1f, 0f);
    public bool ornament = true;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (shape == Shape.Gem)
        {
            Gem(vh, r);
            return;
        }

        if (shape == Shape.Gear)
        {
            Gear(vh, r);
            return;
        }

        float cut = Mathf.Min(16f, r.height * .15f);
        if (glow.a > 0)
            for (int i = 7; i >= 1; i--)
                Ring(vh, Expand(r, i * 1.4f), cut + i, 2, new Color(glow.r, glow.g, glow.b, glow.a * (8 - i) / 45f));
        Fill(vh, r, cut, color);
        Ring(vh, r, cut, 2.4f, metal * new Color(1, 1, 1, color.a));
        Ring(vh, Expand(r, -5), Mathf.Max(2, cut - 3), 1, new Color(metal.r, metal.g, metal.b, .55f * color.a));
        Line(vh, new Vector2(r.xMin + cut + 6, r.yMax - 8), new Vector2(r.xMax - cut - 6, r.yMax - 8), 1, new Color(1, .88f, .62f, .3f * color.a));
        if (ornament)
        {
            foreach (float x in new[]{r.xMin + 12, r.xMax - 12})
                foreach (float y in new[]{r.yMin + 12, r.yMax - 12})
                {
                    Diamond(vh, new Vector2(x, y), 4, metal);
                    float sx = x < r.center.x ? 1 : -1, sy = y < r.center.y ? 1 : -1;
                    Line(vh, new Vector2(x + sx * 5, y), new Vector2(x + sx * 22, y), 1.5f, metal);
                    Line(vh, new Vector2(x, y + sy * 5), new Vector2(x, y + sy * 22), 1.5f, metal);
                    if (r.width > 300 && r.height > 90)
                    {
                        Vector2 origin = new Vector2(x, y);
                        Curl(vh, origin, sx, sy, metal);
                        Curl(vh, origin, sy, sx, metal, true);
                    }
                }
        }

        if (shape == Shape.CardBack)
        {
            Vector2 c = r.center;
            float s = Mathf.Min(r.width * .30f, r.height * .27f);
            Vector2[] d = {c + Vector2.up * s * 1.35f, c + Vector2.right * s, c + Vector2.down * s * 1.35f, c + Vector2.left * s};
            for (int i = 0; i < 4; i++)
                Line(vh, d[i], d[(i + 1) % 4], 2, metal);
            Circle(vh, c, s * .57f, 2, metal, 32);
            Diamond(vh, c, s * .23f, metal);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Line(vh, c + dir * s * .65f, c + dir * s * .84f, 1.5f, metal);
            }
        }
    }

    static Rect Expand(Rect r, float amount) => new Rect(r.xMin - amount, r.yMin - amount, r.width + amount * 2, r.height + amount * 2);
    static Vector2[] Corners(Rect r, float cut) => new[]{new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut, r.yMin), new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax, r.yMax - cut), new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut, r.yMax), new Vector2(r.xMin, r.yMax - cut), new Vector2(r.xMin, r.yMin + cut)};
    static void Fill(VertexHelper vh, Rect r, float cut, Color col)
    {
        Vector2[] p = Corners(r, cut);
        for (int i = 0; i < 8; i++)
            Tri(vh, r.center, p[i], p[(i + 1) % 8], col, Shade(col, p[i].y, r), Shade(col, p[(i + 1) % 8].y, r));
    }

    static Color Shade(Color color, float y, Rect r)
    {
        float light = Mathf.Lerp(.62f, 1.45f, Mathf.InverseLerp(r.yMin, r.yMax, y));
        return new Color(color.r * light, color.g * light, color.b * light, color.a);
    }

    static void Curl(VertexHelper vh, Vector2 origin, float sx, float sy, Color color, bool transpose = false)
    {
        Vector2 previous = Vector2.zero;
        for (int i = 0; i <= 18; i++)
        {
            float t = i / 18f;
            float angle = t * Mathf.PI * 2.1f;
            float radius = Mathf.Lerp(10, 1.5f, t);
            Vector2 local = new Vector2(29 + Mathf.Cos(angle) * radius, 12 + Mathf.Sin(angle) * radius);
            if (transpose)
                local = new Vector2(local.y, local.x);
            Vector2 point = origin + Vector2.Scale(local, new Vector2(transpose ? sy : sx, transpose ? sx : sy));
            if (i > 0)
                Line(vh, previous, point, 1.1f, new Color(color.r, color.g, color.b, .68f));
            previous = point;
        }
    }

    static void Ring(VertexHelper vh, Rect r, float cut, float width, Color col)
    {
        Vector2[] a = Corners(r, cut), b = Corners(Expand(r, -width), Mathf.Max(0, cut - width * .5f));
        for (int i = 0; i < 8; i++)
            Quad(vh, a[i], a[(i + 1) % 8], b[(i + 1) % 8], b[i], col);
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * width * .5f;
        Quad(vh, a - n, b - n, b + n, a + n, col);
    }

    static void Diamond(VertexHelper vh, Vector2 c, float size, Color col) => Quad(vh, c + Vector2.down * size, c + Vector2.right * size, c + Vector2.up * size, c + Vector2.left * size, col);
    static void Circle(VertexHelper vh, Vector2 c, float radius, float width, Color col, int steps)
    {
        for (int i = 0; i < steps; i++)
        {
            float a = i * Mathf.PI * 2 / steps, b = (i + 1) * Mathf.PI * 2 / steps;
            Line(vh, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, width, col);
        }
    }

    void Gem(VertexHelper vh, Rect r)
    {
        Vector2 c = r.center;
        float s = Mathf.Min(r.width, r.height) * .44f;
        for (int i = 0; i < 6; i++)
        {
            float a = (i * 60 + 30) * Mathf.Deg2Rad, b = (i * 60 + 90) * Mathf.Deg2Rad;
            Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s, q = c + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * s;
            Tri(vh, c, p, q, Color.Lerp(color, Color.white, .4f), color, color * .65f);
            Line(vh, p, q, 2, metal);
        }
    }

    void Gear(VertexHelper vh, Rect r)
    {
        Vector2 c = r.center;
        float s = Mathf.Min(r.width, r.height) * .28f;
        Circle(vh, c, s, 5, color, 32);
        Circle(vh, c, s * .37f, 3, color, 24);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Line(vh, c + d * (s - 2), c + d * (s + 7), 6, color);
        }
    }

    static void Tri(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
    {
        int n = vh.currentVertCount;
        vh.AddVert(a, ca, Vector2.zero);
        vh.AddVert(b, cb, Vector2.zero);
        vh.AddVert(c, cc, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
    }

    static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color col)
    {
        int n = vh.currentVertCount;
        vh.AddVert(a, col, Vector2.zero);
        vh.AddVert(b, col, Vector2.zero);
        vh.AddVert(c, col, Vector2.zero);
        vh.AddVert(d, col, Vector2.zero);
        vh.AddTriangle(n, n + 1, n + 2);
        vh.AddTriangle(n, n + 2, n + 3);
    }
}
