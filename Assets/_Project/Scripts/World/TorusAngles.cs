using System;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Position on the surface of the torus: theta (around the big circle, along the snake body)
    /// and phi (around the tube cross-section). Stored in radians and always wrapped to [0, 2π).
    /// The Inspector shows it in degrees. Like Vector2, but with angular arithmetic.
    /// </summary>
    [Serializable]
    public struct TorusAngles : IEquatable<TorusAngles>
    {
        private const float TwoPi = Mathf.PI * 2f;

        // Serialized in radians. The property drawer displays them in degrees.
        [SerializeField]
        private float thetaRadians;

        [SerializeField]
        private float phiRadians;

        /// <summary>Angle along the snake body, in radians [0, 2π).</summary>
        public float Theta => Mathf.Repeat(thetaRadians, TwoPi);

        /// <summary>Angle around the tube, in radians [0, 2π). 0 = outer side, π/2 = top.</summary>
        public float Phi => Mathf.Repeat(phiRadians, TwoPi);

        public float ThetaDegrees => Theta * Mathf.Rad2Deg;
        public float PhiDegrees => Phi * Mathf.Rad2Deg;

        public TorusAngles(float thetaRadians, float phiRadians)
        {
            this.thetaRadians = Mathf.Repeat(thetaRadians, TwoPi);
            this.phiRadians = Mathf.Repeat(phiRadians, TwoPi);
        }

        public static TorusAngles FromDegrees(float thetaDegrees, float phiDegrees)
        {
            return new TorusAngles(thetaDegrees * Mathf.Deg2Rad, phiDegrees * Mathf.Deg2Rad);
        }

        public TorusAngles WithTheta(float newThetaRadians)
        {
            return new TorusAngles(newThetaRadians, phiRadians);
        }

        public TorusAngles WithPhi(float newPhiRadians)
        {
            return new TorusAngles(thetaRadians, newPhiRadians);
        }

        /// <summary>Moves by the given angles (radians). The result is wrapped.</summary>
        public TorusAngles Add(float deltaTheta, float deltaPhi)
        {
            return new TorusAngles(thetaRadians + deltaTheta, phiRadians + deltaPhi);
        }

        /// <summary>Signed shortest angles (radians, each in [-π, π]) that lead from this position to the other one.</summary>
        public Vector2 DeltaTo(TorusAngles other)
        {
            return new Vector2(
                Mathf.DeltaAngle(Theta * Mathf.Rad2Deg, other.Theta * Mathf.Rad2Deg) * Mathf.Deg2Rad,
                Mathf.DeltaAngle(Phi * Mathf.Rad2Deg, other.Phi * Mathf.Rad2Deg) * Mathf.Deg2Rad);
        }

        /// <summary>Interpolates along the shortest way around both circles.</summary>
        public static TorusAngles Lerp(TorusAngles from, TorusAngles to, float t)
        {
            var delta = from.DeltaTo(to);
            return from.Add(delta.x * t, delta.y * t);
        }

        public bool Equals(TorusAngles other)
        {
            return Mathf.Approximately(Theta, other.Theta) && Mathf.Approximately(Phi, other.Phi);
        }

        public override bool Equals(object obj)
        {
            return obj is TorusAngles other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Theta, Phi);
        }

        public static bool operator ==(TorusAngles left, TorusAngles right) => left.Equals(right);

        public static bool operator !=(TorusAngles left, TorusAngles right) => left.Equals(right) == false;

        public override string ToString()
        {
            return $"(theta {ThetaDegrees:0.#}°, phi {PhiDegrees:0.#}°)";
        }
    }
}
