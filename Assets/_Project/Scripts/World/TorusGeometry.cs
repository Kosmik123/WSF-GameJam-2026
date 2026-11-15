using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// The only place that knows how torus angles map to Unity's left-handed space.
    /// Theta 0 is on +Z and grows towards +X. Phi 0 is the outer side of the torus and grows upwards (+Y).
    /// All sines, cosines and cross product orders that depend on this convention live here,
    /// so changing the convention means changing only this class.
    /// All results are in the local space of the world and all angles are in radians.
    /// </summary>
    public static class TorusGeometry
    {
        public const float TwoPi = Mathf.PI * 2f;

        /// <summary>Distance of the point from the axis of the torus (the Y axis).</summary>
        public static float GetRingRadius(float majorRadius, float minorRadius, float phi)
        {
            return majorRadius + minorRadius * Mathf.Cos(phi);
        }

        /// <summary>Point of the tube centre line (phi independent) for the given theta.</summary>
        public static Vector3 GetCenterPoint(float majorRadius, float theta)
        {
            return new Vector3(majorRadius * Mathf.Sin(theta), 0f, majorRadius * Mathf.Cos(theta));
        }

        /// <summary>Point of the base tube surface.</summary>
        public static Vector3 GetPoint(float majorRadius, float minorRadius, float theta, float phi)
        {
            float ring = GetRingRadius(majorRadius, minorRadius, phi);
            return new Vector3(ring * Mathf.Sin(theta), minorRadius * Mathf.Sin(phi), ring * Mathf.Cos(theta));
        }

        /// <summary>Outward unit normal of the base tube surface.</summary>
        public static Vector3 GetNormal(float theta, float phi)
        {
            float cosPhi = Mathf.Cos(phi);
            return new Vector3(cosPhi * Mathf.Sin(theta), Mathf.Sin(phi), cosPhi * Mathf.Cos(theta));
        }

        /// <summary>Unit tangent in the direction of growing theta (along the snake body).</summary>
        public static Vector3 GetThetaDirection(float theta)
        {
            return new Vector3(Mathf.Cos(theta), 0f, -Mathf.Sin(theta));
        }

        /// <summary>Unit tangent in the direction of growing phi (around the tube). Perpendicular to the theta direction.</summary>
        public static Vector3 GetPhiDirection(float theta, float phi)
        {
            float sinPhi = Mathf.Sin(phi);
            return new Vector3(-sinPhi * Mathf.Sin(theta), Mathf.Cos(phi), -sinPhi * Mathf.Cos(theta));
        }

        /// <summary>Outward normal of a surface from its tangents in the directions of growing theta and phi (not necessarily unit).</summary>
        public static Vector3 GetNormalFromTangents(Vector3 alongTheta, Vector3 alongPhi)
        {
            return Vector3.Cross(alongPhi, alongTheta).normalized;
        }

        /// <summary>Angle change (theta, phi) after moving by a distance along the surface in a direction given by components on the tangents.</summary>
        public static Vector2 GetAngleDelta(float majorRadius, float minorRadius, float phi, float thetaDistance, float phiDistance)
        {
            float ring = Mathf.Max(0.1f, GetRingRadius(majorRadius, minorRadius, phi));
            return new Vector2(thetaDistance / ring, phiDistance / minorRadius);
        }
    }
}
