using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Dust and debris thrown up in front of the snake's mouth, like a coming sandstorm. Every emitter is a ring around the tube
    /// (particle system with a Circle shape, emitting outwards), kept a given distance ahead of the front of the head
    /// and as wide as the terrain there. The dust takes the color of the terrain the head is eating.
    /// The particle systems are children of the head, made by WSF GameJam/Setup/Head Dust Storm.
    /// </summary>
    public class HeadDustStorm : MonoBehaviour
    {
        private const int ColorSamples = 6;

        [System.Serializable]
        private class RingEmitter
        {
            public ParticleSystem Particles;

            [Tooltip("Distance ahead of the front of the head, in meters along the body.")]
            public float AheadDistance;

            [Tooltip("Added to the radius of the terrain ring, in meters (positive = above the ground).")]
            public float RadiusOffset;

            [Tooltip("Mix of the particle color towards the terrain color (0 = keep the particle color).")]
            [Range(0f, 1f)]
            public float TerrainTint;
        }

        [SerializeField]
        private HeadMovement head;

        [SerializeField]
        private RingEmitter[] emitters;

        [Tooltip("Dust color mixed with the terrain color.")]
        [SerializeField]
        private Color dustColor = new Color(0.78f, 0.68f, 0.52f, 1f);

        [Tooltip("Seconds between updates of the terrain colors.")]
        [SerializeField]
        [Min(0.05f)]
        private float colorUpdateInterval = 0.3f;

        private float nextColorUpdate;

        private void Reset()
        {
            head = GetComponent<HeadMovement>();
        }

        private void LateUpdate()
        {
            if (head == null || head.World == null || emitters == null)
                return;

            var world = head.World;
            var surface = world.Surface;
            var headTerrain = world.HeadTerrain;
            float front = headTerrain.IsActive ? headTerrain.NeckTheta + headTerrain.HeadLength : head.Theta;
            bool updateColors = Time.time >= nextColorUpdate;
            if (updateColors)
                nextColorUpdate = Time.time + colorUpdateInterval;

            foreach (var emitter in emitters)
            {
                if (emitter == null || emitter.Particles == null)
                    continue;

                float theta = front + emitter.AheadDistance / Mathf.Max(world.MajorRadius, 1f);
                PlaceRing(world, surface, emitter, theta);
                SetEmitting(emitter.Particles, head.IsMoving);

                if (updateColors && emitter.TerrainTint > 0f)
                    TintFromTerrain(surface, emitter, theta);
            }
        }

        private static void PlaceRing(TorusWorld world, TorusSurface surface, RingEmitter emitter, float theta)
        {
            var center = TorusGeometry.GetCenterPoint(world.MajorRadius, theta);
            float radius = 0f;
            for (int i = 0; i < ColorSamples; i++)
            {
                float phi = (i + 0.5f) / ColorSamples * TorusGeometry.TwoPi;
                radius += Vector3.Distance(surface.GetPoint(theta, phi), center);
            }

            radius /= ColorSamples;
            var worldTransform = world.transform;
            var forward = worldTransform.TransformDirection(TorusGeometry.GetThetaDirection(theta));
            emitter.Particles.transform.SetPositionAndRotation(worldTransform.TransformPoint(center), Quaternion.LookRotation(forward, worldTransform.up));

            var shape = emitter.Particles.shape;
            shape.radius = Mathf.Max(0.5f, (radius + emitter.RadiusOffset) * worldTransform.lossyScale.x);
        }

        private static void SetEmitting(ParticleSystem particles, bool isEmitting)
        {
            var emission = particles.emission;
            emission.enabled = isEmitting;
            if (isEmitting && particles.isPlaying == false)
                particles.Play();
        }

        /// <summary>Start color = random between two terrain colors of the ring (top and side), mixed with the dust color.</summary>
        private void TintFromTerrain(TorusSurface surface, RingEmitter emitter, float theta)
        {
            var first = SampleTerrainColor(surface, theta, Mathf.PI * 0.5f);
            var second = SampleTerrainColor(surface, theta, 0f);
            var main = emitter.Particles.main;
            var original = main.startColor;
            float alphaMin = original.mode == ParticleSystemGradientMode.TwoColors ? original.colorMin.a : original.color.a;
            float alphaMax = original.mode == ParticleSystemGradientMode.TwoColors ? original.colorMax.a : original.color.a;
            var colorMin = Color.Lerp(dustColor, first, emitter.TerrainTint);
            var colorMax = Color.Lerp(dustColor, second, emitter.TerrainTint);
            colorMin.a = alphaMin;
            colorMax.a = alphaMax;
            main.startColor = new ParticleSystem.MinMaxGradient(colorMin, colorMax);
        }

        private static Color SampleTerrainColor(TorusSurface surface, float theta, float phi)
        {
            theta = Mathf.Repeat(theta, TorusGeometry.TwoPi);
            float segmentPosition = theta / surface.SegmentAngle;
            int segment = Mathf.FloorToInt(segmentPosition);
            float along = segmentPosition - segment;
            float around = phi / TorusGeometry.TwoPi;
            var color = TorusSegmentBlend.GetColor(surface.GetProfile(segment), surface.GetProfile(segment - 1), surface.GetProfile(segment + 1),
                along, around, surface.BlendWidth, surface.GetBasePoint(theta, phi));
            color.a = 1f;
            return color;
        }
    }
}
