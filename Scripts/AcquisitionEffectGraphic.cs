using UnityEngine;
using UnityEngine.UI;

/// <summary>画像素材不要の光、光芒、粒子。ゲームの乱数状態は変更しません。</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AcquisitionEffectGraphic : MaskableGraphic
{
    public enum Mode { Background, Opening }
    public const float OpeningCutHeight = 244f;
    public Mode mode;
    public float Cut { get; set; }
    public float Intensity { get; set; }
    private static readonly Color Gold = new Color(1, .72f, .28f, 1);
    private static Texture2D softLight;
    private static readonly Vector2 SolidUV = new Vector2(127.5f / 128, .5f);

    // Texture interpolation keeps very faint light smooth instead of quantizing
    // its falloff into the mesh's eight-bit vertex alpha values.
    public override Texture mainTexture
    {
        get
        {
            if (softLight != null) return softLight;
            const int size = 128;
            softLight = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            softLight.name = "AcquisitionSoftLight";
            softLight.hideFlags = HideFlags.HideAndDontSave;
            softLight.wrapMode = TextureWrapMode.Clamp;
            softLight.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2(x / 125f * 2 - 1, y / 127f * 2 - 1).magnitude;
                    float alpha = x == 127 ? 1 : Mathf.Pow(Mathf.Clamp01(1 - radius), 2);
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            softLight.SetPixels32(pixels);
            softLight.Apply(false, true);
            return softLight;
        }
    }

    private static Vector2 SoftUV(float x, float y) => new Vector2((.5f + 125 * x) / 128, (.5f + 127 * y) / 128);

    private void Update() { SetVerticesDirty(); }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (mode == Mode.Opening) DrawOpening(vh);
        else DrawBackground(vh);
    }

    private void DrawOpening(VertexHelper vh)
    {
        Vector2 slit = new Vector2(0, OpeningCutHeight);
        float end = Mathf.Lerp(-164, 164, Cut);
        float width = (end + 164) * .5f;
        Vector2 center = new Vector2(-164 + width, slit.y);
        if (width > 0)
        {
            Glow(vh, center, new Vector2(width + 8, 12 + Intensity * 55), Alpha(Gold, .6f));
            Quad(vh, new Rect(-164, slit.y - 1.5f, width * 2, 3), Color.white);
        }
        if (Cut < 1)
        {
            Glow(vh, new Vector2(end, slit.y), new Vector2(35, 20), Color.white);
            Glow(vh, new Vector2(end, slit.y), new Vector2(4, 60), Alpha(Gold, .8f));
        }
        if (Intensity <= 0) return;
        for (int i = 0; i < 11; i++)
        {
            float angle = Mathf.Lerp(22, 158, i / 10f) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 origin = slit + new Vector2(Mathf.Lerp(-125, 125, i / 10f), 0);
            float length = (300 + 180 * Hash(i + 3)) * Intensity;
            Beam(vh, origin, direction, length, 45 + 70 * Intensity,
                Alpha(Gold, Intensity * .27f));
        }
        Glow(vh, slit, new Vector2(220 + 240 * Intensity, 80 + 220 * Intensity), Alpha(Gold, Intensity * .65f));
        Glow(vh, slit, new Vector2(160, 15 + 55 * Intensity), Alpha(Color.white, Intensity));
    }

    private void DrawBackground(VertexHelper vh)
    {
        Rect r = rectTransform.rect;
        float unit = Mathf.Min(r.width / 1600, r.height / 900);
        float time = Time.unscaledTime;
        Quad(vh, r, new Color(.012f, .023f, .06f, 1));
        Glow(vh, r.center, new Vector2(r.width * .65f, r.height * .8f), new Color(.065f, .13f, .23f, .9f));
        Vector2 origin = r.center + Vector2.down * r.height * .08f;
        for (int i = 0; i < 16; i++)
        {
            float angle = (i * 22.5f + time * 1.4f) * Mathf.Deg2Rad;
            Beam(vh, origin, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                r.size.magnitude * .7f, (65 + 80 * Hash(i)) * unit,
                Alpha(Gold, .012f + .006f * Mathf.Sin(time * .4f + i)));
        }
        Glow(vh, r.center + Vector2.up * r.height * .42f,
            new Vector2(r.width * .45f, r.height * .28f), Alpha(Gold, .055f));
        Glow(vh, r.center - Vector2.up * r.height * .5f,
            new Vector2(r.width * .5f, r.height * .22f), Alpha(Gold, .045f));
        for (int i = 0; i < 72; i++)
        {
            float x = Mathf.Lerp(r.xMin, r.xMax, Hash(i * 3 + 1));
            float y = Mathf.Lerp(r.yMin, r.yMax, Mathf.Repeat(Hash(i * 3 + 2) + time * (.006f + .008f * Hash(i)), 1));
            x += Mathf.Sin(time * .3f + i) * 12 * unit;
            float size = (1.2f + 2.4f * Hash(i * 3 + 3)) * unit;
            float alpha = .25f + .4f * (.5f + .5f * Mathf.Sin(time * 1.2f + i * 2));
            Vector2 p = new Vector2(x, y);
            Glow(vh, p, Vector2.one * size * 4, Alpha(Gold, alpha * .24f));
            Glow(vh, p, new Vector2(size, size * 1.4f), Alpha(Gold, alpha));
            if (i % 9 == 0)
            {
                Glow(vh, p, new Vector2(size * 6, size * .45f), Alpha(Gold, alpha * .55f));
                Glow(vh, p, new Vector2(size * .45f, size * 6), Alpha(Gold, alpha * .55f));
            }
        }
    }

    private static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 127.1f + 31.7f) * 43758.5453f, 1);
    private static Color Alpha(Color c, float a) { c.a = a; return c; }

    private static void Glow(VertexHelper vh, Vector2 p, Vector2 radius, Color c)
    {
        int start = vh.currentVertCount;
        vh.AddVert(p + new Vector2(-radius.x, -radius.y), c, SoftUV(0, 0));
        vh.AddVert(p + new Vector2(radius.x, -radius.y), c, SoftUV(1, 0));
        vh.AddVert(p + new Vector2(radius.x, radius.y), c, SoftUV(1, 1));
        vh.AddVert(p + new Vector2(-radius.x, radius.y), c, SoftUV(0, 1));
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void Beam(VertexHelper vh, Vector2 origin, Vector2 direction, float length, float width, Color c)
    {
        Vector2 side = new Vector2(-direction.y, direction.x) * width;
        int start = vh.currentVertCount;
        const int rows = 8, columns = 6;
        for (int row = 0; row <= rows; row++)
        {
            float t = row / (float)rows;
            for (int col = 0; col <= columns; col++)
            {
                float across = col / (float)columns;
                vh.AddVert(origin + direction * (length * t) + side * ((across * 2 - 1) * t),
                    c, SoftUV(across, .5f + .5f * t));
                if (row == 0 || col == 0) continue;
                int a = start + row * (columns + 1) + col;
                int b = a - columns - 1;
                vh.AddTriangle(a - 1, b - 1, b);
                vh.AddTriangle(a - 1, b, a);
            }
        }
    }

    private static void Quad(VertexHelper vh, Rect r, Color c)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector2(r.xMin, r.yMin), c, SolidUV);
        vh.AddVert(new Vector2(r.xMax, r.yMin), c, SolidUV);
        vh.AddVert(new Vector2(r.xMax, r.yMax), c, SolidUV);
        vh.AddVert(new Vector2(r.xMin, r.yMax), c, SolidUV);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
