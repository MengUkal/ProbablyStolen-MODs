using UnityEngine;

// ============================================================================
// 像素 K 线/走势图：运行时生成 Texture2D（Point 采样，贴合游戏像素风）
// ============================================================================

public static class ChartTexture
{
    public static Sprite Make(float[] series, bool up, out Texture2D tex,
        int w = 110, int h = 36)
    {
        tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color bg = new Color(0.05f, 0.07f, 0.06f, 0.95f);
        Color grid = new Color(1f, 1f, 1f, 0.06f);
        Color line = up
            ? new Color(0.30f, 0.90f, 0.45f, 1f)
            : new Color(0.95f, 0.36f, 0.36f, 1f);
        Color mark = up
            ? new Color(0.70f, 1f, 0.75f, 1f)
            : new Color(1f, 0.70f, 0.70f, 1f);

        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++)
        {
            px[i] = bg;
        }

        // ---- 水平参考线（1/4、2/4、3/4 高度）----
        for (int y = h / 4; y < h; y += h / 4)
        {
            for (int x = 0; x < w; x++)
            {
                px[y * w + x] = grid;
            }
        }
        // ---- 垂直参考线（每 1/6 宽度）----
        for (int x = w / 6; x < w; x += w / 6)
        {
            for (int y = 0; y < h; y++)
            {
                px[y * w + x] = grid;
            }
        }

        // ---- 走势折线 ----
        int n = series != null ? series.Length : 0;
        if (n >= 2)
        {
            float min = series[0], max = series[0];
            for (int k = 1; k < n; k++)
            {
                if (series[k] < min) min = series[k];
                if (series[k] > max) max = series[k];
            }
            float span = Mathf.Max(0.001f, (max - min) * 1.1f); // 5% 上下留白
            float mid = (min + max) * 0.5f;
            int prevY = -1;

            for (int x = 0; x < w; x++)
            {
                int k = n <= 1 ? 0 : (x * (n - 1)) / (w - 1);
                float t = (series[k] - mid) / span + 0.5f; // 0..1
                int y = Mathf.Clamp(1 + Mathf.RoundToInt(t * (h - 3)), 0, h - 1);

                if (prevY >= 0)
                {
                    // 竖向补线，避免斜率大时断点
                    int from = Mathf.Min(prevY, y);
                    int to = Mathf.Max(prevY, y);
                    for (int yy = from; yy <= to; yy++)
                    {
                        px[yy * w + x] = line;
                    }
                }
                else
                {
                    px[y * w + x] = line;
                }
                prevY = y;

                if (x == w - 1)
                {
                    px[y * w + x] = mark; // 最新价亮点
                }
            }
        }

        tex.SetPixels(px);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    public static void Destroy(ref Texture2D tex)
    {
        if (tex != null)
        {
            Object.Destroy(tex);
            tex = null;
        }
    }
}
