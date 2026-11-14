using UnityEngine;

namespace WSFGameJam2026.Audio
{
    /// <summary>
    /// One sound of the game: recorded clip variants or a synthesized recipe, with its volume, pitch jitter and spatial blend.
    /// Played by <see cref="SfxPlayer"/> (through <see cref="Sfx"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/Sound Effect", fileName = "SoundEffect")]
    public class SoundEffect : ScriptableObject
    {
        [Tooltip("Recorded variants, one picked at random per play. When empty, the recipe below is synthesized.")]
        [SerializeField]
        private AudioClip[] clips = new AudioClip[0];

        [Tooltip("Synthesized sound used when there are no clips (built once by the SfxPlayer).")]
        [SerializeField]
        private SfxRecipe recipe = SfxRecipe.None;
        public SfxRecipe Recipe => recipe;

        [SerializeField]
        [Range(0f, 1f)]
        private float volume = 1f;
        public float Volume => volume;

        [Tooltip("Random pitch change per play, +/- this fraction.")]
        [SerializeField]
        [Range(0f, 0.5f)]
        private float pitchJitter = 0.08f;
        public float PitchJitter => pitchJitter;

        [Tooltip("0 = 2D (UI, stingers), 1 = fully 3D at the play position.")]
        [SerializeField]
        [Range(0f, 1f)]
        private float spatialBlend = 0.8f;
        public float SpatialBlend => spatialBlend;

        [Tooltip("Plays closer together than this are skipped (many hits in one frame).")]
        [SerializeField]
        [Min(0f)]
        private float minInterval = 0.03f;
        public float MinInterval => minInterval;

        public bool HasClips
        {
            get
            {
                for (int i = 0; i < clips.Length; i++)
                    if (clips[i])
                        return true;

                return false;
            }
        }

        /// <summary>A random recorded variant, or null when there are none.</summary>
        public AudioClip PickClip(System.Random random)
        {
            if (HasClips == false)
                return null;

            // Empty slots are skipped by trying from a random start.
            int start = random.Next(clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[(start + i) % clips.Length];
                if (clip)
                    return clip;
            }

            return null;
        }
    }
}
