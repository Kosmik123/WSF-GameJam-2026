using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>Cross-fades heights of neighbouring segment profiles so the terrain is continuous on segment borders.</summary>
    public static class TorusSegmentBlend
    {
        /// <param name="local">Position inside the segment: 0 at its start, 1 at its end.</param>
        /// <param name="blendWidth">Part of the segment (from each end) where the profiles are cross-faded.</param>
        public static float GetHeight(TorusSegmentProfile own, TorusSegmentProfile previous, TorusSegmentProfile next, float local, float blendWidth, Vector3 basePoint)
        {
            float ownHeight = SampleHeight(own, basePoint);

            if (local < blendWidth)
                return BlendWithNeighbour(ownHeight, previous, basePoint, local / blendWidth);

            if (local > 1f - blendWidth)
                return BlendWithNeighbour(ownHeight, next, basePoint, (1f - local) / blendWidth);

            return ownHeight;
        }

        private static float BlendWithNeighbour(float ownHeight, TorusSegmentProfile neighbour, Vector3 basePoint, float distanceFromBorder)
        {
            float neighbourHeight = SampleHeight(neighbour, basePoint);

            // At the border both profiles weigh 0.5, so the surface is continuous.
            float ownWeight = Mathf.SmoothStep(0f, 1f, 0.5f + 0.5f * distanceFromBorder);
            return Mathf.Lerp(neighbourHeight, ownHeight, ownWeight);
        }

        private static float SampleHeight(TorusSegmentProfile profile, Vector3 basePoint)
        {
            return profile ? profile.SampleHeight(basePoint) : 0f;
        }
    }
}
