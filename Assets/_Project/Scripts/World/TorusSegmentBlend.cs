using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Cross-fades neighbouring segment profiles near segment borders, so terrain height and color are continuous.
    /// The border line is jagged by noise (the same on both sides, so the surface stays continuous).
    /// Profiles that only cover their own segment (<see cref="TorusSegmentProfile.ClampsAlong"/>) are sampled mirrored:
    /// a point at distance d from the border on one side blends with the neighbour's terrain at distance d on its side,
    /// so the biomes really overlap instead of fading into the neighbour's edge row.
    /// </summary>
    public static class TorusSegmentBlend
    {
        /// <summary>How far (in blend widths) the noise moves the border line back and forth.</summary>
        private const float BorderJaggedness = 0.45f;
        private const float NoiseFrequency = 7f;

        /// <param name="along">Position inside the segment: 0 at its start, 1 at its end.</param>
        /// <param name="around">Position around the tube: 0-1.</param>
        /// <param name="blendWidth">Part of the segment (from each end) where the profiles are cross-faded.</param>
        public static float GetHeight(TorusSegmentProfile own, TorusSegmentProfile previous, TorusSegmentProfile next, float along, float around, float blendWidth, Vector3 basePoint)
        {
            float ownHeight = SampleHeight(own, along, around, basePoint);
            if (TryGetBlend(own, previous, next, along, around, blendWidth, out var neighbour, out float neighbourAlong, out float ownWeight) == false)
                return ownHeight;

            return Mathf.Lerp(SampleHeight(neighbour, neighbourAlong, around, basePoint), ownHeight, ownWeight);
        }

        /// <summary>Cross-fades vertex colors the same way as <see cref="GetHeight"/> does heights.</summary>
        public static Color GetColor(TorusSegmentProfile own, TorusSegmentProfile previous, TorusSegmentProfile next, float along, float around, float blendWidth, Vector3 basePoint)
        {
            var ownColor = SampleColor(own, along, around, basePoint);
            if (TryGetBlend(own, previous, next, along, around, blendWidth, out var neighbour, out float neighbourAlong, out float ownWeight) == false)
                return ownColor;

            return Color.Lerp(SampleColor(neighbour, neighbourAlong, around, basePoint), ownColor, ownWeight);
        }

        /// <summary>
        /// Weight (0-1) of a segment's own profile at a point near one of its borders.
        /// 1 deep inside, 0.5 on the (jagged) border line, and the two neighbours always sum to 1.
        /// </summary>
        /// <param name="distanceInside">Distance from the border into this segment, in blend widths (negative = inside the neighbour).</param>
        /// <param name="isEndBorder">True for the border with the next segment, false for the one with the previous.</param>
        public static float GetOwnWeight(TorusSegmentProfile own, TorusSegmentProfile neighbour, float distanceInside, float around, bool isEndBorder)
        {
            float noise = GetBorderNoise(isEndBorder ? own : neighbour, isEndBorder ? neighbour : own, around);
            float signedNoise = isEndBorder ? noise : -noise;
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(0.5f + 0.5f * (distanceInside + signedNoise)));
        }

        private static bool TryGetBlend(TorusSegmentProfile own, TorusSegmentProfile previous, TorusSegmentProfile next, float along, float around, float blendWidth, out TorusSegmentProfile neighbour, out float neighbourAlong, out float ownWeight)
        {
            float reach = blendWidth * (1f + BorderJaggedness);
            if (blendWidth <= 0f || (along >= reach && along <= 1f - reach))
            {
                neighbour = null;
                neighbourAlong = 0f;
                ownWeight = 1f;
                return false;
            }

            bool isEndBorder = along > 0.5f;
            float distance = isEndBorder ? 1f - along : along;
            neighbour = isEndBorder ? next : previous;
            ownWeight = GetOwnWeight(own, neighbour, distance / blendWidth, around, isEndBorder);

            // The same place seen from the neighbouring segment lies one segment further (continuous profiles),
            // or, for profiles that cover only their own segment, at the same distance inside the neighbour (mirrored).
            bool isMirrored = neighbour && neighbour.ClampsAlong;
            if (isEndBorder)
                neighbourAlong = isMirrored ? distance : along - 1f;
            else
                neighbourAlong = isMirrored ? 1f - distance : along + 1f;

            return true;
        }

        /// <summary>Noise of the border line between two segments (-jaggedness..jaggedness), stable for the pair.</summary>
        private static float GetBorderNoise(TorusSegmentProfile first, TorusSegmentProfile second, float around)
        {
            float seed = (GetStableHash(first) * 31 + GetStableHash(second)) % 1000 * 0.137f;
            float noise = Mathf.PerlinNoise(around * NoiseFrequency, seed) - 0.5f;
            noise += (Mathf.PerlinNoise(around * NoiseFrequency * 3.1f, seed + 17.3f) - 0.5f) * 0.4f;
            // Perlin noise is not periodic, so it fades out towards the seam around the tube.
            float wrapFade = Mathf.SmoothStep(0f, 1f, Mathf.Min(around, 1f - around) * 10f);
            return noise * 2f * BorderJaggedness * wrapFade;
        }

        private static int GetStableHash(TorusSegmentProfile profile)
        {
            if (profile == null)
                return 0;

            int hash = 17;
            foreach (char character in profile.name)
                hash = (hash * 31 + character) & 0x7FFFFFFF;

            return hash;
        }

        private static Color SampleColor(TorusSegmentProfile profile, float along, float around, Vector3 basePoint)
        {
            return profile ? profile.SampleColor(along, around, basePoint) : Color.white;
        }

        private static float SampleHeight(TorusSegmentProfile profile, float along, float around, Vector3 basePoint)
        {
            return profile ? profile.SampleHeight(along, around, basePoint) : 0f;
        }
    }
}
