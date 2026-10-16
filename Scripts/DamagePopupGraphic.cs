using UnityEngine;
using UnityEngine.UI;

/// <summary>Procedural slash, impact, magic, smoke, blood and lightning meshes; no image assets.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class DamagePopupGraphic : MaskableGraphic
{
    public int style;
    public float progress;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Color bright = Color.Lerp(color, Color.white, .4f);
        if (style == 7)
        {
            // Soft recovery light with rising motes; deliberately no plus symbols.
            for (int i = 5; i >= 0; i--)
                Disk(vh, Vector2.zero, 30 + i * 9, WithAlpha(color, .04f + (5 - i) * .012f));
            for (int i = 0; i < 32; i++)
            {
                float a = i * Mathf.PI / 16 + progress * 1.2f;
                Line(vh, Orbit(a, 88), Orbit(a + .13f, 88), 2, WithAlpha(bright, .65f));
            }

            for (int i = 0; i < 14; i++)
            {
                Vector2 point = new Vector2(Mathf.Sin(i * 2.4f) * 83, Mathf.Repeat(i * 19 + progress * 100, 130) - 65);
                Disk(vh, point, 2 + i % 3, WithAlpha(bright, .8f));
            }
        }
        else if (style == 8 || style == 9)
        {
            Shield(vh, bright);
        }
        else if (style == 10)
        {
            Disk(vh, Vector2.zero, 36, WithAlpha(color, .3f));
            for (int i = 0; i < 15; i++)
            {
                float a = i * Mathf.PI * 2 / 15;
                Vector2 tip = Direction(a) * (i % 3 == 0 ? 91 : 68);
                Triangle(vh, Direction(a - .13f) * 28, tip, Direction(a + .13f) * 28, WithAlpha(color, .9f));
                Line(vh, Direction(a) * 36, tip, 1.5f, bright);
                Disk(vh, tip * 1.13f, 1.5f, WithAlpha(color, .7f));
            }
        }
        else if (style == 11)
        {
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6 + progress * .12f;
                Vector2 dir = Direction(a);
                Vector2 side = new Vector2(-dir.y, dir.x);
                // Rays converge inward to distinguish self damage from an external hit.
                float outer = 95 - progress * 18;
                Triangle(vh, dir * outer - side * 7, dir * 28, dir * outer + side * 7, WithAlpha(color, .8f));
                Disk(vh, dir * (outer + 8), 2 + i % 3, WithAlpha(color, .7f));
            }

            Disk(vh, Vector2.zero, 42, WithAlpha(color, .18f));
        }
        else if (style == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 a = new Vector2(-98 + i * 13, -64), b = new Vector2(95, 64 - i * 12);
                Line(vh, a, b, 12 - i * 3, WithAlpha(color, .3f));
                Line(vh, a, b, 3, bright);
            }
        }
        else if (style == 2)
        {
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i < 40; i++)
                {
                    float a = i * Mathf.PI * 2 / 40, b = (i + 1) * Mathf.PI * 2 / 40;
                    Line(vh, Orbit(a, 82 - ring * 9), Orbit(b, 82 - ring * 9), ring == 0 ? 3 : 1.5f, WithAlpha(ring == 0 ? bright : color, .8f - ring * .2f));
                }

            for (int i = 0; i < 8; i++)
                Disk(vh, Orbit(i * .83f, 93), 2.5f, bright);
        }
        else if (style == 4)
        {
            for (int i = 0; i < 18; i++)
            {
                Vector2 center = new Vector2(Mathf.Sin(i * 2.4f) * 65, Mathf.Cos(i * 1.7f) * 42);
                Disk(vh, center, 22 + i % 4 * 5, WithAlpha(color, .11f));
                Disk(vh, center * 1.35f, 2 + i % 3, WithAlpha(bright, .65f));
            }
        }
        else if (style == 6)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector2 dir = Direction(i * Mathf.PI / 4 + .13f);
                Vector2 side = new Vector2(-dir.y, dir.x);
                Vector2 a = dir * 25;
                for (int j = 1; j <= 4; j++)
                {
                    Vector2 b = dir * (25 + j * 17) + side * (j % 2 == 0 ? -9 : 9);
                    Line(vh, a, b, 7, WithAlpha(color, .22f));
                    Line(vh, a, b, 2, bright);
                    a = b;
                }
            }
        }
        else
        {
            int rays = style == 1 ? 14 : 11;
            for (int i = 0; i < rays; i++)
            {
                float angle = i * Mathf.PI * 2 / rays;
                Vector2 tip = Direction(angle) * (i % 2 == 0 ? 91 : 62);
                Triangle(vh, Direction(angle - .22f) * 26, tip, Direction(angle + .22f) * 26, WithAlpha(color, .8f));
                Line(vh, Direction(angle) * 42, tip * 1.14f, 1.5f, bright);
                if (style == 5)
                    Disk(vh, tip * 1.12f + new Vector2(4, -3), 2 + i % 4, color);
            }

            Disk(vh, Vector2.zero, 39, WithAlpha(color, .3f));
        }
    }

    void Shield(VertexHelper vh, Color bright)
    {
        Vector2[] points = {new Vector2(0, 69), new Vector2(58, 49), new Vector2(52, -18), new Vector2(30, -50), new Vector2(0, -73), new Vector2(-30, -50), new Vector2(-52, -18), new Vector2(-58, 49)};
        float fracture = style == 9 ? Mathf.Clamp01((progress - .12f) / .25f) : 0;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Length];
            Vector2 shift = ((a + b) * .5f).normalized * fracture * (20 + i % 3 * 9);
            Color fill = WithAlpha(color, .5f * (1 - fracture * .55f));
            Triangle(vh, shift, a + shift, b + shift, fill);
            Line(vh, a + shift, b + shift, 8, WithAlpha(color, .4f));
            Line(vh, a + shift, b + shift, 3, bright);
            Line(vh, a * .85f + shift, b * .85f + shift, 1, WithAlpha(bright, .65f));
            if (style == 9 && fracture > 0)
            {
                Line(vh, shift, a + shift, 1.8f, WithAlpha(bright, fracture));
                Vector2 chip = a + shift * 1.8f;
                Triangle(vh, chip, chip + new Vector2(7, 3), chip + new Vector2(2, 10), WithAlpha(color, fracture));
            }
        }

        if (style == 8)
            for (int i = 0; i < 32; i++)
                Line(vh, Orbit(i * Mathf.PI / 16, 85), Orbit((i + 1) * Mathf.PI / 16, 85), 1, WithAlpha(bright, .4f));
    }

    static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    static Vector2 Orbit(float angle, float radius) => new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .65f + Mathf.Cos(angle) * 22);
    static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);
    static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        int index = vh.currentVertCount;
        vh.AddVert(a, tint, Vector2.zero);
        vh.AddVert(b, tint, Vector2.zero);
        vh.AddVert(c, tint, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
    {
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
        Triangle(vh, a - n, b - n, b + n, tint);
        Triangle(vh, a - n, b + n, a + n, tint);
    }

    static void Disk(VertexHelper vh, Vector2 center, float radius, Color tint)
    {
        for (int i = 0; i < 16; i++)
            Triangle(vh, center, center + Direction(i * Mathf.PI / 8) * radius, center + Direction((i + 1) * Mathf.PI / 8) * radius, tint);
    }
}
