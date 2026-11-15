using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// White sprites of the Aztec inspired UI, drawn in code and saved as PNG files in <see cref="Folder"/>:
    /// panels with stepped corners, rings, ticks, the stepped pyramid band, the turquoise mosaic, the stepped spiral
    /// and the sun stone. They are tinted by the Image color. A missing file is drawn again; delete a file to redraw it.
    /// </summary>
    internal static class AztecUiSprites
    {
        public const string Folder = "Assets/_Project/Ui/Sprites";

        private const float Step = 4f;
        private const float Notch = 12f;

        public static Sprite Panel => Get("Panel", 64, 64, (x, y) => IsInsideStepped(x, y, 64, 64, 0f) ? 1f : 0f, new Vector4(16f, 16f, 16f, 16f));

        public static Sprite PanelFrame => Get("PanelFrame", 64, 64, PanelFrameAlpha, new Vector4(16f, 16f, 16f, 16f));

        public static Sprite Ring => Get("Ring", 256, 256, (x, y) => Annulus(Distance(x, y, 128f), 102f, 124f), Vector4.zero);

        public static Sprite RingTicks => Get("RingTicks", 256, 256, (x, y) => Annulus(Distance(x, y, 128f), 113f, 127f) * Ticks(x, y, 128f, 20, 3f), Vector4.zero);

        public static Sprite Dot => Get("Dot", 32, 32, (x, y) => Annulus(Distance(x, y, 16f), -1f, 14f), Vector4.zero);

        public static Sprite DotOutline => Get("DotOutline", 32, 32, (x, y) => Annulus(Distance(x, y, 16f), 10f, 14f), Vector4.zero);

        public static Sprite Fret => Get("Fret", 48, 24, FretAlpha, Vector4.zero, TextureWrapMode.Repeat);

        public static Sprite Mosaic => Get("Mosaic", 16, 16, (x, y) => x >= 14f || (y >= 7f && y < 9f) ? 1f : 0f, Vector4.zero, TextureWrapMode.Repeat);

        public static Sprite Spiral => Get("Spiral", 64, 64, SpiralAlpha, Vector4.zero);

        public static Sprite SunStone => Get("SunStone", 512, 512, SunStoneAlpha, Vector4.zero);

        public static Sprite Vignette => Get("Vignette", 128, 128, VignetteAlpha, Vector4.zero);

        public static Sprite Ouroboros => Get("Ouroboros", 128, 128, OuroborosAlpha, Vector4.zero);

        public static Sprite Pill => Get("Pill", 64, 32, PillAlpha, new Vector4(16f, 0f, 16f, 0f));

        public static Sprite ThinRing => Get("ThinRing", 128, 128, (x, y) => Annulus(Distance(x, y, 64f), 58.5f, 63f), Vector4.zero);

        public static Sprite SunRays => Get("SunRays", 256, 256, (x, y) => Annulus(Distance(x, y, 128f), 104f, 127f) * Ticks(x, y, 128f, 12, 9f), Vector4.zero);

        public static Sprite Triangle => Get("Triangle", 64, 64, TriangleAlpha, Vector4.zero);

        public static Sprite Chevron => Get("Chevron", 48, 48, (x, y) => Icon(x, y, 48f, 2.6f, new[] { 6f, 9f, 12f, 15f, 18f, 9f }), Vector4.zero);

        public static Sprite IconShot => Get("IconShot", 64, 64, IconShotAlpha, Vector4.zero);

        public static Sprite IconSword => Get("IconSword", 64, 64, IconSwordAlpha, Vector4.zero);

        public static Sprite IconDodge => Get("IconDodge", 64, 64, IconDodgeAlpha, Vector4.zero);

        private static Sprite Get(string spriteName, int width, int height, Func<float, float, float> alpha, Vector4 border, TextureWrapMode wrap = TextureWrapMode.Clamp)
        {
            var path = $"{Folder}/{spriteName}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing)
                return existing;

            EnsureFolder();
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float value = Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(value * 255f));
                }
            }

            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder))
                return;

            if (AssetDatabase.IsValidFolder("Assets/_Project/Ui") == false)
                AssetDatabase.CreateFolder("Assets/_Project", "Ui");

            AssetDatabase.CreateFolder("Assets/_Project/Ui", "Sprites");
        }

        // A rectangle whose corners are cut in two steps, like the corner of a stepped pyramid.
        private static bool IsInsideStepped(float x, float y, float width, float height, float inset)
        {
            float dx = Mathf.Min(x, width - x) - inset;
            float dy = Mathf.Min(y, height - y) - inset;
            if (dx < 0f || dy < 0f)
                return false;

            bool isInNotch = (dx < Step && dy < Notch) || (dx < Notch && dy < Step);
            return isInNotch == false;
        }

        private static float PanelFrameAlpha(float x, float y)
        {
            bool isOuter = IsInsideStepped(x, y, 64f, 64f, 0f);
            bool isInner = IsInsideStepped(x, y, 64f, 64f, 2f);
            return isOuter && isInner == false ? 1f : 0f;
        }

        private static float Distance(float x, float y, float center)
        {
            return new Vector2(x - center, y - center).magnitude;
        }

        // 1 between the radii, with a one pixel soft edge.
        private static float Annulus(float distance, float innerRadius, float outerRadius)
        {
            return Mathf.Clamp01(distance - innerRadius + 0.5f) * Mathf.Clamp01(outerRadius - distance + 0.5f);
        }

        // 1 on 'count' short marks around the centre, each 'halfWidth' pixels wide on both sides.
        private static float Ticks(float x, float y, float center, int count, float halfWidth)
        {
            float distance = Distance(x, y, center);
            float angle = Mathf.Atan2(y - center, x - center) * Mathf.Rad2Deg + 90f;
            float cell = 360f / count;
            float offset = Mathf.Repeat(angle + cell * 0.5f, cell) - cell * 0.5f;
            float pixels = Mathf.Abs(offset) * Mathf.Deg2Rad * distance;
            return Mathf.Clamp01(halfWidth - pixels + 0.5f);
        }

        private static float Polyline(float x, float y, float[] points, float halfWidth)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 3 < points.Length; i += 2)
            {
                var start = new Vector2(points[i], points[i + 1]);
                var end = new Vector2(points[i + 2], points[i + 3]);
                var segment = end - start;
                float along = Mathf.Clamp01(Vector2.Dot(new Vector2(x, y) - start, segment) / Mathf.Max(0.0001f, segment.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(new Vector2(x, y), start + segment * along));
            }

            return Mathf.Clamp01(halfWidth - best + 0.5f);
        }

        // A stepped pyramid line; the tiles join into a band.
        private static float FretAlpha(float x, float y)
        {
            float[] points = { 0f, 2f, 6f, 2f, 6f, 8f, 12f, 8f, 12f, 14f, 18f, 14f, 18f, 20f, 30f, 20f, 30f, 14f, 36f, 14f, 36f, 8f, 42f, 8f, 42f, 2f, 48f, 2f };
            return Polyline(x, y, points, 1.2f);
        }

        // The stepped spiral (xicalcoliuhqui), drawn on a 44 x 44 grid with y going up.
        private static float SpiralAlpha(float x, float y)
        {
            const float Scale = 64f / 44f;
            float[] grid = { 2f, 2f, 8f, 2f, 8f, 8f, 42f, 8f, 42f, 42f, 8f, 42f, 8f, 14f, 34f, 14f, 34f, 34f, 16f, 34f, 16f, 22f, 26f, 22f };
            var points = new float[grid.Length];
            for (int i = 0; i < grid.Length; i++)
                points[i] = grid[i] * Scale;

            return Polyline(x, y, points, 2.2f);
        }

        // Rings, marks and blocks loosely inspired by the Aztec sun stone.
        private static float SunStoneAlpha(float x, float y)
        {
            const float Center = 256f;
            float distance = Distance(x, y, Center);
            float alpha = Annulus(distance, 238f, 241f);
            alpha = Mathf.Max(alpha, Annulus(distance, 220f, 234f) * Ticks(x, y, Center, 20, 4f));
            alpha = Mathf.Max(alpha, Annulus(distance, 212f, 215f));
            alpha = Mathf.Max(alpha, Annulus(distance, 190f, 202f) * Ticks(x, y, Center, 40, 2f));
            alpha = Mathf.Max(alpha, Annulus(distance, 178f, 181f));
            alpha = Mathf.Max(alpha, Annulus(distance, 112f, 168f) * Ticks(x, y, Center, 8, 18f));
            alpha = Mathf.Max(alpha, Annulus(distance, 92f, 95f));
            alpha = Mathf.Max(alpha, Annulus(distance, 52f, 55f));
            return alpha;
        }

        // A snake in a ring biting its tail: a scaled body that thins into the tail, and a head with an open jaw
        // and an eye at the top. The jaw faces counter-clockwise, so the sprite turned that way chases its tail.
        private static float OuroborosAlpha(float x, float y)
        {
            const float Center = 64f;
            const float Radius = 44f;
            const float HeadDegrees = 22f;
            const float HeadRadius = 13f;
            const float BodyStartDegrees = 36f;
            const float TailEndDegrees = 367f;

            var point = new Vector2(x - Center, y - Center);
            float distance = point.magnitude;

            // Angle clockwise from the top; the part right of the head is counted past 360, where the tail ends.
            float angle = Mathf.Repeat(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg, 360f);
            if (angle < BodyStartDegrees)
                angle += 360f;

            float body = 0f;
            if (angle <= TailEndDegrees)
            {
                float halfWidth = Mathf.Lerp(2f, 9f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((TailEndDegrees - angle) / 150f)));
                body = Mathf.Clamp01(halfWidth - Mathf.Abs(distance - Radius) + 0.5f);

                float scaleOffset = Mathf.Abs(Mathf.Repeat(angle + 9f, 18f) - 9f) * Mathf.Deg2Rad * distance;
                if (scaleOffset < 1f)
                    body *= 0.35f;
            }

            float headRadians = HeadDegrees * Mathf.Deg2Rad;
            var outward = new Vector2(Mathf.Sin(headRadians), Mathf.Cos(headRadians));
            var towardTail = new Vector2(-Mathf.Cos(headRadians), Mathf.Sin(headRadians));
            var headCenter = outward * Radius;
            var fromHead = point - headCenter;
            float head = Mathf.Clamp01(HeadRadius - fromHead.magnitude + 0.5f);

            float along = Vector2.Dot(fromHead, towardTail);
            float across = Mathf.Abs(Vector2.Dot(fromHead, outward));
            if (along > 2f && across < (along - 2f) * 0.6f)
                head = 0f;

            var eye = headCenter + outward * 5f - towardTail * 2f;
            if (Vector2.Distance(point, eye) < 2.2f)
                head = 0f;

            return Mathf.Max(body, head);
        }

        // A capsule with fully rounded ends, for badges and the warning banner.
        private static float PillAlpha(float x, float y)
        {
            const float Radius = 16f;
            float clampedX = Mathf.Clamp(x, Radius, 64f - Radius);
            return Mathf.Clamp01(Radius - Vector2.Distance(new Vector2(x, y), new Vector2(clampedX, 16f)) + 0.5f);
        }

        private static float TriangleAlpha(float x, float y)
        {
            var a = new Vector2(6f, 10f);
            var b = new Vector2(58f, 22f);
            var c = new Vector2(30f, 58f);
            var p = new Vector2(x, y);
            float d1 = Cross(p, a, b);
            float d2 = Cross(p, b, c);
            float d3 = Cross(p, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return hasNegative && hasPositive ? 0f : 1f;
        }

        private static float Cross(Vector2 p, Vector2 a, Vector2 b)
        {
            return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        }

        // A stroke icon drawn on a 24 x 24 grid with y going down (like an SVG icon), scaled to the sprite.
        private static float Icon(float x, float y, float size, float strokeWidth, float[] svgPoints)
        {
            float scale = size / 24f;
            var points = new float[svgPoints.Length];
            for (int i = 0; i < svgPoints.Length; i += 2)
            {
                points[i] = svgPoints[i] * scale;
                points[i + 1] = (24f - svgPoints[i + 1]) * scale;
            }

            return Polyline(x, y, points, strokeWidth * scale * 0.5f);
        }

        private static float IconShotAlpha(float x, float y)
        {
            const float Scale = 64f / 24f;
            float ring = Annulus(Distance(x, y, 32f), (4f - 0.9f) * Scale, (4f + 0.9f) * Scale);
            float alpha = Mathf.Max(ring, Icon(x, y, 64f, 1.8f, new[] { 12f, 2f, 12f, 6f }));
            alpha = Mathf.Max(alpha, Icon(x, y, 64f, 1.8f, new[] { 12f, 18f, 12f, 22f }));
            alpha = Mathf.Max(alpha, Icon(x, y, 64f, 1.8f, new[] { 2f, 12f, 6f, 12f }));
            return Mathf.Max(alpha, Icon(x, y, 64f, 1.8f, new[] { 18f, 12f, 22f, 12f }));
        }

        private static float IconSwordAlpha(float x, float y)
        {
            float alpha = Icon(x, y, 64f, 1.8f, new[] { 8f, 16f, 20f, 4f, 20f, 8f, 8f, 20f });
            alpha = Mathf.Max(alpha, Icon(x, y, 64f, 1.8f, new[] { 5f, 13f, 11f, 19f }));
            return Mathf.Max(alpha, Icon(x, y, 64f, 1.8f, new[] { 3f, 21f, 6.5f, 17.5f }));
        }

        // "M4 17c4 0 6-3 8-6s4-6 8-6" and the arrow head "M16 5h4v4", the curve sampled into short lines.
        private static float IconDodgeAlpha(float x, float y)
        {
            const int Samples = 12;
            var curve = new float[(Samples * 2 + 1) * 2];
            int index = 0;
            AddBezier(curve, ref index, new Vector2(4f, 17f), new Vector2(8f, 17f), new Vector2(10f, 14f), new Vector2(12f, 11f), Samples, true);
            AddBezier(curve, ref index, new Vector2(12f, 11f), new Vector2(14f, 8f), new Vector2(16f, 5f), new Vector2(20f, 5f), Samples, false);
            float alpha = Icon(x, y, 64f, 1.8f, curve);
            return Mathf.Max(alpha, Icon(x, y, 64f, 1.8f, new[] { 16f, 5f, 20f, 5f, 20f, 9f }));
        }

        private static void AddBezier(float[] points, ref int index, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int samples, bool includeStart)
        {
            for (int i = includeStart ? 0 : 1; i <= samples; i++)
            {
                float t = (float)i / samples;
                float u = 1f - t;
                var point = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                points[index++] = point.x;
                points[index++] = point.y;
            }
        }

        // Transparent in the middle, opaque at the edges.
        private static float VignetteAlpha(float x, float y)
        {
            var offset = new Vector2(x / 128f - 0.5f, y / 128f - 0.5f) * 2f;
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, offset.magnitude));
        }
    }
}
