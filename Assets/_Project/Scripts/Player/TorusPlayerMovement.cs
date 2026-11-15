using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Player input for a <see cref="TorusObject"/>. Two control schemes:
    /// aiming (default): the player faces the aim of <see cref="PlayerAim"/> (mouse cursor or right stick) and the move input
    /// goes relative to the screen (up = away from the camera); tank: left/right turns the character, up/down moves it
    /// forward/back along the direction it faces.
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    public class TorusPlayerMovement : MonoBehaviour
    {
        [SerializeField]
        private InputActionReference moveAction;

        [Tooltip("Old tank controls (A/D turn, W/S forward/back) instead of aiming with the mouse. Turn on 'Is Following Facing' of the TorusFollowCamera with it.")]
        [SerializeField]
        private bool isUsingTankControls;
        public bool IsUsingTankControls => isUsingTankControls;

        [Header("Movement")]
        [Tooltip("Speed over the world in units per second.")]
        [SerializeField]
        [Min(0f)]
        private float moveSpeed = 8f;

        [Tooltip("How fast the input reaches its target value (1 = full input in 1 second).")]
        [SerializeField]
        [Min(0.1f)]
        private float inputResponse = 8f;

        [Tooltip("Tank controls: turning speed of the character (left/right) in degrees per second.")]
        [FormerlySerializedAs("turnSpeed")]
        [SerializeField]
        [Min(0f)]
        private float turnSpeedDegrees = 180f;

        [Tooltip("Aiming: how fast the character turns towards the aim, in degrees per second.")]
        [SerializeField]
        [Min(0f)]
        private float aimTurnSpeedDegrees = 1080f;

        private TorusObject torusObject;
        public TorusObject TorusObject => torusObject;

        private PlayerAim aim;
        private Vector2 smoothedInput;
        private Vector2 smoothedScreenInput;
        private Vector3 moveDirection;

        /// <summary>
        /// Smoothed movement relative to the facing: x = right, y = forward/back (tank controls: x = turn).
        /// Read by the animator (run blend).
        /// </summary>
        public Vector2 SmoothedInput => smoothedInput;

        /// <summary>Wanted movement in world space, in the tangent plane of the player. Length 0 (standing) to 1 (full speed).</summary>
        public Vector3 MoveDirection => moveDirection;

        /// <summary>Multiplier of the move speed, set by combat (slower while attacking). 1 = normal.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Multiplier of the turn speed, set by combat. 1 = normal.</summary>
        public float TurnMultiplier { get; set; } = 1f;

        /// <summary>When true, input does not move the character (dodge, knockback, death).</summary>
        public bool IsLocked { get; set; }

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
            TryGetComponent(out aim);
        }

        private void OnEnable()
        {
            smoothedInput = Vector2.zero;
            smoothedScreenInput = Vector2.zero;
            moveDirection = Vector3.zero;
        }

        /// <summary>Unit direction of the movement, or the facing when the player is (almost) not moving. Used by the dodge and Blink.</summary>
        public Vector3 GetMoveDirectionOrFacing()
        {
            return moveDirection.sqrMagnitude > 0.01f ? moveDirection.normalized : torusObject.Facing;
        }

        private void Update()
        {
            if (isUsingTankControls)
            {
                UpdateTankControls();
                return;
            }

            UpdateAimedControls();
        }

        private void UpdateTankControls()
        {
            smoothedInput = Vector2.MoveTowards(smoothedInput, ReadInput(), inputResponse * Time.deltaTime);
            moveDirection = torusObject.Facing * smoothedInput.y;
            if (IsLocked)
                return;

            torusObject.Turn(smoothedInput.x * turnSpeedDegrees * TurnMultiplier * Mathf.Deg2Rad * Time.deltaTime);
            torusObject.Move(smoothedInput.y * moveSpeed * SpeedMultiplier * Time.deltaTime);
        }

        private void UpdateAimedControls()
        {
            smoothedScreenInput = Vector2.MoveTowards(smoothedScreenInput, ReadInput(), inputResponse * Time.deltaTime);
            GetScreenAxes(out var forward, out var right);
            moveDirection = right * smoothedScreenInput.x + forward * smoothedScreenInput.y;

            if (IsLocked == false)
                Steer();

            var facing = torusObject.Facing;
            var facingRight = Vector3.Cross(torusObject.Up, facing);
            smoothedInput = new Vector2(Vector3.Dot(moveDirection, facingRight), Vector3.Dot(moveDirection, facing));
        }

        private void Steer()
        {
            var turnTarget = GetTurnTarget();
            if (turnTarget.sqrMagnitude > 0.0001f)
                torusObject.TurnTowards(turnTarget, aimTurnSpeedDegrees * TurnMultiplier * Mathf.Deg2Rad * Time.deltaTime);

            float amount = moveDirection.magnitude;
            if (amount > 0.0001f)
                torusObject.MoveInDirection(moveDirection, amount * moveSpeed * SpeedMultiplier * Time.deltaTime);
        }

        private Vector3 GetTurnTarget()
        {
            if (aim && aim.HasAimDirection)
                return aim.AimDirection;

            // No aim from the gamepad stick (or no PlayerAim at all): face where the player runs.
            // An idle mouse cursor on the player keeps the facing.
            bool canFaceMovement = aim == null || aim.IsUsingStick;
            return canFaceMovement ? moveDirection : Vector3.zero;
        }

        private void GetScreenAxes(out Vector3 forward, out Vector3 right)
        {
            if (aim && aim.TryGetScreenAxes(out forward, out right))
                return;

            // No camera: up on the screen is the direction of running along the snake body.
            forward = torusObject.RunDirection;
            right = Vector3.Cross(torusObject.Up, forward);
        }

        private Vector2 ReadInput()
        {
            if (moveAction == null)
                return Vector2.zero;

            return Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        }
    }
}
