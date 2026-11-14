using UnityEngine;
using UnityEngine.Serialization;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Cameras
{
    /// <summary>
    /// Follows the player from above at an angle, behind him (opposite to the direction he faces).
    /// The camera's up is the normal of the smooth torus, so the world turns around the player
    /// as he runs around the tube.
    /// </summary>
    public class TorusFollowCamera : MonoBehaviour
    {
        [SerializeField]
        private TorusObject target;

        [Header("Placement")]
        [SerializeField]
        [Min(1f)]
        private float distance = 14f;

        [Tooltip("Angle above the surface. 90 = straight from above, 0 = from behind.")]
        [FormerlySerializedAs("pitch")]
        [SerializeField]
        [Range(10f, 89f)]
        private float pitchDegrees = 55f;

        [Tooltip("Height of the point the camera looks at, above the player's pivot.")]
        [SerializeField]
        private float lookAtHeight = 1f;

        [Header("Smoothing")]
        [SerializeField]
        [Min(0f)]
        private float positionSmoothTime = 0.12f;

        [SerializeField]
        [Min(0f)]
        private float rotationSharpness = 8f;

        private Vector3 velocity;
        private bool shouldSnap = true;

        private void Reset()
        {
            target = FindAnyObjectByType<TorusObject>();
        }

        private void OnEnable()
        {
            shouldSnap = true;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            // The smooth base tube is used instead of the terrain, so bumps don't shake the camera.
            var up = target.Up;
            var forward = Vector3.ProjectOnPlane(target.Facing, up).normalized;
            var focus = target.transform.position + up * lookAtHeight;

            float pitchRadians = pitchDegrees * Mathf.Deg2Rad;
            var desiredPosition = focus + up * (distance * Mathf.Sin(pitchRadians)) - forward * (distance * Mathf.Cos(pitchRadians));

            if (shouldSnap)
            {
                transform.SetPositionAndRotation(desiredPosition, Quaternion.LookRotation(focus - desiredPosition, up));
                velocity = Vector3.zero;
                shouldSnap = false;
                return;
            }

            var newPosition = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

            var desiredRotation = Quaternion.LookRotation(focus - newPosition, up);
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(newPosition, Quaternion.Slerp(transform.rotation, desiredRotation, blend));
        }
    }
}
