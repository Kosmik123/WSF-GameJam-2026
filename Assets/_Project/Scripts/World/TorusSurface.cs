using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Math of the torus world. The surface is addressed by two angles:
    /// theta - around the big circle (the direction of running), phi - around the tube cross-section.
    /// Terrain height comes from segment profiles, so it can be queried for gameplay without touching meshes.
    /// </summary>
    public class TorusSurface
    {
        private const float TwoPi = Mathf.PI * 2f;
        private const float DerivativeStep = 0.0005f;

        private readonly TorusSegmentProfile[] profiles;

        public float MajorRadius { get; }
        public float MinorRadius { get; }
        public int SegmentCount { get; }
        public float BlendWidth { get; }
        public float SegmentAngle => TwoPi / SegmentCount;

        public TorusSurface(float majorRadius, float minorRadius, int segmentCount, IReadOnlyList<TorusSegmentProfile> segmentProfiles, float blendWidth)
        {
            MajorRadius = majorRadius;
            MinorRadius = minorRadius;
            SegmentCount = Mathf.Max(1, segmentCount);
            BlendWidth = Mathf.Clamp(blendWidth, 0f, 0.5f);

            profiles = new TorusSegmentProfile[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
                profiles[i] = GetAssignedProfile(segmentProfiles, i);
        }

        /// <summary>Profile of a segment. The index wraps around, so -1 is the last segment. Never null.</summary>
        public TorusSegmentProfile GetProfile(int segmentIndex)
        {
            return profiles[(segmentIndex % SegmentCount + SegmentCount) % SegmentCount];
        }

        public int GetSegmentIndex(float theta)
        {
            float wrapped = Mathf.Repeat(theta, TwoPi);
            return Mathf.Min(Mathf.FloorToInt(wrapped / SegmentAngle), SegmentCount - 1);
        }

        /// <summary>Point of the base tube, without terrain height.</summary>
        public Vector3 GetBasePoint(float theta, float phi)
        {
            float ring = MajorRadius + MinorRadius * Mathf.Cos(phi);
            return new Vector3(ring * Mathf.Cos(theta), MinorRadius * Mathf.Sin(phi), ring * Mathf.Sin(theta));
        }

        /// <summary>Outward normal of the base tube, without terrain height.</summary>
        public Vector3 GetBaseNormal(float theta, float phi)
        {
            return new Vector3(Mathf.Cos(phi) * Mathf.Cos(theta), Mathf.Sin(phi), Mathf.Cos(phi) * Mathf.Sin(theta));
        }

        /// <summary>Surface point including terrain height.</summary>
        public Vector3 GetPoint(float theta, float phi)
        {
            return GetBasePoint(theta, phi) + GetBaseNormal(theta, phi) * GetHeight(theta, phi);
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

            return TorusSegmentBlend.GetHeight(
                GetProfile(index),
                GetProfile(index - 1),
                GetProfile(index + 1),
                scaled - index,
                Mathf.Repeat(phi, TwoPi) / TwoPi,
                BlendWidth,
                GetBasePoint(wrapped, phi));
        }

        private TorusSegmentProfile GetAssignedProfile(IReadOnlyList<TorusSegmentProfile> segmentProfiles, int segmentIndex)
        {
            if (segmentProfiles == null || segmentProfiles.Count == 0)
                return FlatTorusSegmentProfile.Instance;

            TorusSegmentProfile profile = segmentProfiles[segmentIndex % segmentProfiles.Count];
            return profile ? profile : FlatTorusSegmentProfile.Instance;
        }
    }
}
