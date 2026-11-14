using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Player
{
    /// <summary>
    /// Player input for a <see cref="TorusObject"/>. Tank-like controls: left/right turns the character,
    /// up/down moves it forward/back along the direction it faces.
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

        [Tooltip("Turning speed of the character (left/right) in degrees per second.")]
        [FormerlySerializedAs("turnSpeed")]
        [SerializeField]
        [Min(0f)]
        private float turnSpeedDegrees = 180f;

        private TorusObject torusObject;
        public TorusObject TorusObject => torusObject;

        private Vector2 smoothedInput;

        /// <summary>Smoothed input: x = turn, y = forward/back. Read by combat (dodge direction).</summary>
        public Vector2 SmoothedInput => smoothedInput;

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
        }

        private void Update()
        {
            smoothedInput = Vector2.MoveTowards(smoothedInput, ReadInput(), inputResponse * Time.deltaTime);
            if (IsLocked)
                return;

            torusObject.Turn(smoothedInput.x * turnSpeedDegrees * TurnMultiplier * Mathf.Deg2Rad * Time.deltaTime);
            torusObject.Move(smoothedInput.y * moveSpeed * SpeedMultiplier * Time.deltaTime);
        }

        private Vector2 ReadInput()
        {
            if (moveAction == null)
                return Vector2.zero;

            return Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        }
    }
}
