using System;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Cameras
{
    /// <summary>
    /// The camera of the main menu: first it looks at the whole torus from above and slowly turns around it,
    /// then it flies down behind the back of the player and hands over to <see cref="TorusFollowCamera"/>.
    /// Runs in unscaled time, because the game is paused while the menu is open.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(TorusFollowCamera))]
    public class CameraIntro : MonoBehaviour
    {
        /// <summary>The camera reached the follow view and the follow camera took over.</summary>
        public event Action OnDescentFinished;

        [SerializeField]
        private TorusWorld world;

        [Header("Overview")]
        [Tooltip("Height above the centre of the torus, in multiples of its outer radius.")]
        [SerializeField]
        [Min(0.5f)]
        private float overviewHeight = 2.2f;

        [Tooltip("Slow turn of the view from above, in degrees per second.")]
        [SerializeField]
        private float overviewSpinDegrees = 4f;

        [Header("Descent")]
        [SerializeField]
        [Min(0.1f)]
        private float descentSeconds = 3f;

        [Tooltip("Progress of the flight over time (0-1 on both axes).")]
        [SerializeField]
        private AnimationCurve descentCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private TorusFollowCamera followCamera;
        private Vector3 descentStartPosition;
        private Quaternion descentStartRotation;

        [Header("States")]
        [SerializeField]
        private bool isOverview;

        [SerializeField]
        private bool isDescending;

        [SerializeField]
        private float descentElapsed;

        [SerializeField]
        private float spinDegrees;

        /// <summary>True while the camera shows the torus from above or flies down to the player.</summary>
        public bool IsPlaying => isOverview || isDescending;

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
        }

        private void Awake()
        {
            followCamera = GetComponent<TorusFollowCamera>();
        }

        /// <summary>Shows the whole torus from above. The follow camera is off until <see cref="PlayDescent"/> ends.</summary>
        public void ShowOverview()
        {
            followCamera.enabled = false;
            isDescending = false;
            isOverview = true;
            ApplyOverview();
        }

        /// <summary>Flies from the current view down behind the back of the player.</summary>
        public void PlayDescent()
        {
            followCamera.enabled = false;
            descentStartPosition = transform.position;
            descentStartRotation = transform.rotation;
            descentElapsed = 0f;
            isOverview = false;
            isDescending = true;
        }

        private void LateUpdate()
        {
            if (isOverview)
            {
                spinDegrees += overviewSpinDegrees * Time.unscaledDeltaTime;
                ApplyOverview();
                return;
            }

            if (isDescending == false)
                return;

            descentElapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(descentElapsed / descentSeconds);
            float eased = descentCurve.Evaluate(progress);

            followCamera.GetTargetPose(out var targetPosition, out var targetRotation);
            transform.SetPositionAndRotation(
                Vector3.Lerp(descentStartPosition, targetPosition, eased),
                Quaternion.Slerp(descentStartRotation, targetRotation, eased));

            if (progress < 1f)
                return;

            isDescending = false;
            followCamera.enabled = true;
            OnDescentFinished?.Invoke();
        }

        private void ApplyOverview()
        {
            if (world == null)
                return;

            var worldTransform = world.transform;
            float outerRadius = (world.MajorRadius + world.MinorRadius) * worldTransform.lossyScale.x;
            var position = worldTransform.position + worldTransform.up * (outerRadius * overviewHeight);
            var screenUp = Quaternion.AngleAxis(spinDegrees, worldTransform.up) * worldTransform.forward;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(-worldTransform.up, screenUp));
        }
    }
}
