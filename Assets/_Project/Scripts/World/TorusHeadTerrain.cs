using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Terrain shaped by the snake's head. The head sits on the torus and the body grows out of its back,
    /// so near the head the terrain is blended into the outline of the head's neck.
    /// Height is full "neck" height (<see cref="GetNeckHeight"/>) on the head and fades to the normal terrain
    /// over <see cref="BlendLength"/> behind the neck. Ahead of the head the tail being eaten thins out over <see cref="TailLength"/>.
    /// All angles are in radians. The data is applied both to the heights used by gameplay
    /// (<see cref="TorusSurface.GetHeight(float, float)"/>) and to the segment meshes (<see cref="TorusHeadMeshBlender"/>).
    /// </summary>
    public class TorusHeadTerrain
    {
        private const float TwoPi = TorusGeometry.TwoPi;

        private float[] neckHeights;
        private AnimationCurve tailCurve;

        /// <summary>Changes every time anything affecting the terrain changes. Used to skip needless mesh updates.</summary>
        public int Version { get; private set; }

        public bool IsActive => neckHeights != null;

        /// <summary>Theta of the head's pivot, in radians.</summary>
        public float Theta { get; private set; }

        /// <summary>How far behind the pivot (in radians of theta) the rear end of the head is.</summary>
        public float NeckOffset { get; private set; }

        /// <summary>Length of the head along the body, in radians of theta.</summary>
        public float HeadLength { get; private set; }

        /// <summary>Length of the transition from the head outline to the normal terrain behind the neck, in radians of theta.</summary>
        public float BlendLength { get; private set; }

        /// <summary>Length of the zone ahead of the head where the tail thins out, in radians of theta.</summary>
        public float TailLength { get; private set; }

        /// <summary>Theta of the rear end of the head, where the body grows out of it.</summary>
        public float NeckTheta => Theta - NeckOffset;

        /// <summary>Sets the neck outline. Heights are above the base tube, evenly spaced around phi starting from phi = 0.</summary>
        public void Configure(float neckOffset, float headLength, float blendLength, float[] heights)
        {
            NeckOffset = neckOffset;
            HeadLength = Mathf.Max(0f, headLength);
            BlendLength = Mathf.Max(0f, blendLength);
            neckHeights = heights != null && heights.Length > 0 ? (float[])heights.Clone() : null;
            Version++;
        }

        public void Deactivate()
        {
            if (neckHeights == null)
                return;

            neckHeights = null;
            Version++;
        }

        public void SetTheta(float theta)
        {
            theta = Mathf.Repeat(theta, TwoPi);
            if (Mathf.Approximately(theta, Theta))
                return;

            Theta = theta;
            Version++;
        }

        /// <summary>Height of the neck outline above the base tube for phi (radians). Interpolated, wraps around.</summary>
        public float GetNeckHeight(float phi)
        {
            if (neckHeights == null)
                return 0f;

            int count = neckHeights.Length;
            float position = Mathf.Repeat(phi, TwoPi) / TwoPi * count;
            int first = Mathf.FloorToInt(position) % count;
            int second = (first + 1) % count;
            return Mathf.Lerp(neckHeights[first], neckHeights[second], position - Mathf.Floor(position));
        }

        /// <summary>
        /// Weight of the head outline (0-1) at the given theta: 1 on the head, fading out behind the neck.
        /// Ahead of the head the terrain (the tail being eaten) is left alone, so the weight is 0 there.
        /// </summary>
        public float GetWeight(float theta)
        {
            if (IsActive == false)
                return 0f;

            // Positive behind the neck, negative on and ahead of it.
            float behindNeck = Mathf.Repeat(NeckTheta - theta + Mathf.PI, TwoPi) - Mathf.PI;
            if (behindNeck <= 0f)
                return -behindNeck <= HeadLength ? 1f : 0f;

            if (BlendLength <= 0f)
                return 0f;

            return Mathf.SmoothStep(1f, 0f, behindNeck / BlendLength);
        }

        /// <summary>
        /// Sets how the tail ahead of the head thins out just before it is eaten. The curve is evaluated from 0 (the front of the
        /// head) to 1 (start of the zone, away from the head) and gives the factor of the tube radius: 0 is the centre, 1 the original radius.
        /// </summary>
        public void ConfigureTail(float tailLength, AnimationCurve radiusCurve)
        {
            TailLength = Mathf.Max(0f, tailLength);
            tailCurve = radiusCurve == null ? null : new AnimationCurve(radiusCurve.keys);
            Version++;
        }

        /// <summary>Factor (0-1) of the tube radius at the given theta. 1 outside the tail zone.</summary>
        public float GetTailRadiusScale(float theta)
        {
            if (IsActive == false || tailCurve == null || TailLength <= 0f)
                return 1f;

            float behindNeck = Mathf.Repeat(NeckTheta - theta + Mathf.PI, TwoPi) - Mathf.PI;
            float aheadOfHead = -behindNeck - HeadLength;
            if (aheadOfHead < 0f || aheadOfHead >= TailLength)
                return 1f;

            return Mathf.Max(0f, tailCurve.Evaluate(aheadOfHead / TailLength));
        }

        /// <summary>Terrain height after the head shaped it: the neck outline on and behind the head, a thinner tube on the tail ahead of it.</summary>
        public float Apply(float terrainHeight, float theta, float phi, float minorRadius)
        {
            float weight = GetWeight(theta);
            if (weight > 0f)
                return Mathf.Lerp(terrainHeight, GetNeckHeight(phi), weight);

            float scale = GetTailRadiusScale(theta);
            return scale >= 1f ? terrainHeight : (minorRadius + terrainHeight) * scale - minorRadius;
        }

        /// <summary>Range of theta (start and length, radians) where the terrain is changed.</summary>
        public void GetAffectedRange(out float start, out float length)
        {
            start = NeckTheta - BlendLength;
            length = BlendLength + HeadLength + TailLength;
        }
    }
}
