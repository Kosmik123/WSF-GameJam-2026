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
        private float[,] headMap;
        private float headMapLength;
        private float headMapRampLength;
        private float roll;

        /// <summary>Changes every time anything affecting the terrain changes. Used to skip needless mesh updates.</summary>
        public int Version { get; private set; }

        public bool IsActive => neckHeights != null;

        /// <summary>Rotation of the head around its own forward axis since it was measured, in radians of phi. The neck outline and the height map turn with it.</summary>
        public float Roll => roll;

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

        /// <summary>
        /// Sets the height map of the top of the head. Rows go along the body from the rear end of the head, columns around phi
        /// (evenly spaced from phi = 0). Heights are above the base tube. Without it the head is the neck outline extruded along theta.
        /// </summary>
        /// <param name="mapLength">Length covered by the rows, in radians of theta (the measured length of the head).</param>
        /// <param name="rampLength">Distance from the rear end over which the neck outline changes into the map, in radians of theta.</param>
        public void ConfigureHeadMap(float[,] heights, float mapLength, float rampLength)
        {
            headMap = heights != null && heights.GetLength(0) > 1 && heights.GetLength(1) > 0 ? (float[,])heights.Clone() : null;
            headMapLength = Mathf.Max(0.0001f, mapLength);
            headMapRampLength = Mathf.Max(0f, rampLength);
            Version++;
        }

        public void Deactivate()
        {
            if (neckHeights == null)
                return;

            neckHeights = null;
            headMap = null;
            roll = 0f;
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

        public void SetRoll(float newRoll)
        {
            newRoll = Mathf.DeltaAngle(0f, newRoll * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            if (Mathf.Approximately(newRoll, roll))
                return;

            roll = newRoll;
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
        /// Height of the head's own surface at theta and phi (radians), above the base tube. Only meaningful on the head
        /// (<see cref="GetWeight"/> = 1): the neck outline at the rear end, changing into the height map over the ramp.
        /// </summary>
        public float GetHeadHeight(float theta, float phi)
        {
            phi -= roll;
            float neckHeight = GetNeckHeight(phi);
            if (headMap == null)
                return neckHeight;

            float distanceFromNeck = Mathf.Repeat(theta - NeckTheta, TwoPi);
            float mapHeight = SampleHeadMap(Mathf.Min(distanceFromNeck, headMapLength), phi);
            float ramp = headMapRampLength <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, distanceFromNeck / headMapRampLength);
            return Mathf.Lerp(neckHeight, mapHeight, ramp);
        }

        private float SampleHeadMap(float distanceFromNeck, float phi)
        {
            int rows = headMap.GetLength(0);
            int columns = headMap.GetLength(1);

            float rowPosition = distanceFromNeck / headMapLength * (rows - 1);
            int firstRow = Mathf.Clamp(Mathf.FloorToInt(rowPosition), 0, rows - 2);
            float rowT = Mathf.Clamp01(rowPosition - firstRow);

            float columnPosition = Mathf.Repeat(phi, TwoPi) / TwoPi * columns;
            int firstColumn = Mathf.FloorToInt(columnPosition) % columns;
            int secondColumn = (firstColumn + 1) % columns;
            float columnT = columnPosition - Mathf.Floor(columnPosition);

            float near = Mathf.Lerp(headMap[firstRow, firstColumn], headMap[firstRow, secondColumn], columnT);
            float far = Mathf.Lerp(headMap[firstRow + 1, firstColumn], headMap[firstRow + 1, secondColumn], columnT);
            return Mathf.Lerp(near, far, rowT);
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
                return Mathf.Lerp(terrainHeight, GetHeadHeight(theta, phi), weight);

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
