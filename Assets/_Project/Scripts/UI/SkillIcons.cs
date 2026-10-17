using System;
using System.Collections.Generic;
using UnityEngine;

namespace Survivors
{
    // Placeholder skill icons drawn procedurally. Replaced by painted icons with the real art.
    public static class SkillIcons
    {
        const int Size = 64;
        static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string id)
        {
            Texture2D t;
            if (_cache.TryGetValue(id, out t) && t != null) return t;
            t = Build(id);
            _cache[id] = t;
            return t;
        }

        static Texture2D Make(Func<float, float, Color> f)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int py = 0; py < Size; py++)
                for (int px = 0; px < Size; px++)
                {
                    float x = (px + 0.5f) / Size * 2f - 1f;
                    float y = (py + 0.5f) / Size * 2f - 1f;
                    tex.SetPixel(px, py, f(x, y));
                }
            tex.Apply();
            return tex;
        }

        static bool Disc(float x, float y, float cx, float cy, float r)
        {
            return (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        }

        static bool Ring(float x, float y, float r, float w)
        {
            float d = Mathf.Sqrt(x * x + y * y);
            return d >= r - w && d <= r + w;
        }

        static bool Box(float x, float y, float x0, float y0, float x1, float y1)
        {
            return x >= x0 && x <= x1 && y >= y0 && y <= y1;
        }

        static Texture2D Build(string id)
        {
            Color clear = new Color(0, 0, 0, 0);
            switch (id)
            {
                case "bolt":
                    return Make((x, y) =>
                    {
                        if (Disc(x, y, 0.3f, 0.3f, 0.42f)) return new Color(1f, 0.85f, 0.3f);
                        if (Mathf.Abs(x - y) < 0.12f && x < 0f) return new Color(1f, 0.85f, 0.3f, 0.5f);
                        return clear;
                    });
                case "orbit":
                    return Make((x, y) =>
                    {
                        if (Disc(x, y, 0f, 0f, 0.16f)) return new Color(0.3f, 0.45f, 0.85f);
                        if (Disc(x, y, 0f, 0.7f, 0.2f) || Disc(x, y, 0f, -0.7f, 0.2f) || Disc(x, y, 0.7f, 0f, 0.2f)) return new Color(0.6f, 0.9f, 1f);
                        if (Ring(x, y, 0.7f, 0.03f)) return new Color(0.6f, 0.9f, 1f, 0.5f);
                        return clear;
                    });
                case "pulse":
                    return Make((x, y) =>
                    {
                        if (Disc(x, y, 0f, 0f, 0.12f)) return new Color(1f, 0.98f, 0.85f);
                        if (Ring(x, y, 0.35f, 0.07f) || Ring(x, y, 0.62f, 0.06f) || Ring(x, y, 0.88f, 0.05f)) return new Color(1f, 0.95f, 0.6f);
                        return clear;
                    });
                case "might":
                    return Make((x, y) =>
                    {
                        if (Box(x, y, -0.1f, -0.2f, 0.1f, 0.9f)) return new Color(0.9f, 0.92f, 0.97f);
                        if (Box(x, y, -0.4f, -0.3f, 0.4f, -0.15f)) return new Color(0.88f, 0.72f, 0.29f);
                        if (Box(x, y, -0.07f, -0.85f, 0.07f, -0.3f)) return new Color(0.54f, 0.35f, 0.17f);
                        return clear;
                    });
                case "haste":
                    return Make((x, y) =>
                    {
                        // two right-pointing chevrons
                        float a = Mathf.Abs(y) * 0.9f;
                        bool c1 = Mathf.Abs(x + 0.35f - (0.6f - a)) < 0.12f && Mathf.Abs(y) < 0.7f;
                        bool c2 = Mathf.Abs(x - 0.15f - (0.6f - a)) < 0.12f && Mathf.Abs(y) < 0.7f;
                        return (c1 || c2) ? new Color(0.44f, 0.88f, 0.69f) : clear;
                    });
                case "boots":
                    return Make((x, y) =>
                    {
                        if (Box(x, y, -0.5f, -0.7f, 0.8f, -0.35f)) return new Color(0.45f, 0.3f, 0.13f);
                        if (Box(x, y, -0.5f, -0.35f, 0.1f, 0.8f) || Box(x, y, 0.1f, -0.35f, 0.7f, -0.05f)) return new Color(0.79f, 0.54f, 0.29f);
                        return clear;
                    });
                case "magnet":
                    return Make((x, y) =>
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        bool arc = y < 0.1f && d >= 0.35f && d <= 0.75f;
                        bool legs = y >= 0.1f && y <= 0.8f && Mathf.Abs(Mathf.Abs(x) - 0.55f) <= 0.2f;
                        if (y > 0.55f && y <= 0.85f && Mathf.Abs(Mathf.Abs(x) - 0.55f) <= 0.2f) return new Color(0.93f, 0.95f, 0.97f);
                        return (arc || legs) ? new Color(1f, 0.35f, 0.35f) : clear;
                    });
                case "vital":
                    return Make((x, y) =>
                    {
                        float hx = x * 1.25f, hy = -y * 1.25f + 0.1f;
                        float a = hx * hx + hy * hy - 1f;
                        return (a * a * a - hx * hx * hy * hy * hy < 0f) ? new Color(1f, 0.42f, 0.54f) : clear;
                    });
                default: // heal
                    return Make((x, y) =>
                    {
                        if (Disc(x, y, 0f, 0f, 0.9f))
                        {
                            bool plus = Box(x, y, -0.15f, -0.55f, 0.15f, 0.55f) || Box(x, y, -0.55f, -0.15f, 0.55f, 0.15f);
                            return plus ? new Color(0.92f, 1f, 0.95f) : new Color(0.17f, 0.54f, 0.35f);
                        }
                        return clear;
                    });
            }
        }
    }
}
