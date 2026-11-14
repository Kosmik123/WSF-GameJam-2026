using Unity.Mathematics;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>Segment terrain made of fractal 3D simplex noise.</summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Noise Torus Segment Profile")]
    public class NoiseTorusSegmentProfile : TorusSegmentProfile
    {
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

        public override float SampleHeight(float along, float around, Vector3 basePoint)
        {
            var samplePoint = basePoint * frequency + noiseOffset;
            float value = Mathf.Clamp01(FractalNoise(samplePoint));
            return amplitude * (heightCurve.Evaluate(value) - 0.5f);
        }

        private float FractalNoise(Vector3 point)
        {
            float sum = 0f;
            float total = 0f;
            float octaveAmplitude = 1f;
            float octaveFrequency = 1f;

            for (int i = 0; i < octaves; i++)
            {
                sum += Noise3D(point * octaveFrequency) * octaveAmplitude;
                total += octaveAmplitude;
                octaveAmplitude *= persistence;
                octaveFrequency *= 2f;
            }

            return sum / total;
        }

        /// <summary>True 3D simplex noise remapped to 0-1.</summary>
        private float Noise3D(Vector3 point)
        {
            return noise.snoise(new float3(point.x, point.y, point.z)) * 0.5f + 0.5f;
        }
    }
}
