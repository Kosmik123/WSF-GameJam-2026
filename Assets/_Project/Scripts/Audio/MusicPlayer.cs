using System.Collections;
using UnityEngine;

namespace WSFGameJam2026.Audio
{
    /// <summary>
    /// Looping background music (the ambient), the same in the menu and in the game. It fades in when it starts.
    /// The music source lives outside the scene (DontDestroyOnLoad), so a scene reload (a new run) does not restart
    /// the track: the player of the reloaded scene finds it already playing and leaves it. The music is 2D and follows
    /// the master volume of the settings (AudioListener.volume). Put on the Audio object.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        [SerializeField]
        private AudioClip music;

        [SerializeField]
        [Range(0f, 1f)]
        private float volume = 0.5f;

        [Tooltip("Time in which the music grows from silence to its volume when it starts.")]
        [SerializeField]
        [Min(0f)]
        private float fadeInSeconds = 3f;

        private static AudioSource source;
        private Coroutine fade;

        private bool IsPlayingMusic => source && source.clip == music && source.isPlaying;

        private void Start()
        {
            if (music == null)
                return;

            if (IsPlayingMusic == false)
                StartMusic();

            // Also finishes a fade in cut short by a scene reload.
            fade = StartCoroutine(MusicFadeCo(volume));
        }

        private void StartMusic()
        {
            if (source == null)
                source = CreateSource();

            source.clip = music;
            source.volume = 0f;
            source.Play();
        }

        private static AudioSource CreateSource()
        {
            var musicObject = new GameObject("Music");
            DontDestroyOnLoad(musicObject);
            var musicSource = musicObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
            musicSource.priority = 0;
            musicSource.bypassReverbZones = true;
            musicSource.ignoreListenerPause = true;
            return musicSource;
        }

        private IEnumerator MusicFadeCo(float targetVolume)
        {
            float startVolume = source.volume;
            for (float time = 0f; time < fadeInSeconds; time += Time.unscaledDeltaTime)
            {
                // Smoothstep, so the music eases in instead of growing at a constant rate.
                float progress = Mathf.SmoothStep(0f, 1f, time / fadeInSeconds);
                source.volume = Mathf.Lerp(startVolume, targetVolume, progress);
                yield return null;
            }

            source.volume = targetVolume;
            fade = null;
        }

        private void OnValidate()
        {
            // Lets the volume be tuned in the Inspector during play.
            if (Application.isPlaying == false || source == null || fade != null || gameObject.scene.isLoaded == false)
                return;

            source.volume = volume;
        }
    }
}
