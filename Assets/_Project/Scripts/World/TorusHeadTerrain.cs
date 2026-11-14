using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Terrain shaped by the snake's head. The head sits on the torus and the body grows out of its back,
    /// so near the head the terrain is blended into the outline of the head's neck.
    /// Height is full "neck" height (<see cref="GetNeckHeight"/>) on the head and fades to the normal terrain
    /// over <see cref="BlendLength"/> behind the neck (and ahead of the head).
    /// All angles are in radians. The data is applied both to the heights used by gameplay
    /// (<see cref="TorusSurface.GetHeight(float, float)"/>) and to the segment meshes (<see cref="TorusHeadMeshBlender"/>).
    /// </summary>
    public class TorusHeadTerrain
    {
        private const float TwoPi = TorusGeometry.TwoPi;

        private float[] neckHeights;

        /// <summary>Changes every time anything affecting the terrain changes. Used to skip needless mesh updates.</summary>
        public int Version { get; private set; }

        public bool IsActive => neckHeights != null;

        /// <summary>Theta of the head's pivot, in radians.</summary>
        public float Theta { get; private set; }

        /// <summary>How far behind the pivot (in radians of theta) the rear end of the head is.</summary>
        public float NeckOffset { get; private set; }

        /// <summary>Length of the head along the body, in radians of theta.</summary>
        public float HeadLength { get; private set; }

        /// <summary>Length of the transition from the head outline to the normal terrain, in radians of theta.</summary>
        public float BlendLength { get; private set; }

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

        /// <summary>Weight of the head outline (0-1) at the given theta: 1 on the head, fading out around it.</summary>
        public float GetWeight(float theta)
        {
            if (IsActive == false)
                return 0f;

            // Distance from the head (the plateau between the neck and the front of the head).
            float behindNeck = Mathf.Repeat(NeckTheta - theta + Mathf.PI, TwoPi) - Mathf.PI;
            float outside = behindNeck > 0f ? behindNeck : Mathf.Max(0f, -behindNeck - HeadLength);

            if (BlendLength <= 0f)
                return outside <= 0f ? 1f : 0f;

            return Mathf.SmoothStep(1f, 0f, outside / BlendLength);
        }

        public float Apply(float terrainHeight, float theta, float phi)
        {
            float weight = GetWeight(theta);
            return weight <= 0f ? terrainHeight : Mathf.Lerp(terrainHeight, GetNeckHeight(phi), weight);
        }

        /// <summary>Range of theta (start and length, radians) where the weight is above zero.</summary>
        public void GetAffectedRange(out float start, out float length)
        {
            start = NeckTheta - BlendLength;
            length = HeadLength + BlendLength * 2f;
        }
    }
}
