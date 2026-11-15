using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// 3D value noise sampled at points of the base torus, so it has no seams around the tube nor between segments.
    /// </summary>
    public static class TorusNoise
    {
        /// <summary>Fractal noise, roughly 0-1 (most values near 0.5).</summary>
        /// <param name="point">Point in noise space (world units divided by the feature size).</param>
        public static float Fractal(Vector3 point, int octaves)
        {
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Value(point) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                point = point * 2.03f + new Vector3(17.1f, -9.7f, 5.3f);
            }

            return sum / total;
        }

        /// <summary>Fractal noise stretched so its values spread over the whole 0-1 range.</summary>
        public static float Spread(Vector3 point, int octaves)
        {
            return Mathf.Clamp01((Fractal(point, octaves) - 0.5f) * 2.6f + 0.5f);
        }

        private static float Value(Vector3 point)
        {
            int x = Mathf.FloorToInt(point.x);
            int y = Mathf.FloorToInt(point.y);
            int z = Mathf.FloorToInt(point.z);
            float tx = Smooth(point.x - x);
            float ty = Smooth(point.y - y);
            float tz = Smooth(point.z - z);

            float x00 = Mathf.Lerp(Hash(x, y, z), Hash(x + 1, y, z), tx);
            float x10 = Mathf.Lerp(Hash(x, y + 1, z), Hash(x + 1, y + 1, z), tx);
            float x01 = Mathf.Lerp(Hash(x, y, z + 1), Hash(x + 1, y, z + 1), tx);
            float x11 = Mathf.Lerp(Hash(x, y + 1, z + 1), Hash(x + 1, y + 1, z + 1), tx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, ty), Mathf.Lerp(x01, x11, ty), tz);
        }

        private static float Smooth(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static float Hash(int x, int y, int z)
        {
            unchecked
            {
                uint hash = (uint)x * 73856093u ^ (uint)y * 19349663u ^ (uint)z * 83492791u;
                hash ^= hash >> 13;
                hash *= 0x5bd1e995u;
                hash ^= hash >> 15;
                return (hash & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
