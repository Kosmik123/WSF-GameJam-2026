using UnityEngine;
using UnityEngine.Serialization;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Moves the snake's head (the jaw) along the centre line of the torus, at a constant speed. When the player comes close from behind,
    /// the head speeds up and runs away: the speed grows linearly with the distance, from the base speed at the distance where
    /// the speedup starts to the maximum speed at the distance where it is reached.
    /// Its local forward is always tangent to the torus, in the direction of growing theta.
    /// </summary>
    public class HeadMovement : MonoBehaviour
    {
        private const float TwoPi = TorusGeometry.TwoPi;

        [SerializeField]
        private TorusWorld world;

        [Tooltip("Speed along the snake body in degrees per second. Negative = opposite direction.")]
        [FormerlySerializedAs("angularMoveSpeed")]
        [SerializeField]
        private float angularMoveSpeedDegrees = 5f;

        [Header("Speedup when the player is close")]
        [Tooltip("The player behind the head: the speed of the head when the player is as close as 'Max Speed Theta' or closer, degrees per second. Not more than the base speed = no speedup.")]
        [SerializeField]
        [Min(0f)]
        private float maxSpeedDegrees = 12f;

        [Tooltip("Distance behind the head (degrees of theta) at which the head reaches the maximum speed.")]
        [SerializeField]
        [Min(0f)]
        private float maxSpeedThetaDegrees = 15f;

        [Tooltip("Distance behind the head (degrees of theta) from which the head starts to speed up. Further than this the speed is the base one.")]
        [SerializeField]
        [Min(0f)]
        private float speedupStartThetaDegrees = 60f;

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
            if (IsMoving == false)
                return;

            theta = Mathf.Repeat(theta + CurrentSpeedDegrees * SpeedMultiplier * Mathf.Deg2Rad * Time.deltaTime, TwoPi);
            Refresh();
        }

        /// <summary>Speed along the body now, in degrees per second, before the round multiplier: the base speed, or more when the player is close behind.</summary>
        public float CurrentSpeedDegrees => player ? GetSpeedDegrees(GetDistanceBehindDegrees(player.Angles.Theta)) : angularMoveSpeedDegrees;

        /// <summary>
        /// Speed of the head (degrees per second, before the round multiplier) when the player is the given distance behind it.
        /// The base speed from <see cref="speedupStartThetaDegrees"/> and further, the maximum speed from <see cref="maxSpeedThetaDegrees"/>
        /// and closer, a straight line between them.
        /// </summary>
        public float GetSpeedDegrees(float distanceBehindDegrees)
        {
            float closest = maxSpeedThetaDegrees;
            float farthest = Mathf.Max(speedupStartThetaDegrees, closest + 0.01f);
            float closeness = Mathf.InverseLerp(farthest, closest, distanceBehindDegrees);
            return Mathf.Lerp(angularMoveSpeedDegrees, maxSpeedDegrees, closeness);
        }

        // How far behind the head the given theta (radians) is, in degrees [0, 360). A player ahead of the head is far behind it.
        private float GetDistanceBehindDegrees(float playerTheta)
        {
            return Mathf.Repeat(theta - playerTheta, TwoPi) * Mathf.Rad2Deg;
        }

        // Rings around the tube: yellow where the head starts to speed up, red where it reaches the maximum speed (both behind the head), white at the head.
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
            DrawRing(gizmoWorld, headTheta - speedupStartThetaDegrees * Mathf.Deg2Rad);

            Gizmos.color = Color.red;
            DrawRing(gizmoWorld, headTheta - maxSpeedThetaDegrees * Mathf.Deg2Rad);

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
