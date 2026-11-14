using UnityEngine;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Cameras
{
    /// <summary>
    /// Follows the player from above at an angle, behind him along the snake body.
    /// The camera's up is the normal of the smooth torus, so the world turns around the player
    /// as he runs around the tube.
    /// </summary>
    public class TorusFollowCamera : MonoBehaviour
    {
        [SerializeField]
        private TorusPlayerMovement target;

        [Header("Placement")]
        [SerializeField]
        [Min(1f)]
        private float distance = 14f;

        [Tooltip("Angle above the surface. 90 = straight from above, 0 = from behind.")]
        [SerializeField]
        [Range(10f, 89f)]
        private float pitch = 55f;

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
            target = FindFirstObjectByType<TorusPlayerMovement>();
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
            Vector3 up = target.BaseNormal;
            Vector3 forward = Vector3.ProjectOnPlane(target.RunDirection, up).normalized;
            Vector3 focus = target.transform.position + up * lookAtHeight;

            float pitchRadians = pitch * Mathf.Deg2Rad;
            Vector3 desiredPosition = focus + up * (distance * Mathf.Sin(pitchRadians)) - forward * (distance * Mathf.Cos(pitchRadians));

            if (shouldSnap)
            {
                transform.SetPositionAndRotation(desiredPosition, Quaternion.LookRotation(focus - desiredPosition, up));
                velocity = Vector3.zero;
                shouldSnap = false;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, positionSmoothTime);

            Quaternion desiredRotation = Quaternion.LookRotation(focus - transform.position, up);
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
        }
    }
}
