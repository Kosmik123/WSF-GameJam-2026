using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Cameras
{
    /// <summary>
    /// Camera of the player. Put it on the Cinemachine camera with <see cref="CinemachineOrbitalFollow"/>.
    /// The camera follows a separate rig that stands on the player but keeps the frame of the torus (up = the minor radius,
    /// forward = the direction of the snake body), so it does not turn with the character. The mouse turns the camera
    /// only while the right button is held (<see cref="TorusPlayerMovement.IsOrbiting"/>): the input of Cinemachine is off otherwise.
    /// The mouse wheel zooms (scale of the orbit).
    /// </summary>
    [RequireComponent(typeof(CinemachineOrbitalFollow))]
    [DefaultExecutionOrder(200)]
    public class PlayerCameraRig : MonoBehaviour
    {
        [Tooltip("The player. Empty = found in the scene.")]
        [SerializeField]
        private TorusObject target;

        [Tooltip("Input of the player. Empty = found on the target.")]
        [SerializeField]
        private TorusPlayerMovement movement;

        [Header("Zoom")]
        [Tooltip("Scale of the orbit at the closest and at the farthest zoom (1 = as set in the camera).")]
        [SerializeField]
        private Vector2 zoomRange = new Vector2(0.5f, 2f);

        [Tooltip("Change of the zoom for one notch of the mouse wheel.")]
        [SerializeField]
        [Min(0f)]
        private float zoomStep = 0.15f;

        [Tooltip("How fast the camera reaches the zoom, per second (higher = tighter).")]
        [SerializeField]
        [Min(0.1f)]
        private float zoomSharpness = 12f;

        private CinemachineOrbitalFollow orbit;
        private float targetZoom = 1f;
        private CinemachineCamera virtualCamera;
        private CinemachineInputAxisController inputController;
        private Transform rig;

        private void Awake()
        {
            virtualCamera = GetComponent<CinemachineCamera>();
            inputController = GetComponent<CinemachineInputAxisController>();
            orbit = GetComponent<CinemachineOrbitalFollow>();
            orbit.RadialAxis.Range = zoomRange;
            targetZoom = Mathf.Clamp(orbit.RadialAxis.Value, zoomRange.x, zoomRange.y);

            if (target == null)
                target = FindAnyObjectByType<TorusPlayerMovement>()?.TorusObject;

            if (movement == null && target)
                movement = target.GetComponent<TorusPlayerMovement>();

            rig = new GameObject("Player Camera Rig").transform;
            virtualCamera.Follow = rig;
            virtualCamera.LookAt = rig;
        }

        private void OnEnable()
        {
            MoveRig();
        }

        private void OnDestroy()
        {
            if (rig)
                Destroy(rig.gameObject);
        }

        private void Update()
        {
            MoveRig();
            if (inputController && movement)
                inputController.enabled = movement.IsOrbiting;

            UpdateZoom();
        }

        private void UpdateZoom()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                // One notch = one step, whatever the unit of the wheel on the platform is.
                float scroll = mouse.scroll.ReadValue().y;
                float notches = Mathf.Approximately(scroll, 0f) ? 0f : Mathf.Sign(scroll);
                targetZoom = Mathf.Clamp(targetZoom - notches * zoomStep, zoomRange.x, zoomRange.y);
            }

            orbit.RadialAxis.Value = Mathf.Lerp(orbit.RadialAxis.Value, targetZoom, 1f - Mathf.Exp(-zoomSharpness * Time.deltaTime));
        }

        private void MoveRig()
        {
            if (target == null || target.World == null)
                return;

            rig.SetPositionAndRotation(target.transform.position, Quaternion.LookRotation(target.RunDirection, target.Up));
        }
    }
}
