using System.Collections;
using UnityEngine;

namespace WSFGameJam2026.Combat
{
    /// <summary>
    /// Short freeze of the game on a hit (port from LD57). One component in the scene, used through <see cref="Freeze"/>.
    /// The freeze sets timeScale to 0.0001 (not 0), so it can be told apart from the pause (exactly 0).
    /// A pause set during the freeze is never undone.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        private const float FrozenScale = 0.0001f;

        private static HitStop instance;

        private Coroutine running;

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnDisable()
        {
            // Disabling during the freeze must not leave the time stopped.
            if (running != null && Mathf.Approximately(Time.timeScale, FrozenScale))
                Time.timeScale = 1f;

            running = null;
        }

        /// <summary>Freezes the game for the given real time seconds. Ignored during a pause and during another freeze.</summary>
        public static void Freeze(float seconds)
        {
            if (instance != null)
                instance.StartFreeze(seconds);
        }

        private void StartFreeze(float seconds)
        {
            if (running != null || Time.timeScale <= 0f || isActiveAndEnabled == false || seconds <= 0f)
                return;

            running = StartCoroutine(FreezeRoutine(seconds));
        }

        private IEnumerator FreezeRoutine(float seconds)
        {
            float previousScale = Time.timeScale;
            Time.timeScale = FrozenScale;
            yield return new WaitForSecondsRealtime(seconds);

            if (Mathf.Approximately(Time.timeScale, FrozenScale))
                Time.timeScale = previousScale;

            running = null;
        }
    }
}
