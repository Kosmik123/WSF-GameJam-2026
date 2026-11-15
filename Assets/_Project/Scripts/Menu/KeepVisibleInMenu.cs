using UnityEngine;

namespace WSFGameJam2026.Menu
{
    /// <summary>
    /// Marker for a root canvas that stays visible while <see cref="MainMenu"/> hides the HUD canvases (for example the leaderboard).
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class KeepVisibleInMenu : MonoBehaviour
    {
    }
}
