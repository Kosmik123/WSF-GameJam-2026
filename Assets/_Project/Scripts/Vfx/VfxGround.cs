using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Ground under a point of the torus, without physics: the point is projected on the tube of the world and lifted
    /// by the terrain height. Effects on the ground (rings, scorch marks, ice) are placed with it and the up of that spot.
    /// </summary>
    public static class VfxGround
    {
        private static TorusWorld cachedWorld;

        private static TorusWorld World
        {
            get
            {
                if (cachedWorld == null)
                    cachedWorld = Object.FindAnyObjectByType<TorusWorld>();

                return cachedWorld;
            }
        }

        /// <summary>Ground under the object and the up of that spot.</summary>
        public static Vector3 Below(TorusObject torusObject, out Vector3 up)
        {
            if (torusObject.World == null)
            {
                up = torusObject.transform.up;
                return torusObject.transform.position;
            }

            return At(torusObject.World, torusObject.Angles, out up);
        }

        /// <summary>Ground under any world point (a projectile, an enemy, a point of a lightning). Without a world it returns the point and world up.</summary>
        public static Vector3 Below(Vector3 position, out Vector3 up)
        {
            var world = World;
            if (world == null)
            {
                up = Vector3.up;
                return position;
            }

            return At(world, ToAngles(world, position), out up);
        }

        /// <summary>Ground at the given angles, with the terrain height, and its up (the base normal, like <see cref="TorusObject.Up"/>).</summary>
        public static Vector3 At(TorusWorld world, TorusAngles angles, out Vector3 up)
        {
            var surface = world.Surface;
            var localUp = surface.GetBaseNormal(angles);
            var localPoint = surface.GetBasePoint(angles) + localUp * surface.GetHeight(angles);
            up = world.transform.TransformDirection(localUp).normalized;
            return world.transform.TransformPoint(localPoint);
        }

        /// <summary>Angles of the spot of the surface closest to a world point.</summary>
        public static TorusAngles ToAngles(TorusWorld world, Vector3 position)
        {
            var local = world.transform.InverseTransformPoint(position);
            float theta = Mathf.Atan2(local.x, local.z);
            var center = TorusGeometry.GetCenterPoint(world.Surface.MajorRadius, theta);
            var offset = local - center;
            var radial = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
            float phi = Mathf.Atan2(offset.y, Vector3.Dot(offset, radial));
            return new TorusAngles(theta, phi);
        }

        /// <summary>Two unit directions in the tangent plane of <paramref name="up"/>, perpendicular to each other.</summary>
        public static void GetTangents(Vector3 up, out Vector3 right, out Vector3 forward)
        {
            var reference = Mathf.Abs(Vector3.Dot(up, Vector3.forward)) > 0.9f ? Vector3.right : Vector3.forward;
            right = Vector3.Cross(up, reference).normalized;
            forward = Vector3.Cross(right, up);
        }
    }
}
