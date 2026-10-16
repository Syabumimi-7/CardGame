using UnityEngine;
using UnityEngine.UI;

/// <summary>ButtonUI.png: beveled gold frame and stone, drawn entirely with UGUI vertices.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class ButtonDesignGraphic : MaskableGraphic
{
    [Range(0, 1)]
    public float hover;
    [Range(0, 1)]
    public float pressed;
    [Range(0, 1)]
    public float disabled;
    static Color Hex(string s)
    {
        ColorUtility.TryParseHtmlString(s, out var c);
        return c;
    }

    static readonly Color DarkGold = Hex("#8B7355"), LightGold = Hex("#D4B483"), Light = Hex("#FFE5B3");
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        if (r.width < 4 || r.height < 4)
            return;
        float unit = Mathf.Min(r.height, 80) / 80f;
        float cut = Mathf.Min(r.height * .27f, r.width * .16f);
        float edge = Mathf.Max(2, 6 * unit);
        float glow = hover * (1 - disabled) * (1 - pressed * .8f);
        for (int i = 8; i >= 1; i--)
            Ring(vh, Expand(r, i * unit), cut + i * unit, 1.3f * unit, new Color(1, .69f, .23f, glow * (9 - i) * .024f), new Color(1, .83f, .46f, glow * (9 - i) * .024f));
        Rect shadow = r;
        shadow.position += new Vector2(0, -3 * unit);
        Fill(vh, shadow, cut, new Color(0, 0, 0, .6f), new Color(0, 0, 0, .6f));
        r.position += Vector2.down * (pressed * 2 * unit);
        Fill(vh, r, cut, Tone(Hex("#191710")), Tone(Hex("#322A20")));
        Ring(vh, Expand(r, -1 * unit), cut, edge, Tone(DarkGold * .65f), Tone(LightGold));
        Ring(vh, Expand(r, -2 * unit), cut - 1 * unit, .85f * unit, Tone(DarkGold), Tone(Light));
        Ring(vh, Expand(r, -edge - 1 * unit), cut - edge * .5f, 1.5f * unit, Tone(Hex("#241C13")), Tone(Hex("#F0CE94")));
        Rect stone = Expand(r, -edge - 3 * unit);
        Fill(vh, stone, Mathf.Max(2, cut - edge), Tone(Hex("#262728")), Tone(Hex("#404440")));
        Stone(vh, stone, unit);
        Ring(vh, stone, Mathf.Max(2, cut - edge), 1 * unit, Tone(DarkGold), Tone(LightGold));
        Ring(vh, Expand(stone, -3 * unit), Mathf.Max(1, cut - edge - 2 * unit), .7f * unit, Tone(DarkGold * .65f), Tone(DarkGold));
        float cx = r.center.x;
        foreach (float y in new[]{r.yMin + edge * .5f, r.yMax - edge * .5f})
        {
            Diamond(vh, new Vector2(cx, y), 4.3f * unit, Tone(Hex("#392B1C")));
            Diamond(vh, new Vector2(cx, y), 2.5f * unit, Tone(LightGold));
            Line(vh, new Vector2(cx - 3 * unit, y), new Vector2(cx, y + 4 * unit), .8f * unit, Tone(Light));
        }

        // Subtle cool light along the lower edge, as in the reference.
        Line(vh, new Vector2(cx - r.width * .17f, r.yMin + edge + 2 * unit), new Vector2(cx + r.width * .17f, r.yMin + edge + 2 * unit), 1.3f * unit, Tone(new Color(.38f, .70f, .78f, .48f + glow * .3f)));
        foreach (float x in new[]{stone.xMin + 3 * unit, stone.xMax - 3 * unit})
            Diamond(vh, new Vector2(x, stone.center.y), 2 * unit, Tone(LightGold));
    }

    Color Tone(Color c)
    {
        float gray = c.grayscale;
        Color muted = new Color(gray * .7f, gray * .7f, gray * .7f, c.a);
        c = Color.Lerp(c, muted, disabled * .9f);
        float brightness = 1 + hover * .18f * (1 - disabled) - pressed * .18f;
        return new Color(c.r * brightness, c.g * brightness, c.b * brightness, c.a);
    }

    void Stone(VertexHelper vh, Rect r, float unit)
    {
        // Fixed irregular seams stay stable between frames and do not consume the game's RNG.
        for (int i = 0; i < 7; i++)
        {
            float u = (i + 1) / 8f;
            float x = Mathf.Lerp(r.xMin + r.height * .18f, r.xMax - r.height * .18f, u);
            float y = r.yMin + r.height * (.3f + (i % 3) * .16f);
            float dx = (i % 2 == 0 ? 1 : -1) * Mathf.Min(r.width * .026f, r.height * .17f);
            var a = new Vector2(x - dx, r.yMin + 3 * unit);
            var b = new Vector2(x, y);
            var c = new Vector2(x + dx * .65f, y + r.height * .15f);
            var d = new Vector2(x - dx * .4f, r.yMax - 3 * unit);
            Color shade = Tone(new Color(.075f, .072f, .065f, .52f));
            Line(vh, a, b, .9f * unit, shade);
            Line(vh, b, c, .9f * unit, shade);
            Line(vh, c, d, .9f * unit, shade);
            Line(vh, b, b + new Vector2(dx * 1.2f, r.height * .08f), .7f * unit, shade);
            Line(vh, b + Vector2.right * unit, c + Vector2.right * unit, .5f * unit, Tone(new Color(.55f, .52f, .45f, .13f)));
        }
    }

    static Rect Expand(Rect r, float n) => new Rect(r.x - n, r.y - n, r.width + 2 * n, r.height + 2 * n);
    static Vector2[] Points(Rect r, float cut)
    {
        cut = Mathf.Clamp(cut, 0, Mathf.Min(r.width, r.height) * .45f);
        return new[]{new Vector2(r.xMin + cut, r.yMin), new Vector2(r.xMax - cut, r.yMin), new Vector2(r.xMax, r.yMin + cut), new Vector2(r.xMax, r.yMax - cut), new Vector2(r.xMax - cut, r.yMax), new Vector2(r.xMin + cut, r.yMax), new Vector2(r.xMin, r.yMax - cut), new Vector2(r.xMin, r.yMin + cut)};
    }

    static Color At(Color bottom, Color top, Vector2 p, Rect r) => Color.Lerp(bottom, top, Mathf.InverseLerp(r.yMin, r.yMax, p.y));
    static void Fill(VertexHelper vh, Rect r, float cut, Color bottom, Color top)
    {
        var p = Points(r, cut);
        for (int i = 0; i < 8; i++)
            Tri(vh, r.center, p[i], p[(i + 1) % 8], Color.Lerp(bottom, top, .5f), At(bottom, top, p[i], r), At(bottom, top, p[(i + 1) % 8], r));
    }

    static void Ring(VertexHelper vh, Rect r, float cut, float width, Color bottom, Color top)
    {
        var a = Points(r, cut);
        var b = Points(Expand(r, -width), Mathf.Max(0, cut - width * .5f));
        for (int i = 0; i < 8; i++)
        {
            int j = (i + 1) % 8;
            Color ca = At(bottom, top, a[i], r), cb = At(bottom, top, a[j], r);
            Tri(vh, a[i], a[j], b[j], ca, cb, cb);
            Tri(vh, a[i], b[j], b[i], ca, cb, ca);
        }
    }

    static void Diamond(VertexHelper vh, Vector2 c, float s, Color col)
    {
        Tri(vh, c + Vector2.left * s, c + Vector2.up * s, c + Vector2.right * s, col, col, col);
        Tri(vh, c + Vector2.left * s, c + Vector2.right * s, c + Vector2.down * s, col, col, col);
    }

    static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color col)
    {
        Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
        Tri(vh, a - n, b - n, b + n, col, col, col);
        Tri(vh, a - n, b + n, a + n, col, col, col);
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
