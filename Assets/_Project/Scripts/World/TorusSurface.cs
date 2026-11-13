using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Math of the torus world. The surface is addressed by two angles:
    /// theta - around the big circle (the direction of running), phi - around the tube cross-section.
    /// Terrain height is sampled from 3D noise, so it is seamless in both directions.
    /// </summary>
    public class TorusSurface
    {
        private const float TwoPi = Mathf.PI * 2f;
        private const float DerivativeStep = 0.0005f;

        private readonly TorusSegmentProfile[] profiles;
        private readonly float blendWidth;

        public float MajorRadius { get; }
        public float MinorRadius { get; }
        public int SegmentCount { get; }
        public float SegmentAngle => TwoPi / SegmentCount;

        public TorusSurface(float majorRadius, float minorRadius, int segmentCount, IReadOnlyList<TorusSegmentProfile> segmentProfiles, float blendWidth)
        {
            MajorRadius = majorRadius;
            MinorRadius = minorRadius;
            SegmentCount = Mathf.Max(1, segmentCount);
            this.blendWidth = Mathf.Clamp(blendWidth, 0f, 0.5f);

            profiles = new TorusSegmentProfile[SegmentCount];
            if (segmentProfiles == null || segmentProfiles.Count == 0)
                return;

            for (int i = 0; i < SegmentCount; i++)
                profiles[i] = segmentProfiles[i % segmentProfiles.Count];
        }

        public TorusSegmentProfile GetProfile(int segmentIndex)
        {
            return profiles[segmentIndex];
        }

        public int GetSegmentIndex(float theta)
        {
            float wrapped = Mathf.Repeat(theta, TwoPi);
            return Mathf.Min(Mathf.FloorToInt(wrapped / SegmentAngle), SegmentCount - 1);
        }

        /// <summary>Surface point including terrain height.</summary>
        public Vector3 GetPoint(float theta, float phi)
        {
            return GetTubePoint(theta, phi, MinorRadius + GetHeight(theta, phi));
        }

        /// <summary>Outward surface normal including terrain height.</summary>
        public Vector3 GetNormal(float theta, float phi)
        {
            Vector3 alongTheta = GetPoint(theta + DerivativeStep, phi) - GetPoint(theta - DerivativeStep, phi);
            Vector3 alongPhi = GetPoint(theta, phi + DerivativeStep) - GetPoint(theta, phi - DerivativeStep);
            return Vector3.Cross(alongPhi, alongTheta).normalized;
        }

        /// <summary>Terrain height above the base tube. Neighbouring segment profiles are cross-faded near borders.</summary>
        public float GetHeight(float theta, float phi)
        {
            float wrapped = Mathf.Repeat(theta, TwoPi);
            float scaled = wrapped / SegmentAngle;
            int index = Mathf.Min(Mathf.FloorToInt(scaled), SegmentCount - 1);
            float local = scaled - index;

            Vector3 basePoint = GetTubePoint(wrapped, phi, MinorRadius);
            float ownHeight = SampleProfile(profiles[index], basePoint);

            if (local < blendWidth)
                return BlendWithNeighbour(ownHeight, index - 1, basePoint, local / blendWidth);

            if (local > 1f - blendWidth)
                return BlendWithNeighbour(ownHeight, index + 1, basePoint, (1f - local) / blendWidth);

            return ownHeight;
        }

        private float BlendWithNeighbour(float ownHeight, int neighbourIndex, Vector3 basePoint, float distanceFromBorder)
        {
            int wrappedIndex = (neighbourIndex + SegmentCount) % SegmentCount;
            float neighbourHeight = SampleProfile(profiles[wrappedIndex], basePoint);

            // At the border both profiles weigh 0.5, so the surface is continuous.
            float ownWeight = Mathf.SmoothStep(0f, 1f, 0.5f + 0.5f * distanceFromBorder);
            return Mathf.Lerp(neighbourHeight, ownHeight, ownWeight);
        }

        private Vector3 GetTubePoint(float theta, float phi, float tubeRadius)
        {
            float ring = MajorRadius + tubeRadius * Mathf.Cos(phi);
            return new Vector3(ring * Mathf.Cos(theta), tubeRadius * Mathf.Sin(phi), ring * Mathf.Sin(theta));
        }

        private float SampleProfile(TorusSegmentProfile profile, Vector3 basePoint)
        {
            if (profile == null)
                return 0f;

            Vector3 samplePoint = basePoint * profile.Frequency + profile.NoiseOffset;
            float value = Mathf.Clamp01(FractalNoise(samplePoint, profile.Octaves, profile.Persistence));
            return profile.Amplitude * (profile.HeightCurve.Evaluate(value) - 0.5f);
        }

        private float FractalNoise(Vector3 point, int octaves, float persistence)
        {
            float sum = 0f;
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;

            for (int i = 0; i < octaves; i++)
            {
                sum += Noise3D(point * frequency) * amplitude;
                total += amplitude;
                amplitude *= persistence;
                frequency *= 2f;
            }

            return sum / total;
        }

        /// <summary>True 3D simplex noise remapped to 0-1.</summary>
        private float Noise3D(Vector3 point)
        {
            return noise.snoise(new float3(point.x, point.y, point.z)) * 0.5f + 0.5f;
        }
    }
}
