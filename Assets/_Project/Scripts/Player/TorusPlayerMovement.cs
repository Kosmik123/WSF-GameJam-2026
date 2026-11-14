using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Moves the player over the torus surface. The position is kept as surface angles (theta, phi),
    /// so the player always stands on the terrain, at the height given by the segment profiles.
    /// Tank-like controls: left/right on the input turns the character, up/down moves it forward/back
    /// along the direction it faces. Speed is measured on the smooth base tube, so rough terrain
    /// doesn't make the movement erratic.
    /// </summary>
    public class TorusPlayerMovement : MonoBehaviour
    {
        private const float TwoPi = Mathf.PI * 2f;

        [SerializeField]
        private TorusWorld world;

        [SerializeField]
        private InputActionReference moveAction;

        [Header("Movement")]
        [Tooltip("Speed over the world in units per second.")]
        [SerializeField]
        [Min(0f)]
        private float moveSpeed = 8f;

        [Tooltip("How fast the input reaches its target value (1 = full input in 1 second).")]
        [SerializeField]
        [Min(0.1f)]
        private float inputResponse = 8f;

        [Tooltip("Turning speed of the character (left/right) in degrees per second.")]
        [SerializeField]
        [Min(0f)]
        private float turnSpeed = 180f;

        [Header("Terrain following")]
        [Tooltip("Distance of the character's pivot from the ground.")]
        [SerializeField]
        [Min(0f)]
        private float groundOffset = 1f;

        [Tooltip("Terrain slope is measured over this distance, so small bumps don't tilt the character.")]
        [SerializeField]
        [Min(0.1f)]
        private float slopeSampleDistance = 1.5f;

        [Tooltip("How fast the character follows the terrain height. Higher = tighter.")]
        [SerializeField]
        [Min(0f)]
        private float heightSharpness = 25f;

        [Tooltip("How fast the character follows the terrain tilt. Higher = tighter.")]
        [SerializeField]
        [Min(0f)]
        private float tiltSharpness = 12f;

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
        private float smoothedHeight;
        private bool shouldSnap = true;

        // Frame of the surface in the local space of the world.
        private Vector3 localPosition;
        private Vector3 localNormal = Vector3.up;
        private Vector3 localRunDirection = Vector3.forward;
        private Vector3 localFacing = Vector3.forward;
        private Vector3 localBaseNormal = Vector3.up;
        private Vector3 localBaseRunDirection = Vector3.forward;

        /// <summary>Angle along the snake body, in radians.</summary>
        public float Theta => theta;

        /// <summary>Angle around the tube, in radians.</summary>
        public float Phi => phi;

        /// <summary>Smoothed normal of the terrain under the player, in world space.</summary>
        public Vector3 SurfaceNormal { get; private set; } = Vector3.up;

        /// <summary>Normal of the smooth base tube under the player (without terrain), in world space.</summary>
        public Vector3 BaseNormal { get; private set; } = Vector3.up;

        /// <summary>Direction of running along the snake body (growing theta) on the base tube, in world space.</summary>
        public Vector3 RunDirection { get; private set; } = Vector3.forward;

        /// <summary>Direction the character is facing, in world space.</summary>
        public Vector3 Facing { get; private set; } = Vector3.forward;

        private void Reset()
        {
            world = FindFirstObjectByType<TorusWorld>();
        }

        private void OnEnable()
        {
            theta = Mathf.Repeat(startTheta * Mathf.Deg2Rad, TwoPi);
            phi = Mathf.Repeat(startPhi * Mathf.Deg2Rad, TwoPi);
            smoothedInput = Vector2.zero;
            shouldSnap = true;

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

            TorusSurface surface = world.Surface;
            smoothedInput = Vector2.MoveTowards(smoothedInput, ReadInput(), inputResponse * Time.deltaTime);

            Turn(smoothedInput.x * turnSpeed * Time.deltaTime);
            Move(surface, smoothedInput.y * moveSpeed * Time.deltaTime);

            UpdateFrame(surface);
            UpdateFacing();
            ApplyTransform();
        }

        private Vector2 ReadInput()
        {
            if (moveAction == null)
                return Vector2.zero;

            return Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        }

        private float GetRingRadius(TorusSurface surface)
        {
            return Mathf.Max(0.1f, surface.MajorRadius + surface.MinorRadius * Mathf.Cos(phi));
        }

        /// <summary>Turns the character around the surface normal. Positive angle turns right.</summary>
        private void Turn(float degrees)
        {
            localFacing = Quaternion.AngleAxis(degrees, localNormal) * localFacing;
        }

        /// <summary>Moves along the facing direction. Negative distance moves back.</summary>
        private void Move(TorusSurface surface, float distance)
        {
            // Directions of growing theta and phi on the base tube, in the local space of the world.
            var thetaDirection = new Vector3(-Mathf.Sin(theta), 0f, Mathf.Cos(theta));
            var phiDirection = new Vector3(-Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), -Mathf.Sin(phi) * Mathf.Sin(theta));

            float thetaDistance = Vector3.Dot(localFacing, thetaDirection) * distance;
            float phiDistance = Vector3.Dot(localFacing, phiDirection) * distance;

            theta = Mathf.Repeat(theta + thetaDistance / GetRingRadius(surface), TwoPi);
            phi = Mathf.Repeat(phi + phiDistance / surface.MinorRadius, TwoPi);
        }

        private void UpdateFrame(TorusSurface surface)
        {
            float thetaStep = slopeSampleDistance / GetRingRadius(surface);
            float phiStep = slopeSampleDistance / surface.MinorRadius;

            Vector3 alongTheta = surface.GetPoint(theta + thetaStep, phi) - surface.GetPoint(theta - thetaStep, phi);
            Vector3 alongPhi = surface.GetPoint(theta, phi + phiStep) - surface.GetPoint(theta, phi - phiStep);

            float heightBlend = shouldSnap ? 1f : 1f - Mathf.Exp(-heightSharpness * Time.deltaTime);
            float tiltBlend = shouldSnap ? 1f : 1f - Mathf.Exp(-tiltSharpness * Time.deltaTime);
            shouldSnap = false;

            smoothedHeight = Mathf.Lerp(smoothedHeight, surface.GetHeight(theta, phi), heightBlend);

            Vector3 targetNormal = Vector3.Cross(alongPhi, alongTheta).normalized;
            localNormal = Vector3.Slerp(localNormal, targetNormal, tiltBlend);
            localRunDirection = Vector3.ProjectOnPlane(alongTheta, localNormal).normalized;

            localPosition = surface.GetBasePoint(theta, phi) + surface.GetBaseNormal(theta, phi) * smoothedHeight;
            localBaseNormal = surface.GetBaseNormal(theta, phi);
            localBaseRunDirection = new Vector3(-Mathf.Sin(theta), 0f, Mathf.Cos(theta));
        }

        private void UpdateFacing()
        {
            Vector3 projected = Vector3.ProjectOnPlane(localFacing, localNormal);
            localFacing = projected.sqrMagnitude < 0.0001f ? localRunDirection : projected.normalized;
        }

        private void ApplyTransform()
        {
            Transform worldTransform = world.transform;
            SurfaceNormal = worldTransform.TransformDirection(localNormal);
            BaseNormal = worldTransform.TransformDirection(localBaseNormal);
            RunDirection = worldTransform.TransformDirection(localBaseRunDirection);
            Facing = worldTransform.TransformDirection(localFacing);

            transform.position = worldTransform.TransformPoint(localPosition + localNormal * groundOffset);
            transform.rotation = Quaternion.LookRotation(worldTransform.TransformDirection(localFacing), SurfaceNormal);
        }
    }
}
