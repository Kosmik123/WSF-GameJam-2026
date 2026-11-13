using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>Describes mesh resolution and terrain shape of a single snake body segment.</summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Torus Segment Profile")]
    public class TorusSegmentProfile : ScriptableObject
    {
        [Header("Mesh")]
        [Tooltip("Number of mesh subdivisions along the snake body. Borders between segments always match.")]
        [SerializeField]
        [Range(2, 256)]
        private int lengthResolution = 32;
        public int LengthResolution => lengthResolution;

        [Tooltip("Optional. Used instead of the world's default material.")]
        [SerializeField]
        private Material materialOverride;
        public Material MaterialOverride => materialOverride;

        [Header("Terrain")]
        [Tooltip("Total height range of the terrain in world units.")]
        [SerializeField]
        [Min(0f)]
        private float amplitude = 1f;
        public float Amplitude => amplitude;

        [Tooltip("Noise frequency (features per world unit).")]
        [SerializeField]
        [Min(0.001f)]
        private float frequency = 0.15f;
        public float Frequency => frequency;

        [SerializeField]
        [Range(1, 6)]
        private int octaves = 4;
        public int Octaves => octaves;

        [Tooltip("Amplitude multiplier of every next octave.")]
        [SerializeField]
        [Range(0.1f, 0.9f)]
        private float persistence = 0.5f;
        public float Persistence => persistence;

        [Tooltip("Remaps noise (0-1) to height (0-1). Use it for plateaus, ridges and canyons.")]
        [SerializeField]
        private AnimationCurve heightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        public AnimationCurve HeightCurve => heightCurve;

        [Tooltip("Shifts the noise so every segment looks different.")]
        [SerializeField]
        private Vector3 noiseOffset;
        public Vector3 NoiseOffset => noiseOffset;
    }
}
