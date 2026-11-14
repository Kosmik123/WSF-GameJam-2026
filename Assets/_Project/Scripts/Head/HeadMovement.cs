using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Moves the snake's head (the jaw) at a constant speed along the centre line of the torus.
    /// Its local forward is always tangent to the torus, in the direction of growing theta.
    /// </summary>
    public class HeadMovement : MonoBehaviour
    {
        private const float TwoPi = Mathf.PI * 2f;

        [SerializeField]
        private TorusWorld world;

        [Tooltip("Speed along the snake body in degrees per second. Negative = opposite direction.")]
        [SerializeField]
        private float angularMoveSpeed = 5f;

        [Tooltip("Start position along the snake body, in degrees (0-360).")]
        [SerializeField]
        private float startTheta;

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
            theta = startTheta * Mathf.Deg2Rad;
            Refresh();
        }

        private void Update()
        {
            theta = Mathf.Repeat(theta + angularMoveSpeed * Mathf.Deg2Rad * Time.deltaTime, TwoPi);
            Refresh();
        }

        /// <summary>Places the head on the centre line of the torus for the current theta.</summary>
        public void Refresh()
        {
            if (world == null)
                return;

            float cos = Mathf.Cos(theta);
            float sin = Mathf.Sin(theta);

            var localPosition = new Vector3(world.MajorRadius * cos, 0f, world.MajorRadius * sin);
            var localTangent = new Vector3(-sin, 0f, cos);

            var worldTransform = world.transform;
            transform.SetPositionAndRotation(
                worldTransform.TransformPoint(localPosition),
                Quaternion.LookRotation(worldTransform.TransformDirection(localTangent), worldTransform.up));
        }
    }
}
