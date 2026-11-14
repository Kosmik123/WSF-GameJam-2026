using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.Audio
{
    /// <summary>
    /// Plays <see cref="SoundEffect"/>s from a pool of AudioSources (port of the LD57 ProceduralSfxBank).
    /// Synthesizes every <see cref="SfxRecipe"/> once in Awake. One per scene, reached through <see cref="Sfx"/>.
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        [SerializeField]
        [Range(0f, 1f)]
        private float masterVolume = 0.6f;

        [Tooltip("Reverb share of the synthesized mix.")]
        [SerializeField]
        [Range(0f, 0.6f)]
        private float reverbMix = 0.2f;

        [Tooltip("tanh saturation of the synthesized impacts; tones get about 2/3 of it.")]
        [SerializeField]
        [Range(0.5f, 4f)]
        private float drive = 1.8f;

        [SerializeField]
        [Range(1, 32)]
        private int poolSize = 10;

        [Header("3D")]
        [SerializeField]
        [Min(0f)]
        private float minDistance = 4f;

        [SerializeField]
        [Min(0f)]
        private float maxDistance = 40f;

        private readonly Dictionary<SfxRecipe, AudioClip> synthesizedClips = new Dictionary<SfxRecipe, AudioClip>();
        private readonly Dictionary<SoundEffect, float> lastPlayTimes = new Dictionary<SoundEffect, float>();
        private readonly System.Random random = new System.Random(12345);
        private AudioSource[] sources;
        private int nextSource;

        public static SfxPlayer Instance { get; private set; }

        public float MasterVolume
        {
            get => masterVolume;
            set => masterVolume = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            Instance = this;
            BuildClips();
            CreateSources();
        }

        /// <summary>Plays the sound at the position (3D by its spatial blend). The volume multiplies the sound's own.</summary>
        public void Play(SoundEffect sound, Vector3 position, float volume = 1f)
        {
            if (sound == null || sources == null)
                return;

            float now = Time.unscaledTime;
            if (lastPlayTimes.TryGetValue(sound, out float lastTime) && now - lastTime < sound.MinInterval)
                return;

            var clip = sound.HasClips ? sound.PickClip(random) : GetSynthesizedClip(sound.Recipe);
            if (clip == null)
                return;

            lastPlayTimes[sound] = now;
            var source = TakeSource();
            source.transform.position = position;
            source.spatialBlend = sound.SpatialBlend;
            source.pitch = 1f + ((float)random.NextDouble() * 2f - 1f) * sound.PitchJitter;
            source.Stop();
            source.PlayOneShot(clip, Mathf.Clamp01(volume) * masterVolume * sound.Volume);
        }

        public AudioClip GetSynthesizedClip(SfxRecipe recipe)
        {
            return synthesizedClips.TryGetValue(recipe, out var clip) ? clip : null;
        }

        private void BuildClips()
        {
            var synthesizer = new SfxSynthesizer(reverbMix, drive);
            foreach (SfxRecipe recipe in System.Enum.GetValues(typeof(SfxRecipe)))
            {
                var clip = synthesizer.Build(recipe);
                if (clip)
                    synthesizedClips[recipe] = clip;
            }
        }

        private void CreateSources()
        {
            sources = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var sourceObject = new GameObject("Sfx Source " + i);
                sourceObject.transform.SetParent(transform, false);
                var source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.ignoreListenerPause = true;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                sources[i] = source;
            }
        }

        /// <summary>The next free source, or the oldest one when all are playing.</summary>
        private AudioSource TakeSource()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                int index = (nextSource + i) % sources.Length;
                if (sources[index].isPlaying)
                    continue;

                nextSource = (index + 1) % sources.Length;
                return sources[index];
            }

            var oldest = sources[nextSource];
            nextSource = (nextSource + 1) % sources.Length;
            return oldest;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            foreach (var clip in synthesizedClips.Values)
                if (clip)
                    Destroy(clip);
        }
    }
}
