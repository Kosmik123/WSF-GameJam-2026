using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Ray against the terrain of the torus, without colliders: marches along the ray and compares the distance from the
    /// tube with the terrain height of <see cref="TorusSurface"/> (so it also sees the terrain shaped by the head).
    /// Everything is in the local space of the world.
    /// </summary>
    public static class TorusRaycast
    {
        private const int MaxSteps = 192;
        private const int RefineSteps = 12;
        private const float MinStep = 0.02f;

        // The terrain height bends the distance to the tube, so the march takes only a part of it per step.
        private const float StepScale = 0.7f;

        /// <summary>
        /// Finds the first point where the ray goes under the terrain. False when it misses (looks at the sky)
        /// or starts under the terrain. <paramref name="maxStep"/> limits a single step, so thin bumps are not skipped.
        /// </summary>
        public static bool TryRaycast(TorusSurface surface, Ray localRay, float maxDistance, float maxStep, out TorusAngles hitAngles, out Vector3 hitPoint)
        {
            hitAngles = default;
            hitPoint = default;

            var origin = localRay.origin;
            var direction = localRay.direction.normalized;
            float clearance = GetClearance(surface, origin, out _);
            if (clearance <= 0f)
                return false;

            float distance = 0f;
            for (int i = 0; i < MaxSteps && distance < maxDistance; i++)
            {
                float previousDistance = distance;
                distance += Mathf.Clamp(clearance * StepScale, MinStep, maxStep);
                clearance = GetClearance(surface, origin + direction * distance, out _);
                if (clearance > 0f)
                    continue;

                hitPoint = Refine(surface, origin, direction, previousDistance, distance, out hitAngles);
                return true;
            }

            return false;
        }

        /// <summary>Height of a point above the terrain (negative = under it), and the surface angles under the point.</summary>
        public static float GetClearance(TorusSurface surface, Vector3 localPoint, out TorusAngles angles)
        {
            var coordinates = TorusGeometry.GetTubeCoordinates(surface.MajorRadius, localPoint);
            angles = new TorusAngles(coordinates.x, coordinates.y);
            return coordinates.z - surface.MinorRadius - surface.GetHeight(angles);
        }

        private static Vector3 Refine(TorusSurface surface, Vector3 origin, Vector3 direction, float above, float below, out TorusAngles angles)
        {
            for (int i = 0; i < RefineSteps; i++)
            {
                float middle = (above + below) * 0.5f;
                bool isAbove = GetClearance(surface, origin + direction * middle, out _) > 0f;
                above = isAbove ? middle : above;
                below = isAbove ? below : middle;
            }

            var point = origin + direction * ((above + below) * 0.5f);
            GetClearance(surface, point, out angles);
            return point;
        }
    }
}
