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

        [Tooltip("How fast the object follows the terrain height. Higher = tighter.")]
        [SerializeField]
        [Min(0f)]
        private float heightSharpness = 25f;

        [Header("Start")]
        [Tooltip("Start position in degrees. Theta: along the snake body. Phi: around the tube, 0 = outer side of the torus, 90 = top.")]
        [SerializeField]
        private TorusAngles startAngles = TorusAngles.FromDegrees(0f, 90f);

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

            // Directions of growing theta and phi are perpendicular, so the speed stays constant.
            float theta = angles.Theta;
            float phi = angles.Phi;
            var phiDirection = new Vector3(-Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), -Mathf.Sin(phi) * Mathf.Sin(theta));

            var surface = world.Surface;
            float thetaDistance = Vector3.Dot(localFacing, localRunDirection) * distance;
            float phiDistance = Vector3.Dot(localFacing, phiDirection) * distance;

            float ringRadius = Mathf.Max(0.1f, surface.MajorRadius + surface.MinorRadius * Mathf.Cos(phi));
            angles = angles.Add(thetaDistance / ringRadius, phiDistance / surface.MinorRadius);
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

            localUp = surface.GetBaseNormal(angles);
            localRunDirection = new Vector3(-Mathf.Sin(angles.Theta), 0f, Mathf.Cos(angles.Theta));
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
    }
}
