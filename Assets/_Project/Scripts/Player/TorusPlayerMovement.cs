using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Player input for a <see cref="TorusObject"/>. The keys move the character in the frame of the camera (up = away from the
    /// camera along the surface), the character faces the mouse cursor (so he also shoots there). While the right mouse button
    /// is held the camera turns instead: the cursor is hidden and the character keeps his facing.
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    public class TorusPlayerMovement : MonoBehaviour
    {
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

        [Tooltip("How fast the character turns towards the cursor, in degrees per second.")]
        [SerializeField]
        [Min(0f)]
        private float turnSpeedDegrees = 720f;

        [Tooltip("Transform whose forward gives the 'up' of the input (the camera). Empty = the main camera.")]
        [SerializeField]
        private Transform directionProvider;

        private TorusObject torusObject;
        public TorusObject TorusObject => torusObject;

        private Vector2 smoothedInput;

        /// <summary>Smoothed input: x = right, y = away from the camera.</summary>
        public Vector2 SmoothedInput => smoothedInput;

        /// <summary>True while the right mouse button is held: the camera is turned with the mouse and the cursor is hidden.</summary>
        public bool IsOrbiting { get; private set; }

        /// <summary>Direction the player is moving in (world space, along the surface, unit length). Zero without input.</summary>
        public Vector3 MoveDirection { get; private set; }

        /// <summary>Multiplier of the move speed, set by combat (slower while attacking). 1 = normal.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Multiplier of the turn speed, set by combat. 1 = normal.</summary>
        public float TurnMultiplier { get; set; } = 1f;

        /// <summary>When true, input does not move the character (dodge, knockback, death).</summary>
        public bool IsLocked { get; set; }

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
        }

        private void OnEnable()
        {
            smoothedInput = Vector2.zero;
            MoveDirection = Vector3.zero;

            IsOrbiting = false;
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        private void Update()
        {
            smoothedInput = Vector2.MoveTowards(smoothedInput, ReadInput(), inputResponse * Time.deltaTime);
            MoveDirection = GetWorldDirection(smoothedInput);
            UpdateOrbiting();
            if (IsLocked)
                return;

            if (IsOrbiting == false && TryGetAimDirection(out var aimDirection))
                torusObject.TurnTowards(aimDirection, turnSpeedDegrees * TurnMultiplier * Mathf.Deg2Rad * Time.deltaTime);

            if (MoveDirection != Vector3.zero)
                torusObject.MoveInDirection(MoveDirection, smoothedInput.magnitude * moveSpeed * SpeedMultiplier * torusObject.GroundSpeedMultiplier * Time.deltaTime);

            torusObject.Refresh();  
        }

        private void UpdateOrbiting()
        {
            var mouse = Mouse.current;
            IsOrbiting = mouse != null && mouse.rightButton.isPressed;
            Cursor.lockState = IsOrbiting ? CursorLockMode.Locked : CursorLockMode.Confined;
            Cursor.visible = IsOrbiting == false;
        }

        // Direction from the character to the point of the ground plane under the cursor.
        private bool TryGetAimDirection(out Vector3 direction)
        {
            direction = Vector3.zero;
            var mouse = Mouse.current;
            var mainCamera = Camera.main;
            if (mouse == null || mainCamera == null)
                return false;

            var ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            var plane = new Plane(torusObject.Up, transform.position);
            if (plane.Raycast(ray, out var distance) == false)
                return false;

            var offset = Vector3.ProjectOnPlane(ray.GetPoint(distance) - transform.position, torusObject.Up);
            if (offset.sqrMagnitude < 0.04f)
                return false;

            direction = offset.normalized;
            return true;
        }

        // Input (x = right, y = forward) in the frame of the camera, flattened to the surface under the player.
        private Vector3 GetWorldDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            var reference = GetDirectionProvider();
            if (reference == null)
                return Vector3.zero;

            var up = torusObject.Up;
            var forward = Vector3.ProjectOnPlane(reference.forward, up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(reference.up, up); // looking straight down or up

            if (forward.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            forward.Normalize();
            var right = Vector3.Cross(up, forward);
            return (forward * input.y + right * input.x).normalized;
        }

        private Transform GetDirectionProvider()
        {
            if (directionProvider)
                return directionProvider;

            var mainCamera = Camera.main;
            return mainCamera ? mainCamera.transform : transform;
        }

        private Vector2 ReadInput()
        {
            if (moveAction == null)
                return Vector2.zero;

            return Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        }

        private void OnDisable()
        {
            smoothedInput = Vector2.zero;
            MoveDirection = Vector3.zero;
            IsOrbiting = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
