using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Look of a projectile built at runtime (port of the LD57 PlayerBolt visuals): a glowing crystal with two orbiting solids,
    /// or a fire core with sparks and smoke, a light and a shard trail. Hides the placeholder sphere of the prefab and
    /// shows a small burst when the projectile is removed (impact or end of its range). Only a visual, the flight is elsewhere.
    /// </summary>
    public class ProjectileVisual : MonoBehaviour
    {
        [SerializeField]
        private Color color = new Color(0.45f, 0.65f, 1f);

        [Tooltip("Fire core with sparks and smoke instead of a crystal core.")]
        [SerializeField]
        private bool isFiery;

        [Tooltip("Size of the trail shards in world units.")]
        [SerializeField]
        [Min(0.01f)]
        private float trailSize = 0.1f;

        [SerializeField]
        private bool hideBaseRenderer = true;

        [SerializeField]
        private bool playsImpact = true;

        private static bool isQuitting;

        private float nextSparkTime;
        private float nextSmokeTime;

        private void Awake()
        {
            if (hideBaseRenderer && TryGetComponent<MeshRenderer>(out var baseRenderer))
                baseRenderer.enabled = false;

            if (isFiery)
            {
                VfxFireCore.Create(transform);
                StylizedFx.AddTrail(gameObject, new Color(1f, 0.5f, 0.12f), trailSize * 1.4f, 0.5f, true);
                AddLight(new Color(1f, 0.5f, 0.15f), 2.2f, 4f);
                return;
            }

            BuildCrystal();
            StylizedFx.AddTrail(gameObject, color, trailSize, 0.4f, false);
            AddLight(color, 1.6f, 3f);
        }

        private void BuildCrystal()
        {
            var core = StylizedFx.MakeShape(transform, "Core", VfxShape.Octa, Color.Lerp(color, Color.white, 0.6f), 1f, 1f, 4f);
            core.transform.localScale = new Vector3(0.8f, 0.8f, 1.4f);
            core.AddComponent<VfxSpin>().Init(new Vector3(0f, 0f, 540f));

            var glow = StylizedFx.MakeShape(transform, "Glow", VfxShape.Sphere, color, 1.3f, 0.35f, 2.5f);
            glow.AddComponent<VfxSpin>().Init(new Vector3(90f, 140f, 0f));

            var pivot = new GameObject("Orbit");
            pivot.transform.SetParent(transform, false);
            pivot.AddComponent<VfxSpin>().Init(new Vector3(0f, 0f, 720f));
            for (int i = 0; i < 2; i++)
            {
                var shape = i == 0 ? VfxShape.Octa : VfxShape.Tetra;
                var orbiter = StylizedFx.MakeShape(pivot.transform, "Orbiter", shape, color, 0.35f, 1f, 3.5f);
                orbiter.transform.localPosition = new Vector3(i == 0 ? 0.75f : -0.75f, 0f, 0f);
            }
        }

        private void AddLight(Color lightColor, float intensity, float range)
        {
            var glowLight = gameObject.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = lightColor;
            glowLight.intensity = intensity;
            glowLight.range = range;
            glowLight.shadows = LightShadows.None;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetQuitting()
        {
            isQuitting = false;
            Application.quitting -= Application_Quitting;
            Application.quitting += Application_Quitting;
        }

        private static void Application_Quitting()
        {
            isQuitting = true;
        }

        private void Update()
        {
            if (isFiery == false)
                return;

            var back = -transform.forward;
            var behind = transform.position + back * transform.lossyScale.x * 0.5f;
            if (Time.time >= nextSparkTime)
            {
                nextSparkTime = Time.time + 0.03f;
                StylizedFx.FireSpark(behind, back, transform.up);
            }

            if (Time.time >= nextSmokeTime)
            {
                nextSmokeTime = Time.time + 0.09f;
                StylizedFx.SmokePuff(behind + back * 0.2f, transform.up, 0.6f, 0.3f);
            }
        }

        private void OnDestroy()
        {
            // Not while a scene is unloaded or the game is closing: new objects would leak then.
            if (playsImpact == false || isQuitting || gameObject.scene.isLoaded == false)
                return;

            var position = transform.position;
            var up = transform.up;
            var impactColor = isFiery ? new Color(1f, 0.55f, 0.15f) : color;
            StylizedFx.Flash(position, impactColor, 0.7f);
            StylizedFx.Burst(position, up, impactColor, 10, 0.16f, 5f, 0.5f);
            var ground = VfxGround.Below(position, out var groundUp);
            StylizedFx.Ring(ground, groundUp, 0.9f, impactColor, 10);
        }
    }
}
