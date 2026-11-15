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
        private const float TwoPi = TorusGeometry.TwoPi;
        private const float DerivativeStep = 0.0005f;

        private readonly TorusSegmentProfile[] profiles;

        public float MajorRadius { get; }
        public float MinorRadius { get; }
        public int SegmentCount { get; }
        public float BlendWidth { get; }
        public float SegmentAngle => TwoPi / SegmentCount;

        /// <summary>Terrain shaped by the snake's head. Null = no head influence.</summary>
        public TorusHeadTerrain HeadTerrain { get; }

        public TorusSurface(float majorRadius, float minorRadius, int segmentCount, IReadOnlyList<TorusSegmentProfile> segmentProfiles, float blendWidth, TorusHeadTerrain headTerrain = null)
        {
            HeadTerrain = headTerrain;
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

        public int GetSegmentIndex(TorusAngles angles)
        {
            return GetSegmentIndex(angles.Theta);
        }

        /// <summary>Segment index for theta in radians.</summary>
        public int GetSegmentIndex(float theta)
        {
            float wrapped = Mathf.Repeat(theta, TwoPi);
            return Mathf.Min(Mathf.FloorToInt(wrapped / SegmentAngle), SegmentCount - 1);
        }

        /// <summary>Point of the base tube, without terrain height.</summary>
        public Vector3 GetBasePoint(TorusAngles angles)
        {
            return GetBasePoint(angles.Theta, angles.Phi);
        }

        /// <summary>Point of the base tube for angles in radians.</summary>
        public Vector3 GetBasePoint(float theta, float phi)
        {
            return TorusGeometry.GetPoint(MajorRadius, MinorRadius, theta, phi);
        }

        /// <summary>Outward normal of the base tube, without terrain height.</summary>
        public Vector3 GetBaseNormal(TorusAngles angles)
        {
            return GetBaseNormal(angles.Theta, angles.Phi);
        }

        /// <summary>Outward normal of the base tube for angles in radians.</summary>
        public Vector3 GetBaseNormal(float theta, float phi)
        {
            return TorusGeometry.GetNormal(theta, phi);
        }

        /// <summary>Surface point including terrain height.</summary>
        public Vector3 GetPoint(TorusAngles angles)
        {
            return GetPoint(angles.Theta, angles.Phi);
        }

        /// <summary>Surface point for angles in radians.</summary>
        public Vector3 GetPoint(float theta, float phi)
        {
            return GetBasePoint(theta, phi) + GetBaseNormal(theta, phi) * GetHeight(theta, phi);
        }

        /// <summary>Outward surface normal including terrain height.</summary>
        public Vector3 GetNormal(TorusAngles angles)
        {
            return GetNormal(angles.Theta, angles.Phi);
        }

        /// <summary>Outward surface normal for angles in radians.</summary>
        public Vector3 GetNormal(float theta, float phi)
        {
            var alongTheta = GetPoint(theta + DerivativeStep, phi) - GetPoint(theta - DerivativeStep, phi);
            var alongPhi = GetPoint(theta, phi + DerivativeStep) - GetPoint(theta, phi - DerivativeStep);
            return TorusGeometry.GetNormalFromTangents(alongTheta, alongPhi);
        }

        /// <summary>Terrain height above the base tube. Neighbouring segment profiles are cross-faded near borders.</summary>
        public float GetHeight(TorusAngles angles)
        {
            return GetHeight(angles.Theta, angles.Phi);
        }

        /// <summary>Terrain height for angles in radians.</summary>
        public float GetHeight(float theta, float phi)
        {
            float wrapped = Mathf.Repeat(theta, TwoPi);
            float scaled = wrapped / SegmentAngle;
            int index = Mathf.Min(Mathf.FloorToInt(scaled), SegmentCount - 1);

            float height = TorusSegmentBlend.GetHeight(
                GetProfile(index),
                GetProfile(index - 1),
                GetProfile(index + 1),
                scaled - index,
                Mathf.Repeat(phi, TwoPi) / TwoPi,
                BlendWidth,
                GetBasePoint(wrapped, phi));

            return HeadTerrain == null ? height : HeadTerrain.Apply(height, wrapped, phi, MinorRadius);
        }

        /// <summary>Multiplier of the walking speed at the given place from the effect of its biome (water...). 1 = no effect.</summary>
        public float GetSpeedMultiplier(TorusAngles angles)
        {
            var effect = GetProfile(GetSegmentIndex(angles)).Effect;
            return effect ? effect.GetSpeedMultiplier(GetHeight(angles)) : 1f;
        }

        /// <summary>False when the effect of the biome (water) does not let the dodge work at the given place.</summary>
        public bool CanDodge(TorusAngles angles)
        {
            var effect = GetProfile(GetSegmentIndex(angles)).Effect;
            return effect == null || effect.CanDodge(GetHeight(angles));
        }

        private TorusSegmentProfile GetAssignedProfile(IReadOnlyList<TorusSegmentProfile> segmentProfiles, int segmentIndex)
        {
            if (segmentProfiles == null || segmentProfiles.Count == 0)
                return FlatTorusSegmentProfile.Instance;

            var profile = segmentProfiles[segmentIndex % segmentProfiles.Count];
            return profile ? profile : FlatTorusSegmentProfile.Instance;
        }
    }
}
