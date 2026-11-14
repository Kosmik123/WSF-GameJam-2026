using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Moves the player over the torus surface. The position is kept as surface angles (theta, phi),
    /// so the player always stands on the terrain, at the height given by the segment profiles.
    /// Up/down on the input runs along the snake body, left/right goes around the tube.
    /// </summary>
    public class TorusPlayerMovement : MonoBehaviour
    {
        private const float TwoPi = Mathf.PI * 2f;
        private const float FrameStep = 0.002f;

        [SerializeField]
        private TorusWorld world;

        [SerializeField]
        private InputActionReference moveAction;

        [Header("Movement")]
        [Tooltip("Speed over the terrain in world units per second.")]
        [SerializeField]
        [Min(0f)]
        private float moveSpeed = 8f;

        [Tooltip("How fast the input reaches its target value (1 = full input in 1 second).")]
        [SerializeField]
        [Min(0.1f)]
        private float inputResponse = 8f;

        [Tooltip("Turning speed of the character in degrees per second.")]
        [SerializeField]
        [Min(0f)]
        private float turnSpeed = 720f;

        [Tooltip("Distance of the character's pivot from the ground.")]
        [SerializeField]
        [Min(0f)]
        private float groundOffset = 1f;

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

        private Vector2 smoothedInput;
        private float metersPerRadianTheta = 1f;
        private float metersPerRadianPhi = 1f;

        // Surface frame in the local space of the world.
        private Vector3 localPosition;
        private Vector3 localNormal = Vector3.up;
        private Vector3 localRunDirection = Vector3.forward;
        private Vector3 localRight = Vector3.right;
        private Vector3 localFacing = Vector3.forward;

        /// <summary>Angle along the snake body, in radians.</summary>
        public float Theta => theta;

        /// <summary>Angle around the tube, in radians.</summary>
        public float Phi => phi;

        /// <summary>Normal of the terrain under the player, in world space.</summary>
        public Vector3 SurfaceNormal { get; private set; } = Vector3.up;

        /// <summary>Direction of running along the snake body (growing theta), in world space.</summary>
        public Vector3 RunDirection { get; private set; } = Vector3.forward;

        private void Reset()
        {
            world = FindFirstObjectByType<TorusWorld>();
        }

        private void OnEnable()
        {
            theta = Mathf.Repeat(startTheta * Mathf.Deg2Rad, TwoPi);
            phi = Mathf.Repeat(startPhi * Mathf.Deg2Rad, TwoPi);
            smoothedInput = Vector2.zero;

            if (world == null)
                return;

            UpdateFrame(world.Surface);
            localFacing = localRunDirection;
            ApplyTransform();
        }

        private void Update()
        {
            if (world == null)
                return;

            smoothedInput = Vector2.MoveTowards(smoothedInput, ReadInput(), inputResponse * Time.deltaTime);
            Move(smoothedInput * (moveSpeed * Time.deltaTime));

            UpdateFrame(world.Surface);
            UpdateFacing();
            ApplyTransform();
        }

        private Vector2 ReadInput()
        {
            if (moveAction == null)
                return Vector2.zero;

            return Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        }

        private void Move(Vector2 distance)
        {
            // Right of the screen is the negative direction of phi (see UpdateFrame).
            theta = Mathf.Repeat(theta + distance.y / metersPerRadianTheta, TwoPi);
            phi = Mathf.Repeat(phi - distance.x / metersPerRadianPhi, TwoPi);
        }

        private void UpdateFrame(TorusSurface surface)
        {
            Vector3 alongTheta = surface.GetPoint(theta + FrameStep, phi) - surface.GetPoint(theta - FrameStep, phi);
            Vector3 alongPhi = surface.GetPoint(theta, phi + FrameStep) - surface.GetPoint(theta, phi - FrameStep);

            metersPerRadianTheta = Mathf.Max(0.01f, alongTheta.magnitude / (2f * FrameStep));
            metersPerRadianPhi = Mathf.Max(0.01f, alongPhi.magnitude / (2f * FrameStep));

            localPosition = surface.GetPoint(theta, phi);
            localNormal = Vector3.Cross(alongPhi, alongTheta).normalized;
            localRunDirection = alongTheta.normalized;
            localRight = -alongPhi.normalized;
        }

        private void UpdateFacing()
        {
            Vector3 projected = Vector3.ProjectOnPlane(localFacing, localNormal);
            localFacing = projected.sqrMagnitude < 0.0001f ? localRunDirection : projected.normalized;

            if (smoothedInput.sqrMagnitude < 0.01f)
                return;

            Vector3 desired = (localRunDirection * smoothedInput.y + localRight * smoothedInput.x).normalized;
            localFacing = Vector3.RotateTowards(localFacing, desired, turnSpeed * Mathf.Deg2Rad * Time.deltaTime, 0f);
        }

        private void ApplyTransform()
        {
            Transform worldTransform = world.transform;
            SurfaceNormal = worldTransform.TransformDirection(localNormal);
            RunDirection = worldTransform.TransformDirection(localRunDirection);

            transform.position = worldTransform.TransformPoint(localPosition + localNormal * groundOffset);
            transform.rotation = Quaternion.LookRotation(worldTransform.TransformDirection(localFacing), SurfaceNormal);
        }
    }
}
