using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Where the player aims: the point on the torus under the mouse cursor (found by <see cref="TorusRaycast"/>, no colliders),
    /// or the right stick of a gamepad (relative to the screen) when it was used after the last mouse move.
    /// Gives the aim as a direction in the tangent plane of the player. <see cref="TorusPlayerMovement"/> turns the player towards it,
    /// so the melee swing, the mage shot and the skills (they use the facing) go towards the cursor.
    /// Runs before the movement, so the movement uses the aim of the same frame.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(TorusObject))]
    public class PlayerAim : MonoBehaviour
    {
        [Tooltip("Screen position of the pointer (Player/AimPoint, bound to <Pointer>/position).")]
        [SerializeField]
        private InputActionReference pointerAction;

        [Tooltip("Gamepad aim (Player/Look, right stick). Used from the moment the stick moves until the mouse moves again.")]
        [SerializeField]
        private InputActionReference stickAction;

        [Tooltip("Camera that shows the player. Empty = the main camera.")]
        [SerializeField]
        private Camera viewCamera;

        [Header("Cursor")]
        [Tooltip("How far along the camera ray the ground is searched for (units of the world's local space).")]
        [SerializeField]
        [Min(1f)]
        private float maxRayDistance = 250f;

        [Tooltip("Longest single step of the ray march. Smaller = does not skip thin bumps, but costs more.")]
        [SerializeField]
        [Min(0.1f)]
        private float maxRayStep = 1.5f;

        [Tooltip("A cursor closer to the player than this (along the surface) does not change the aim, so the player does not spin on the spot.")]
        [SerializeField]
        [Min(0f)]
        private float deadRadius = 0.6f;

        [Tooltip("Pointer movement (pixels per frame) that switches the aim back to the mouse.")]
        [SerializeField]
        [Min(0f)]
        private float pointerMoveThreshold = 1f;

        [Header("Stick")]
        [SerializeField]
        [Range(0f, 0.95f)]
        private float stickDeadZone = 0.35f;

        private TorusObject torusObject;
        private Vector2 pointerPosition;

        [Header("States")]
        [SerializeField]
        private bool isUsingStick;
        public bool IsUsingStick => isUsingStick;

        [SerializeField]
        private bool hasCursorPoint;
        /// <summary>True when the cursor is over the terrain this frame (not over the sky).</summary>
        public bool HasCursorPoint => hasCursorPoint;

        [SerializeField]
        private TorusAngles cursorAngles;
        /// <summary>Surface angles of the point under the cursor. Valid when <see cref="HasCursorPoint"/>.</summary>
        public TorusAngles CursorAngles => cursorAngles;

        /// <summary>World position of the point under the cursor. Valid when <see cref="HasCursorPoint"/>.</summary>
        public Vector3 CursorPoint { get; private set; }

        /// <summary>True when there is an aim this frame (the cursor is not on the player, the stick is not idle).</summary>
        public bool HasAimDirection { get; private set; }

        /// <summary>Unit direction of the aim in the tangent plane of the player, in world space. Valid when <see cref="HasAimDirection"/>.</summary>
        public Vector3 AimDirection { get; private set; }

        public Camera ViewCamera => viewCamera ? viewCamera : Camera.main;

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
        }

        private void OnEnable()
        {
            pointerPosition = ReadVector(pointerAction);
            isUsingStick = false;
        }

        /// <summary>
        /// Screen axes on the surface under the player: forward = up the screen (away from the camera), right = right on the screen.
        /// Both unit, in the tangent plane of the player. False when there is no camera.
        /// </summary>
        public bool TryGetScreenAxes(out Vector3 forward, out Vector3 right)
        {
            var up = torusObject.Up;
            var camera = ViewCamera;
            forward = Vector3.zero;
            right = Vector3.zero;
            if (camera == null)
                return false;

            var cameraTransform = camera.transform;
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, up);

            // The camera looks straight down: its up is the screen's up.
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(cameraTransform.up, up);

            forward.Normalize();
            right = Vector3.Cross(up, forward);
            return forward.sqrMagnitude > 0.5f;
        }

        private void Update()
        {
            HasAimDirection = false;
            hasCursorPoint = false;

            // The Player map is off (menus): the pointer reads zero, so there is no aim.
            if (pointerAction && pointerAction.action.enabled == false)
                return;

            UpdateDevice();

            if (isUsingStick)
            {
                AimWithStick();
                return;
            }

            AimWithPointer();
        }

        private void UpdateDevice()
        {
            var newPointerPosition = ReadVector(pointerAction);
            bool hasPointerMoved = (newPointerPosition - pointerPosition).sqrMagnitude > pointerMoveThreshold * pointerMoveThreshold;
            pointerPosition = newPointerPosition;
            if (hasPointerMoved)
            {
                isUsingStick = false;
                return;
            }

            // Look also has the mouse delta, but the mouse did not move, so a value here comes from the stick.
            if (ReadVector(stickAction).sqrMagnitude > stickDeadZone * stickDeadZone)
                isUsingStick = true;
        }

        private void AimWithStick()
        {
            var stick = ReadVector(stickAction);
            if (stick.sqrMagnitude <= stickDeadZone * stickDeadZone)
                return;

            if (TryGetScreenAxes(out var forward, out var right))
                SetAim(right * stick.x + forward * stick.y, 0f);
        }

        private void AimWithPointer()
        {
            var camera = ViewCamera;
            var world = torusObject.World;
            if (camera == null || world == null || pointerAction == null)
                return;

            var ray = camera.ScreenPointToRay(pointerPosition);
            var worldTransform = world.transform;
            var localRay = new Ray(worldTransform.InverseTransformPoint(ray.origin), worldTransform.InverseTransformDirection(ray.direction));
            if (TorusRaycast.TryRaycast(world.Surface, localRay, maxRayDistance, maxRayStep, out var angles, out var localPoint))
            {
                hasCursorPoint = true;
                cursorAngles = angles;
                CursorPoint = worldTransform.TransformPoint(localPoint);
                SetAim(torusObject.GetSurfaceOffsetTo(angles), deadRadius);
                return;
            }

            // The cursor is over the sky (above the horizon): aim along the ray, flattened on the ground.
            SetAim(Vector3.ProjectOnPlane(ray.direction, torusObject.Up), 0f);
        }

        private void SetAim(Vector3 direction, float minLength)
        {
            var flat = Vector3.ProjectOnPlane(direction, torusObject.Up);
            if (flat.sqrMagnitude <= Mathf.Max(0.0001f, minLength * minLength))
                return;

            AimDirection = flat.normalized;
            HasAimDirection = true;
        }

        private static Vector2 ReadVector(InputActionReference actionReference)
        {
            return actionReference ? actionReference.action.ReadValue<Vector2>() : Vector2.zero;
        }

        private void OnDrawGizmosSelected()
        {
            if (hasCursorPoint == false)
                return;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, CursorPoint);
            Gizmos.DrawWireSphere(CursorPoint, 0.3f);
        }
    }
}
