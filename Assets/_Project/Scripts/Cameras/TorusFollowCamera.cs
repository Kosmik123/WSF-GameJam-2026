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

        [Header("Shake")]
        [Tooltip("Frequency of the shake noise.")]
        [SerializeField]
        [Min(0f)]
        private float shakeFrequency = 25f;

        private Vector3 velocity;
        private bool shouldSnap = true;

        // Position without shake. Smoothing works on it, so shaking never drags the camera.
        private Vector3 smoothedPosition;
        private float shakeAmplitude;
        private float shakeDuration;
        private float shakeTimeLeft;

        private void Reset()
        {
            target = FindAnyObjectByType<TorusObject>();
        }

        private void OnEnable()
        {
            shouldSnap = true;
        }

        /// <summary>Shakes the camera. A stronger shake replaces a weaker one. Runs in real time, so it also works during the hit stop.</summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude < shakeAmplitude * (shakeTimeLeft / Mathf.Max(0.0001f, shakeDuration)))
                return;

            shakeAmplitude = amplitude;
            shakeDuration = Mathf.Max(0.01f, duration);
            shakeTimeLeft = shakeDuration;
        }

        private Vector3 GetShakeOffset()
        {
            if (shakeTimeLeft <= 0f)
                return Vector3.zero;

            shakeTimeLeft -= Time.unscaledDeltaTime;
            float strength = shakeAmplitude * Mathf.Clamp01(shakeTimeLeft / shakeDuration);
            float time = Time.unscaledTime * shakeFrequency;
            var noise = new Vector3(
                Mathf.PerlinNoise(time, 0.1f) - 0.5f,
                Mathf.PerlinNoise(0.3f, time) - 0.5f,
                Mathf.PerlinNoise(time, 0.7f) - 0.5f);
            return 2f * strength * noise;
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
                smoothedPosition = desiredPosition;
                transform.SetPositionAndRotation(desiredPosition, Quaternion.LookRotation(focus - desiredPosition, up));
                velocity = Vector3.zero;
                shouldSnap = false;
                return;
            }

            smoothedPosition = Vector3.SmoothDamp(smoothedPosition, desiredPosition, ref velocity, positionSmoothTime);

            var desiredRotation = Quaternion.LookRotation(focus - smoothedPosition, up);
            float blend = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(smoothedPosition + GetShakeOffset(), Quaternion.Slerp(transform.rotation, desiredRotation, blend));
        }
    }
}
