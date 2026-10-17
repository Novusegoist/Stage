using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Картонные вырезы и звуки для скримеров — полностью процедурные (никаких файлов не нужно).
/// Вырезы рисуются кодом: контур картона, лицо, зубы; звуки синтезируются (визг, рык, «удар оркестра»).
/// Всё создаётся лениво и кэшируется.
/// </summary>
public static class ScareAssets
{
    public const int VariantCount = 4;
    public const int ClipCount = 3;
    public const float CutoutHeight = 3.4f;     // метров (игрок у вас высокий — вырез должен быть выше него)

    private const int TexW = 160;
    private const int TexH = 320;
    private const float Ppu = TexH / 2f;        // пикселей на «единицу» эскиза (высота эскиза = 2 единицы)
    private const float Rim = 0.035f;           // толщина «картонной» кромки

    private static readonly Dictionary<int, Sprite> Sprites = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();

    private static readonly Color Cardboard = new Color(0.78f, 0.62f, 0.4f);
    private static readonly Color CardboardDark = new Color(0.3f, 0.2f, 0.1f);
    private static readonly Color Black = new Color(0.04f, 0.03f, 0.04f);
    private static readonly Color White = new Color(0.96f, 0.95f, 0.9f);

    // Эскизы-контуры (x: −0.5..0.5, y: 0..2).
    private static readonly float[] GhoulTorso =
    {
        -0.28f, 1.5f,  0.28f, 1.5f,  0.28f, 0.12f,  0.2f, 0f,  0.12f, 0.12f,
        0.04f, 0f,  -0.04f, 0.12f,  -0.12f, 0f,  -0.2f, 0.12f,  -0.28f, 0f
    };
    private static readonly float[] DemonTorso = { -0.34f, 1.25f, 0.34f, 1.25f, 0.22f, 0f, -0.22f, 0f };
    private static readonly float[] GhostBody =
    {
        -0.45f, 1.5f,  0.45f, 1.5f,  0.45f, 0.2f,  0.3f, 0f,  0.15f, 0.2f,
        0f, 0f,  -0.15f, 0.2f,  -0.3f, 0f,  -0.45f, 0.2f
    };
    private static readonly float[] StarShape = BuildStar(14, 0f, 1.35f, 0.48f, 0.3f);

    // ------------------------------------------------------------------ Публичное

    public static Sprite GetSprite(int variant)
    {
        variant = Mathf.Abs(variant) % VariantCount;
        Sprite sprite;
        if (!Sprites.TryGetValue(variant, out sprite) || sprite == null)
        {
            sprite = BuildSprite(variant);
            Sprites[variant] = sprite;
        }
        return sprite;
    }

    public static AudioClip GetClip(int variant)
    {
        variant = Mathf.Abs(variant) % ClipCount;
        AudioClip clip;
        if (!Clips.TryGetValue(variant, out clip) || clip == null)
        {
            clip = BuildClip(variant);
            Clips[variant] = clip;
        }
        return clip;
    }

    // ------------------------------------------------------------------ Картинки

    private static Sprite BuildSprite(int variant)
    {
        Texture2D tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false);
        tex.name = "Cutout" + variant;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[TexW * TexH];
        for (int y = 0; y < TexH; y++)
        {
            for (int x = 0; x < TexW; x++)
            {
                float ux = (x + 0.5f) / TexW - 0.5f;
                float uy = (y + 0.5f) / TexH * 2f;

                float d = Silhouette(variant, ux, uy);
                float rimAlpha = Coverage(d - Rim);
                if (rimAlpha <= 0f)
                {
                    pixels[y * TexW + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // Картонная кромка с тёмной обводкой по внешнему краю.
                Color c = Color.Lerp(Cardboard, CardboardDark, Mathf.Clamp01((d - (Rim - 0.014f)) * Ppu));

                float bodyAlpha = Coverage(d);
                if (bodyAlpha > 0f) c = Color.Lerp(c, Body(variant, ux, uy), bodyAlpha);

                pixels[y * TexW + x] = new Color(c.r, c.g, c.b, rimAlpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        // Низ вырезка = точка опоры, высота ровно CutoutHeight метров.
        return Sprite.Create(tex, new Rect(0f, 0f, TexW, TexH), new Vector2(0.5f, 0f), TexH / CutoutHeight, 0, SpriteMeshType.FullRect);
    }

    private static float Silhouette(int variant, float x, float y)
    {
        switch (variant)
        {
            case 0:   // бледный долговязый
                return Mathf.Min(
                    Mathf.Min(Ellipse(x, y, 0f, 1.55f, 0.26f, 0.3f), Polygon(x, y, GhoulTorso)),
                    Mathf.Min(Box(x, y, -0.37f, 0.95f, 0.07f, 0.55f), Box(x, y, 0.37f, 0.95f, 0.07f, 0.55f)));

            case 1:   // красный демон с рогами
                return Mathf.Min(
                    Mathf.Min(Circle(x, y, 0f, 1.5f, 0.3f), Polygon(x, y, DemonTorso)),
                    Mathf.Min(
                        Triangle(x, y, -0.26f, 1.68f, -0.1f, 1.78f, -0.34f, 1.98f),
                        Triangle(x, y, 0.26f, 1.68f, 0.34f, 1.98f, 0.1f, 1.78f)));

            case 2:   // привидение в простыне
                return Mathf.Min(Polygon(x, y, GhostBody), Circle(x, y, 0f, 1.5f, 0.45f));

            default:  // колючий циклоп
                return Mathf.Min(
                    Polygon(x, y, StarShape),
                    Mathf.Min(Box(x, y, -0.13f, 0.45f, 0.06f, 0.45f), Box(x, y, 0.13f, 0.45f, 0.06f, 0.45f)));
        }
    }

    private static Color Body(int variant, float x, float y)
    {
        Color c;
        switch (variant)
        {
            case 0:
            {
                c = new Color(0.86f, 0.85f, 0.78f);
                Paint(ref c, Black, Ellipse(x, y, -0.1f, 1.58f, 0.065f, 0.1f));
                Paint(ref c, Black, Ellipse(x, y, 0.1f, 1.58f, 0.065f, 0.1f));
                Paint(ref c, new Color(0.9f, 0.1f, 0.1f), Circle(x, y, -0.1f, 1.56f, 0.02f));
                Paint(ref c, new Color(0.9f, 0.1f, 0.1f), Circle(x, y, 0.1f, 1.56f, 0.02f));
                float mouth = Ellipse(x, y, 0f, 1.4f, 0.1f, 0.13f);
                Paint(ref c, Black, mouth);
                Paint(ref c, White, Mathf.Max(mouth, Teeth(x, y, 0f, 0.1f, 4, 1.53f, 1.27f, 0.07f)));
                // кровавые подтёки
                Color blood = new Color(0.55f, 0.04f, 0.04f);
                Paint(ref c, blood, Box(x, y, -0.06f, 1.15f, 0.012f, 0.14f));
                Paint(ref c, blood, Box(x, y, 0.02f, 1.1f, 0.012f, 0.2f));
                Paint(ref c, blood, Box(x, y, 0.08f, 1.2f, 0.012f, 0.1f));
                return c;
            }
            case 1:
            {
                c = new Color(0.62f, 0.07f, 0.07f);
                Paint(ref c, new Color(0.35f, 0.02f, 0.02f), Ellipse(x, y, 0f, 1.12f, 0.3f, 0.08f));   // тень на груди
                Color brow = new Color(0.12f, 0.02f, 0.02f);
                Paint(ref c, brow, EllipseRot(x, y, -0.11f, 1.64f, 0.12f, 0.025f, 0.4f));
                Paint(ref c, brow, EllipseRot(x, y, 0.11f, 1.64f, 0.12f, 0.025f, -0.4f));
                Color eye = new Color(1f, 0.85f, 0.1f);
                Paint(ref c, eye, EllipseRot(x, y, -0.11f, 1.55f, 0.08f, 0.035f, 0.35f));
                Paint(ref c, eye, EllipseRot(x, y, 0.11f, 1.55f, 0.08f, 0.035f, -0.35f));
                Paint(ref c, Black, Ellipse(x, y, -0.11f, 1.55f, 0.012f, 0.03f));
                Paint(ref c, Black, Ellipse(x, y, 0.11f, 1.55f, 0.012f, 0.03f));
                float mouth = Ellipse(x, y, 0f, 1.37f, 0.18f, 0.08f);
                Paint(ref c, Black, mouth);
                Paint(ref c, White, Mathf.Max(mouth, Teeth(x, y, 0f, 0.18f, 6, 1.43f, 1.31f, 0.06f)));
                return c;
            }
            case 2:
            {
                float shade = 0.8f + 0.2f * Mathf.Clamp01(y / 1.4f);
                c = new Color(0.95f * shade, 0.95f * shade, 0.98f * shade);
                Paint(ref c, Black, Ellipse(x, y, -0.15f, 1.55f, 0.07f, 0.12f));
                Paint(ref c, Black, Ellipse(x, y, 0.15f, 1.55f, 0.07f, 0.12f));
                Paint(ref c, Black, Ellipse(x, y, 0f, 1.2f, 0.1f, 0.18f));
                return c;
            }
            default:
            {
                c = new Color(0.36f, 0.2f, 0.56f);
                Paint(ref c, White, Ellipse(x, y, 0f, 1.4f, 0.26f, 0.2f));
                Paint(ref c, new Color(0.9f, 0.08f, 0.08f), Circle(x, y, 0f, 1.4f, 0.12f));
                Paint(ref c, Black, Circle(x, y, 0f, 1.4f, 0.06f));
                Paint(ref c, White, Circle(x, y, -0.03f, 1.43f, 0.015f));
                float mouth = Ellipse(x, y, 0f, 1.05f, 0.22f, 0.07f);
                Paint(ref c, Black, mouth);
                Paint(ref c, White, Mathf.Max(mouth, Teeth(x, y, 0f, 0.22f, 6, 1.11f, 0.99f, 0.055f)));
                return c;
            }
        }
    }

    // Зубы: треугольники сверху вниз и снизу вверх в полосе [bottomY, topY]; быстрый выход, если пиксель далеко.
    private static float Teeth(float x, float y, float cx, float halfWidth, int count, float topY, float bottomY, float height)
    {
        if (Mathf.Abs(x - cx) > halfWidth + 0.04f || y > topY + 0.02f || y < bottomY - 0.02f) return 1f;

        float best = 1f;
        float step = halfWidth * 2f / count;
        float w = step * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float tx = cx - halfWidth + step * (i + 0.5f);
            best = Mathf.Min(best, Triangle(x, y, tx - w, topY, tx + w, topY, tx, topY - height));
            best = Mathf.Min(best, Triangle(x, y, tx - w, bottomY, tx, bottomY + height, tx + w, bottomY));
        }
        return best;
    }

    private static float[] BuildStar(int spikes, float cx, float cy, float outer, float inner)
    {
        float[] pts = new float[spikes * 4];
        for (int i = 0; i < spikes * 2; i++)
        {
            float a = i * Mathf.PI / spikes;
            float r = (i % 2 == 0) ? outer : inner;
            pts[i * 2] = cx + r * Mathf.Sin(a);
            pts[i * 2 + 1] = cy + r * Mathf.Cos(a);
        }
        return pts;
    }

    // ------------------------------------------------------------------ Фигуры (знаковые расстояния; < 0 внутри)

    private static float Coverage(float d)
    {
        return Mathf.Clamp01(0.5f - d * Ppu);
    }

    private static void Paint(ref Color c, Color color, float d)
    {
        float a = Coverage(d);
        if (a > 0f) c = Color.Lerp(c, color, a);
    }

    private static float Circle(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx;
        float dy = y - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - r;
    }

    private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx;
        float dy = (y - cy) / ry;
        return (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
    }

    private static float EllipseRot(float x, float y, float cx, float cy, float rx, float ry, float angle)
    {
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        float dx = x - cx;
        float dy = y - cy;
        return Ellipse(cx + dx * cos + dy * sin, cy - dx * sin + dy * cos, cx, cy, rx, ry);
    }

    private static float Box(float x, float y, float cx, float cy, float hx, float hy)
    {
        float qx = Mathf.Abs(x - cx) - hx;
        float qy = Mathf.Abs(y - cy) - hy;
        float ox = Mathf.Max(qx, 0f);
        float oy = Mathf.Max(qy, 0f);
        return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f);
    }

    private static float Triangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
    {
        float e0x = bx - ax, e0y = by - ay;
        float e1x = cx - bx, e1y = cy - by;
        float e2x = ax - cx, e2y = ay - cy;
        float v0x = px - ax, v0y = py - ay;
        float v1x = px - bx, v1y = py - by;
        float v2x = px - cx, v2y = py - cy;

        float t0 = Mathf.Clamp01((v0x * e0x + v0y * e0y) / (e0x * e0x + e0y * e0y));
        float t1 = Mathf.Clamp01((v1x * e1x + v1y * e1y) / (e1x * e1x + e1y * e1y));
        float t2 = Mathf.Clamp01((v2x * e2x + v2y * e2y) / (e2x * e2x + e2y * e2y));
        float q0x = v0x - e0x * t0, q0y = v0y - e0y * t0;
        float q1x = v1x - e1x * t1, q1y = v1y - e1y * t1;
        float q2x = v2x - e2x * t2, q2y = v2y - e2y * t2;

        float s = Mathf.Sign(e0x * e2y - e0y * e2x);
        float dx = Mathf.Min(Mathf.Min(q0x * q0x + q0y * q0y, q1x * q1x + q1y * q1y), q2x * q2x + q2y * q2y);
        float dy = Mathf.Min(Mathf.Min(s * (v0x * e0y - v0y * e0x), s * (v1x * e1y - v1y * e1x)), s * (v2x * e2y - v2y * e2x));
        return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
    }

    // Многоугольник (вершины подряд x,y,x,y…).
    private static float Polygon(float px, float py, float[] v)
    {
        int n = v.Length / 2;
        float dx0 = px - v[0];
        float dy0 = py - v[1];
        float d = dx0 * dx0 + dy0 * dy0;
        float s = 1f;

        for (int i = 0, j = n - 1; i < n; j = i, i++)
        {
            float ex = v[2 * j] - v[2 * i];
            float ey = v[2 * j + 1] - v[2 * i + 1];
            float wx = px - v[2 * i];
            float wy = py - v[2 * i + 1];
            float t = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
            float bx = wx - ex * t;
            float by = wy - ey * t;
            d = Mathf.Min(d, bx * bx + by * by);

            bool c1 = py >= v[2 * i + 1];
            bool c2 = py < v[2 * j + 1];
            bool c3 = ex * wy > ey * wx;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    // ------------------------------------------------------------------ Звуки

    private static AudioClip BuildClip(int variant)
    {
        const int sampleRate = 44100;
        float duration = variant == 1 ? 1.5f : 1.3f;
        int count = (int)(sampleRate * duration);
        float[] data = new float[count];
        System.Random rng = new System.Random(1234 + variant * 77);

        switch (variant)
        {
            case 0: FillScreech(data, sampleRate, rng); break;
            case 1: FillGrowl(data, sampleRate, rng); break;
            default: FillShatter(data, sampleRate, rng); break;
        }

        // Нормализуем до 0.95 — скример должен быть громким.
        float peak = 0.0001f;
        for (int i = 0; i < count; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float gain = 0.95f / peak;
        for (int i = 0; i < count; i++) data[i] *= gain;

        AudioClip clip = AudioClip.Create("Scare" + variant, count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static float Noise(System.Random rng)
    {
        return (float)(rng.NextDouble() * 2.0 - 1.0);
    }

    // Резкий визг, скользящий вверх, с ударом и низким «бумом».
    private static void FillScreech(float[] data, int sr, System.Random rng)
    {
        double phase = 0.0, phase2 = 0.0, low = 0.0;
        double twoPi = 2.0 * System.Math.PI;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sr;
            float env = (1f - Mathf.Exp(-t * 600f)) * Mathf.Exp(-t * 2.2f);
            float f = 700f + 1300f * Mathf.Min(t / 0.4f, 1f) + 60f * Mathf.Sin((float)(twoPi * 11.0 * t));

            phase += twoPi * f / sr;
            phase2 += twoPi * (f * 1.51f) / sr;
            low += twoPi * 55.0 / sr;

            float saw = (float)((phase / twoPi) % 1.0) * 2f - 1f;
            float square = (float)System.Math.Sign(System.Math.Sin(phase2));
            float screech = (saw * 0.5f + square * 0.25f + (float)System.Math.Sin(phase * 2.0) * 0.2f) * env;
            float hit = Noise(rng) * Mathf.Exp(-t * 16f);
            float boom = (float)System.Math.Sin(low) * Mathf.Exp(-t * 4f);

            data[i] = (float)System.Math.Tanh((screech + hit * 0.9f + boom * 0.9f) * 1.8f);
        }
    }

    // Низкий рык с быстрой амплитудной модуляцией и диссонансным кластером.
    private static void FillGrowl(float[] data, int sr, System.Random rng)
    {
        double phase = 0.0;
        double twoPi = 2.0 * System.Math.PI;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sr;
            float env = (1f - Mathf.Exp(-t * 300f)) * Mathf.Exp(-t * 1.6f);
            float f0 = 80f + 25f * (float)System.Math.Sin(twoPi * 4.0 * t);
            phase += twoPi * f0 / sr;

            float saw = (float)((phase / twoPi) % 1.0) * 2f - 1f;
            float am = 0.55f + 0.45f * (float)System.Math.Sin(twoPi * 42.0 * t);
            float growl = saw * am * env;

            float cluster = ((float)System.Math.Sin(twoPi * 311.0 * t) + (float)System.Math.Sin(twoPi * 330.0 * t)
                             + 0.6f * (float)System.Math.Sin(twoPi * 622.0 * t) + 0.6f * (float)System.Math.Sin(twoPi * 660.0 * t)) * 0.25f * env;
            float hit = Noise(rng) * Mathf.Exp(-t * 20f);

            data[i] = (float)System.Math.Tanh((growl * 1.4f + cluster * 0.8f + hit) * 1.6f);
        }
    }

    // «Удар оркестра»: высокочастотный шум, тревожный аккорд и бум.
    private static void FillShatter(float[] data, int sr, System.Random rng)
    {
        double twoPi = 2.0 * System.Math.PI;
        float previous = 0f;

        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)sr;
            float n = Noise(rng);
            float highPass = n - previous;
            previous = n;

            float env = Mathf.Exp(-t * 5f) * (1f - Mathf.Exp(-t * 800f));
            float chord = ((float)System.Math.Sign(System.Math.Sin(twoPi * 220.0 * t))
                           + (float)System.Math.Sign(System.Math.Sin(twoPi * 233.0 * t))
                           + (float)System.Math.Sign(System.Math.Sin(twoPi * 311.0 * t))
                           + (float)System.Math.Sign(System.Math.Sin(twoPi * 330.0 * t))) * 0.12f * Mathf.Exp(-t * 3f);
            float boom = (float)System.Math.Sin(twoPi * 50.0 * t) * Mathf.Exp(-t * 4.5f);

            data[i] = (float)System.Math.Tanh((highPass * 0.9f * env + chord + boom * 0.9f) * 1.5f);
        }
    }
}
