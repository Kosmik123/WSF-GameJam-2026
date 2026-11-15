using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Anything that stands on the torus: the player, NPCs, items. Its position is a pair of surface
    /// angles (theta, phi). Its local up always points along the minor radius of the torus (away from
    /// the centre of the tube), never along the terrain mesh normal. Only the height follows the terrain.
    /// Other components (player input, AI) only decide where it turns and moves.
    /// </summary>
    public class TorusObject : MonoBehaviour
    {
        [SerializeField]
        private TorusWorld world;

        [Header("Placement")]
        [Tooltip("Distance of the object's pivot from the ground.")]
        [SerializeField]
        [Min(0f)]
        private float groundOffset = 1f;

        [Tooltip("Radius of the object for the props that can not be entered (see TorusPropDefinition). 0 = its centre only.")]
        [SerializeField]
        [Min(0f)]
        private float bodyRadius = 0.5f;

        [Tooltip("Can not cross the wall at the theta of the head (the player). See HeadBarrier.")]
        [SerializeField]
        private bool isBlockedByHeadBarrier;

        [Tooltip("The effect of the biome (water) slows the object down. Turn off for flying things. The movement scripts read GroundSpeedMultiplier.")]
        [SerializeField]
        private bool isSlowedByTerrain = true;

        [Tooltip("How fast the object follows the terrain height. Higher = tighter.")]
        [SerializeField]
        [Min(0f)]
        private float heightSharpness = 25f;

        [Header("Start")]
        [Tooltip("Start position in degrees. Theta: along the snake body. Phi: around the tube, 0 = outer side of the torus, 90 = top.")]
        [SerializeField]
        private TorusAngles startAngles = TorusAngles.FromDegrees(0f, 90f);

        [Header("States")]
        [SerializeField]
        private TorusAngles angles;

        private float smoothedHeight;
        private bool shouldSnap = true;

        // Frame of the base tube in the local space of the world.
        private Vector3 localPosition;
        private Vector3 localUp = Vector3.up;
        private Vector3 localRunDirection = Vector3.forward;
        private Vector3 localFacing = Vector3.forward;

        public TorusWorld World => world;

        /// <summary>Current position on the surface of the torus.</summary>
        public TorusAngles Angles => angles;

        /// <summary>Multiplier of the walking speed from the biome the object stands in (water...). 1 = none. Walking scripts apply it, knockback and dashes do not.</summary>
        public float GroundSpeedMultiplier { get; private set; } = 1f;

        /// <summary>Up of the object: the minor radius direction of the torus, in world space.</summary>
        public Vector3 Up { get; private set; } = Vector3.up;

        /// <summary>Direction of growing theta (along the snake body), in world space.</summary>
        public Vector3 RunDirection { get; private set; } = Vector3.forward;

        /// <summary>Direction the object is facing, in world space.</summary>
        public Vector3 Facing { get; private set; } = Vector3.forward;

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
        }

        private void OnEnable()
        {
            if (world == null)
                return;

            SetPosition(startAngles);
            localFacing = localRunDirection;
            ApplyTransform();
        }

        /// <summary>Puts the object at the given surface angles, without smoothing.</summary>
        public void SetPosition(TorusAngles newAngles)
        {
            angles = newAngles;
            shouldSnap = true;
            Refresh();
        }

        /// <summary>Turns around its up. A positive angle (radians) turns right.</summary>
        public void Turn(float radians)
        {
            if (world == null)
                return;

            localFacing = Quaternion.AngleAxis(radians * Mathf.Rad2Deg, localUp) * localFacing;
            ApplyTransform();
        }

        /// <summary>Moves along the facing direction. A negative distance moves back.</summary>
        public void Move(float distance)
        {
            if (world == null)
                return;

            MoveAlongLocal(localFacing, distance);
        }

        /// <summary>Moves along a world space direction (projected on the surface) without changing the facing. Used by knockback and dodge.</summary>
        public void MoveInDirection(Vector3 worldDirection, float distance)
        {
            if (world == null)
                return;

            var localDirection = Vector3.ProjectOnPlane(world.transform.InverseTransformDirection(worldDirection), localUp);
            if (localDirection.sqrMagnitude < 0.0001f)
                return;

            MoveAlongLocal(localDirection.normalized, distance);
        }

        /// <summary>Turns towards a world space direction by at most the given angle (radians).</summary>
        public void TurnTowards(Vector3 worldDirection, float maxRadians)
        {
            if (world == null)
                return;

            var target = Vector3.ProjectOnPlane(world.transform.InverseTransformDirection(worldDirection), localUp);
            if (target.sqrMagnitude < 0.0001f)
                return;

            float signedDegrees = Vector3.SignedAngle(localFacing, target, localUp);
            float maxDegrees = maxRadians * Mathf.Rad2Deg;
            localFacing = Quaternion.AngleAxis(Mathf.Clamp(signedDegrees, -maxDegrees, maxDegrees), localUp) * localFacing;
            ApplyTransform();
        }

        /// <summary>Puts the object in a world (also for objects spawned from prefabs), at the given angles, facing the given world direction.</summary>
        public void Place(TorusWorld newWorld, TorusAngles newAngles, Vector3 worldFacing)
        {
            world = newWorld;
            if (world == null)
                return;

            SetPosition(newAngles);
            localFacing = world.transform.InverseTransformDirection(worldFacing);
            Refresh();
        }

        /// <summary>
        /// Offset from this object to the given angles, along the surface, in world space. It lies in the tangent plane
        /// of this object and its length is the approximate surface distance (exact for short distances).
        /// Unlike a straight line, it never goes through the tube, so an enemy on the underside is not "close".
        /// </summary>
        public Vector3 GetSurfaceOffsetTo(TorusAngles target)
        {
            if (world == null)
                return Vector3.zero;

            var surface = world.Surface;
            var delta = angles.DeltaTo(target);
            float middlePhi = angles.Phi + delta.y * 0.5f;
            float thetaDistance = delta.x * TorusGeometry.GetRingRadius(surface.MajorRadius, surface.MinorRadius, middlePhi);
            float phiDistance = delta.y * surface.MinorRadius;

            var localOffset = localRunDirection * thetaDistance + TorusGeometry.GetPhiDirection(angles.Theta, angles.Phi) * phiDistance;
            return world.transform.TransformDirection(localOffset);
        }

        /// <summary>Surface offset to another object on the same torus. See <see cref="GetSurfaceOffsetTo(TorusAngles)"/>.</summary>
        public Vector3 GetSurfaceOffsetTo(TorusObject other)
        {
            return GetSurfaceOffsetTo(other.Angles);
        }

        private void MoveAlongLocal(Vector3 localDirection, float distance)
        {
            // Directions of growing theta and phi are perpendicular, so the speed stays constant.
            float theta = angles.Theta;
            float phi = angles.Phi;
            var phiDirection = TorusGeometry.GetPhiDirection(theta, phi);

            var surface = world.Surface;
            float thetaDistance = Vector3.Dot(localDirection, localRunDirection) * distance;
            float phiDistance = Vector3.Dot(localDirection, phiDirection) * distance;

            var delta = TorusGeometry.GetAngleDelta(surface.MajorRadius, surface.MinorRadius, phi, thetaDistance, phiDistance);
            var moved = world.Obstacles.Resolve(surface, angles.Add(delta.x, delta.y), bodyRadius);
            angles = isBlockedByHeadBarrier ? world.HeadBarrier.Constrain(angles, moved) : moved;
            Refresh();
        }

        /// <summary>Recalculates the placement on the terrain. Call it after the world was rebuilt.</summary>
        public void Refresh()
        {
            if (world == null)
                return;

            UpdateFrame(world.Surface);
            UpdateFacing();
            ApplyTransform();
        }

        private void UpdateFrame(TorusSurface surface)
        {
            float heightBlend = shouldSnap ? 1f : 1f - Mathf.Exp(-heightSharpness * Time.deltaTime);
            shouldSnap = false;
            smoothedHeight = Mathf.Lerp(smoothedHeight, surface.GetHeight(angles), heightBlend);

            GroundSpeedMultiplier = isSlowedByTerrain ? surface.GetSpeedMultiplier(angles) : 1f;
            localUp = surface.GetBaseNormal(angles);
            localRunDirection = TorusGeometry.GetThetaDirection(angles.Theta);
            localPosition = surface.GetBasePoint(angles) + localUp * smoothedHeight;
        }

        private void UpdateFacing()
        {
            var projected = Vector3.ProjectOnPlane(localFacing, localUp);
            localFacing = projected.sqrMagnitude < 0.0001f ? localRunDirection : projected.normalized;
        }

        private void ApplyTransform()
        {
            var worldTransform = world.transform;
            Up = worldTransform.TransformDirection(localUp);
            RunDirection = worldTransform.TransformDirection(localRunDirection);
            Facing = worldTransform.TransformDirection(localFacing);

            transform.SetPositionAndRotation(
                worldTransform.TransformPoint(localPosition + localUp * groundOffset),
                Quaternion.LookRotation(Facing, Up));
        }

        private void OnValidate()
        {
            Refresh();
        }
    }
}
