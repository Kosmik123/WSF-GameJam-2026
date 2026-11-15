using System.Collections;
using UnityEngine;

namespace WSFGameJam2026.Menus
{
    /// <summary>Fading of canvas groups in unscaled time, so it also works while the game is paused.</summary>
    public static class UiFade
    {
        public static IEnumerator FadingCo(CanvasGroup group, float targetAlpha, float seconds)
        {
            if (group == null)
                yield break;

            float startAlpha = group.alpha;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / seconds);
                yield return null;
            }

            group.alpha = targetAlpha;
        }
    }
}
