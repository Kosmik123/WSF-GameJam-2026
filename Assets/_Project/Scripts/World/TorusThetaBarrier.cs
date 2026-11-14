using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// A wall across the whole tube at one value of theta, the same for every phi. Objects marked as blocked (see
    /// <see cref="TorusObject"/>) can not cross it in either direction: a move that would take them over the wall
    /// leaves them right beside it, on the side they came from. The head of the snake sets it, so the player can not walk onto the head.
    /// </summary>
    public class TorusThetaBarrier
    {
        private const float TwoPi = TorusGeometry.TwoPi;

        /// <summary>How close to the wall an object that was stopped by it stays, in radians of theta.</summary>
        private const float Margin = 0.0005f;

        public bool IsActive { get; private set; }

        /// <summary>Theta of the wall, in radians.</summary>
        public float Theta { get; private set; }

        public void Set(float theta)
        {
            Theta = Mathf.Repeat(theta, TwoPi);
            IsActive = true;
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        /// <summary>Returns the position after a move from <paramref name="from"/> to <paramref name="to"/>: the target, or beside the wall when the move crosses it.</summary>
        public TorusAngles Constrain(TorusAngles from, TorusAngles to)
        {
            if (IsActive == false)
                return to;

            float before = GetSignedDistance(from.Theta);
            float after = GetSignedDistance(to.Theta);

            // Crossing the wall changes the side. A jump over the opposite side of the torus (more than half a circle) is not a crossing.
            bool isCrossing = before * after < 0f && Mathf.Abs(before) + Mathf.Abs(after) < Mathf.PI;
            if (isCrossing == false)
                return to;

            return to.WithTheta(Theta + Mathf.Sign(before) * Margin);
        }

        // Signed angle from the wall to the given theta, in (-π, π].
        private float GetSignedDistance(float theta)
        {
            return Mathf.Repeat(theta - Theta + Mathf.PI, TwoPi) - Mathf.PI;
        }
    }
}
