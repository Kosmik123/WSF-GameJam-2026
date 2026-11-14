using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>Cross-fades heights of neighbouring segment profiles so the terrain is continuous on segment borders.</summary>
    public static class TorusSegmentBlend
    {
        /// <param name="along">Position inside the segment: 0 at its start, 1 at its end.</param>
        /// <param name="around">Position around the tube: 0-1.</param>
        /// <param name="blendWidth">Part of the segment (from each end) where the profiles are cross-faded.</param>
        public static float GetHeight(TorusSegmentProfile own, TorusSegmentProfile previous, TorusSegmentProfile next, float along, float around, float blendWidth, Vector3 basePoint)
        {
            float ownHeight = SampleHeight(own, along, around, basePoint);

            // The same place seen from the neighbouring segment lies one segment further.
            if (along < blendWidth)
                return BlendWithNeighbour(ownHeight, previous, along + 1f, around, basePoint, along / blendWidth);

            if (along > 1f - blendWidth)
                return BlendWithNeighbour(ownHeight, next, along - 1f, around, basePoint, (1f - along) / blendWidth);

            return ownHeight;
        }

        private static float BlendWithNeighbour(float ownHeight, TorusSegmentProfile neighbour, float neighbourAlong, float around, Vector3 basePoint, float distanceFromBorder)
        {
            float neighbourHeight = SampleHeight(neighbour, neighbourAlong, around, basePoint);

            // At the border both profiles weigh 0.5, so the surface is continuous.
            float ownWeight = Mathf.SmoothStep(0f, 1f, 0.5f + 0.5f * distanceFromBorder);
            return Mathf.Lerp(neighbourHeight, ownHeight, ownWeight);
        }

        private static float SampleHeight(TorusSegmentProfile profile, float along, float around, Vector3 basePoint)
        {
            return profile ? profile.SampleHeight(along, around, basePoint) : 0f;
        }
    }
}
