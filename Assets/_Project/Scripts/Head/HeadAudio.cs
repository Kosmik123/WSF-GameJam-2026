using System.Collections;
using UnityEngine;
using WSFGameJam2026.Rounds;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Sound of the snake head. The loop fades in when a round starts after the run (re)started and keeps playing
    /// between rounds. When the player dies the loop fades out quickly and the death clip (the end of the sound) plays once.
    /// Both sources are 3D and sit on the head, so the sound comes from the jaw.
    /// </summary>
    public class HeadAudio : MonoBehaviour
    {
        [Tooltip("Looping source with the head sound. Its volume is the target of the fade in.")]
        [SerializeField]
        private AudioSource loopSource;

        [Tooltip("Not looping source with the end of the sound, played once at the death of the player.")]
        [SerializeField]
        private AudioSource deathSource;

        [SerializeField]
        private RoundController roundController;

        [Tooltip("Time in which the loop grows from silence to its volume at the start of a run.")]
        [SerializeField]
        [Min(0f)]
        private float fadeInSeconds = 2.5f;

        [Tooltip("Time in which the loop goes silent at the death, while the death clip starts.")]
        [SerializeField]
        [Min(0f)]
        private float deathFadeOutSeconds = 0.2f;

        private float loopVolume;
        private Coroutine fade;

        [Header("States")]
        [SerializeField]
        private bool isLoopActive;

        private void Reset()
        {
            roundController = FindAnyObjectByType<RoundController>();
        }

        private void Awake()
        {
            loopVolume = loopSource.volume;
            loopSource.loop = true;
            loopSource.playOnAwake = false;
            deathSource.loop = false;
            deathSource.playOnAwake = false;
        }

        private void OnEnable()
        {
            if (roundController == null)
                return;

            roundController.OnRoundStarted += RoundController_OnRoundStarted;
            roundController.OnRunEnded += RoundController_OnRunEnded;
        }

        private void Start()
        {
            // Without the round loop (a test scene) the sound just starts.
            if (roundController == null)
                StartLoop();
        }

        private void RoundController_OnRoundStarted(int round)
        {
            StartLoop();
        }

        private void StartLoop()
        {
            if (isLoopActive)
                return;

            isLoopActive = true;
            deathSource.Stop();
            loopSource.volume = 0f;
            loopSource.Play();
            StartFade(loopVolume, fadeInSeconds, false);
        }

        private void RoundController_OnRunEnded(int completedRounds, int bestRounds, bool isNewBest)
        {
            isLoopActive = false;
            deathSource.Play();
            StartFade(0f, deathFadeOutSeconds, true);
        }

        private void StartFade(float targetVolume, float duration, bool stopAtEnd)
        {
            if (fade != null)
                StopCoroutine(fade);

            fade = StartCoroutine(LoopFadeCo(targetVolume, duration, stopAtEnd));
        }

        private IEnumerator LoopFadeCo(float targetVolume, float duration, bool stopAtEnd)
        {
            float startVolume = loopSource.volume;
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                // Smoothstep, so the sound eases in and out instead of changing at a constant rate.
                float progress = Mathf.SmoothStep(0f, 1f, time / duration);
                loopSource.volume = Mathf.Lerp(startVolume, targetVolume, progress);
                yield return null;
            }

            loopSource.volume = targetVolume;
            if (stopAtEnd)
                loopSource.Stop();

            fade = null;
        }

        private void OnDisable()
        {
            if (roundController == null)
                return;

            roundController.OnRoundStarted -= RoundController_OnRoundStarted;
            roundController.OnRunEnded -= RoundController_OnRunEnded;
        }
    }
}
