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
        private const float TwoPi = Mathf.PI * 2f;

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
        [Tooltip("Start position along the snake body, in degrees (0-360).")]
        [SerializeField]
        private float startTheta;

        [Tooltip("Start position around the tube, in degrees. 0 = outer side of the torus, 90 = top.")]
        [SerializeField]
        private float startPhi = 90f;

        [Header("States")]
        [SerializeField]
        private float theta;

        [SerializeField]
        private float phi;

        private float smoothedHeight;
        private bool shouldSnap = true;

        // Frame of the base tube in the local space of the world.
        private Vector3 localPosition;
        private Vector3 localUp = Vector3.up;
        private Vector3 localRunDirection = Vector3.forward;
        private Vector3 localFacing = Vector3.forward;

        public TorusWorld World => world;

        /// <summary>Angle along the snake body, in radians.</summary>
        public float Theta => theta;

        /// <summary>Angle around the tube, in radians.</summary>
        public float Phi => phi;

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

            SetPosition(startTheta * Mathf.Deg2Rad, startPhi * Mathf.Deg2Rad);
            localFacing = localRunDirection;
            ApplyTransform();
        }

        /// <summary>Puts the object at the given surface angles (radians), without smoothing.</summary>
        public void SetPosition(float newTheta, float newPhi)
        {
            theta = Mathf.Repeat(newTheta, TwoPi);
            phi = Mathf.Repeat(newPhi, TwoPi);
            shouldSnap = true;
            Refresh();
        }

        /// <summary>Turns around its up. A positive angle (degrees) turns right.</summary>
        public void Turn(float degrees)
        {
            if (world == null)
                return;

            localFacing = Quaternion.AngleAxis(degrees, localUp) * localFacing;
            ApplyTransform();
        }

        /// <summary>Moves along the facing direction. A negative distance moves back.</summary>
        public void Move(float distance)
        {
            if (world == null)
                return;

            // Directions of growing theta and phi are perpendicular, so the speed stays constant.
            var phiDirection = new Vector3(-Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), -Mathf.Sin(phi) * Mathf.Sin(theta));

            var surface = world.Surface;
            float thetaDistance = Vector3.Dot(localFacing, localRunDirection) * distance;
            float phiDistance = Vector3.Dot(localFacing, phiDirection) * distance;

            float ringRadius = Mathf.Max(0.1f, surface.MajorRadius + surface.MinorRadius * Mathf.Cos(phi));
            theta = Mathf.Repeat(theta + thetaDistance / ringRadius, TwoPi);
            phi = Mathf.Repeat(phi + phiDistance / surface.MinorRadius, TwoPi);
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
            smoothedHeight = Mathf.Lerp(smoothedHeight, surface.GetHeight(theta, phi), heightBlend);

            localUp = surface.GetBaseNormal(theta, phi);
            localRunDirection = new Vector3(-Mathf.Sin(theta), 0f, Mathf.Cos(theta));
            localPosition = surface.GetBasePoint(theta, phi) + localUp * smoothedHeight;
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
