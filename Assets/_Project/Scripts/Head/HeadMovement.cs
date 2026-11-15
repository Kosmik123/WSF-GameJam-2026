using UnityEngine;
using UnityEngine.Serialization;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Moves the snake's head (the jaw) along the centre line of the torus. The player runs away from it, so the speed depends on how far
    /// ahead of the head the player is: the closer the player (the easier the head can eat him), the slower the head, down to the minimum speed
    /// at the threshold distance and closer. Beyond the threshold the speed grows linearly with the distance, without a limit.
    /// Its local forward is always tangent to the torus, in the direction of growing theta.
    /// </summary>
    public class HeadMovement : MonoBehaviour
    {
        private const float TwoPi = TorusGeometry.TwoPi;

        [SerializeField]
        private TorusWorld world;

        [Header("Speed by the distance of the player (ahead of the head)")]
        [Tooltip("The lowest speed of the head, at the threshold distance and closer, degrees per second. Negative = opposite direction.")]
        [FormerlySerializedAs("angularMoveSpeed")]
        [SerializeField]
        private float angularMoveSpeedDegrees = 5f;

        [Tooltip("Distance ahead of the head (degrees of theta) up to which the head moves at the lowest speed.")]
        [FormerlySerializedAs("maxSpeedThetaDegrees")]
        [SerializeField]
        [Min(0f)]
        private float closeThetaDegrees = 15f;

        [Tooltip("How much the speed grows (degrees per second) with every degree of theta of the distance beyond the threshold. No upper limit.")]
        [SerializeField]
        [Min(0f)]
        private float speedGainPerDegree = 0.15f;

        [Tooltip("The player whose distance counts. Empty = found automatically.")]
        [SerializeField]
        private TorusObject player;

        [Tooltip("Start position along the snake body, in degrees (0-360).")]
        [FormerlySerializedAs("startTheta")]
        [SerializeField]
        private float startThetaDegrees;

        // Runtime angles are always in radians. Only Inspector fields (suffix Degrees) use degrees.
        [Header("States")]
        [SerializeField]
        private float theta;

        [Tooltip("Shown for balancing, set every frame (do not edit): the speed of the head now with the round multiplier, in degrees per second. 0 when it is stopped.")]
        [SerializeField]
        private float currentSpeedDegrees;

        /// <summary>Angle along the snake body, in radians.</summary>
        public float Theta => theta;

        public TorusWorld World => world;

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
            player = FindPlayer();
        }

        private void Awake()
        {
            if (player == null)
                player = FindPlayer();
        }

        private void OnEnable()
        {
            theta = startThetaDegrees * Mathf.Deg2Rad;
            Refresh();
        }

        private void Update()
        {
            currentSpeedDegrees = IsMoving ? CurrentSpeedDegrees * SpeedMultiplier : 0f;
            if (IsMoving == false)
                return;

            theta = Mathf.Repeat(theta + currentSpeedDegrees * Mathf.Deg2Rad * Time.deltaTime, TwoPi);
            Refresh();
        }

        /// <summary>Speed along the body now, in degrees per second, before the round multiplier: depends on how far ahead of the head the player is.</summary>
        public float CurrentSpeedDegrees => player ? GetSpeedDegrees(GetDistanceAheadDegrees(player.Angles.Theta)) : angularMoveSpeedDegrees;

        /// <summary>
        /// Speed of the head (degrees per second, before the round multiplier) when the player is the given distance ahead of it:
        /// the lowest speed up to <see cref="closeThetaDegrees"/>, then growing linearly with the distance, with no limit.
        /// </summary>
        public float GetSpeedDegrees(float distanceAheadDegrees)
        {
            return angularMoveSpeedDegrees + speedGainPerDegree * Mathf.Max(0f, distanceAheadDegrees - closeThetaDegrees);
        }

        /// <summary>How far ahead of the head the given theta (radians) is, in degrees [0, 180]. A player behind the head counts as at the head (0).</summary>
        public float GetDistanceAheadDegrees(float otherTheta)
        {
            float degrees = Mathf.Repeat(otherTheta - theta, TwoPi) * Mathf.Rad2Deg;
            return degrees <= 180f ? degrees : 0f;
        }

        // Rings around the tube: white at the head, yellow at the threshold distance ahead of it (closer than this the head is at its lowest speed).
        private void OnDrawGizmosSelected()
        {
            var gizmoWorld = world ? world : FindAnyObjectByType<TorusWorld>();
            if (gizmoWorld == null)
                return;

            // In edit mode the head is read from where it stands (theta 0 is on +Z, growing towards +X).
            float headTheta = theta;
            if (Application.isPlaying == false)
            {
                var localPosition = gizmoWorld.transform.InverseTransformPoint(transform.position);
                headTheta = Mathf.Atan2(localPosition.x, localPosition.z);
            }

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = gizmoWorld.transform.localToWorldMatrix;

            Gizmos.color = Color.white;
            DrawRing(gizmoWorld, headTheta);

            Gizmos.color = Color.yellow;
            DrawRing(gizmoWorld, headTheta + closeThetaDegrees * Mathf.Deg2Rad);

            Gizmos.matrix = previousMatrix;
        }

        private static void DrawRing(TorusWorld targetWorld, float ringTheta)
        {
            const int segments = 48;
            var previous = TorusGeometry.GetPoint(targetWorld.MajorRadius, targetWorld.MinorRadius, ringTheta, (segments - 1) * TwoPi / segments);
            for (int i = 0; i < segments; i++)
            {
                var current = TorusGeometry.GetPoint(targetWorld.MajorRadius, targetWorld.MinorRadius, ringTheta, i * TwoPi / segments);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

        private static TorusObject FindPlayer()
        {
            var movement = FindAnyObjectByType<TorusPlayerMovement>();
            return movement ? movement.GetComponent<TorusObject>() : null;
        }

        /// <summary>Multiplies the speed of the head. Set by the round loop: every round the jaw is faster.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>False stops the head (between rounds, after the death of the player).</summary>
        public bool IsMoving { get; set; } = true;

        /// <summary>Puts the head back at its start position.</summary>
        public void ResetPosition()
        {
            theta = startThetaDegrees * Mathf.Deg2Rad;
            Refresh();
        }

        /// <summary>Places the head on the centre line of the torus for the current theta.</summary>
        public void Refresh()
        {
            if (world == null)
                return;

            var localPosition = TorusGeometry.GetCenterPoint(world.MajorRadius, theta);
            var localTangent = TorusGeometry.GetThetaDirection(theta);

            var worldTransform = world.transform;
            transform.SetPositionAndRotation(
                worldTransform.TransformPoint(localPosition),
                Quaternion.LookRotation(worldTransform.TransformDirection(localTangent), worldTransform.up));
        }
    }
}
