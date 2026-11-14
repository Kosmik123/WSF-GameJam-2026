using UnityEngine;

namespace WSFGameJam2026.Menus
{
    /// <summary>
    /// Turns a UI element around its centre in unscaled time, so it also spins while the game is paused.
    /// Used by the ouroboros of the loading screen: it turns counter-clockwise, so the head chases its tail.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UiSpinner : MonoBehaviour
    {
        [Tooltip("Degrees per second. Positive = counter-clockwise.")]
        [SerializeField]
        private float degreesPerSecond = 120f;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
