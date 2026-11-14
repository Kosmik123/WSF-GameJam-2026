using UnityEngine;
using UnityEngine.Serialization;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Moves the snake's head (the jaw) at a constant speed along the centre line of the torus.
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

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
        }

        private void OnEnable()
        {
            theta = startThetaDegrees * Mathf.Deg2Rad;
            Refresh();
        }

        private void Update()
        {
            theta = Mathf.Repeat(theta + angularMoveSpeedDegrees * Mathf.Deg2Rad * Time.deltaTime, TwoPi);
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
