using UnityEngine;
using UnityEngine.InputSystem;

namespace WSFGameJam2026.Controls
{
    /// <summary>
    /// Owner of an action map: enables it while this component is enabled.
    /// Other components only read or subscribe to the actions and never enable or disable them.
    /// </summary>
    public class InputActionMapEnabler : MonoBehaviour
    {
        [SerializeField]
        private InputActionAsset actions;

        [SerializeField]
        private string mapName = "Player";

        private void OnEnable()
        {
            if (actions == null)
                return;

            actions.FindActionMap(mapName, true).Enable();
        }

        private void OnDisable()
        {
            if (actions == null)
                return;

            actions.FindActionMap(mapName, true).Disable();
        }
    }
}
