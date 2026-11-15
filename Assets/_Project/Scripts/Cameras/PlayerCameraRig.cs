using Unity.Cinemachine;
using UnityEngine;
using WSFGameJam2026.Player;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Cameras
{
    /// <summary>
    /// Camera of the player. Put it on the Cinemachine camera with <see cref="CinemachineOrbitalFollow"/>.
    /// The camera follows a separate rig that stands on the player but keeps the frame of the torus (up = the minor radius,
    /// forward = the direction of the snake body), so it does not turn with the character. The mouse turns the camera
    /// only while the right button is held (<see cref="TorusPlayerMovement.IsOrbiting"/>): the "Look" inputs of Cinemachine are off otherwise.
    /// The other inputs (the zoom with the mouse wheel, "Orbit Scale") always work.
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

        private CinemachineCamera virtualCamera;
        private CinemachineInputAxisController inputController;
        private Transform rig;

        private void Awake()
        {
            virtualCamera = GetComponent<CinemachineCamera>();
            inputController = GetComponent<CinemachineInputAxisController>();

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
                SetLookInput(movement.IsOrbiting);
        }

        // Only the inputs that turn the camera; the zoom stays on.
        private void SetLookInput(bool isEnabled)
        {
            var controllers = inputController.Controllers;
            for (int i = 0; i < controllers.Count; i++)
            {
                if (controllers[i].Name.StartsWith("Look"))
                    controllers[i].Enabled = isEnabled;
            }
        }

        private void MoveRig()
        {
            if (target == null || target.World == null)
                return;

            rig.SetPositionAndRotation(target.transform.position, Quaternion.LookRotation(target.RunDirection, target.Up));
        }
    }
}
