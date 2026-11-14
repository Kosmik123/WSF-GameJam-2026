using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Core of the fireball (port of the LD57 MageFireCore): 6 low poly solids from a white-yellow centre through orange
    /// to red, spinning and pulsing with random phases, and 3 licking flames stretched back (local -Z, so the parent must
    /// face the direction of flight).
    /// </summary>
    public class VfxFireCore : MonoBehaviour
    {
        private static readonly VfxShape[] PartShapes = { VfxShape.Octa, VfxShape.Crystal, VfxShape.Tetra, VfxShape.Octa, VfxShape.CrystalForward, VfxShape.Tetra };
        private static readonly float[] PartIntensities = { 4.5f, 3.6f, 3.3f, 3f, 2.8f, 2.4f };
        private static readonly float[] PartSizes = { 0.5f, 0.7f, 0.8f, 0.9f, 1f, 1.1f };
        private static readonly Color[] PartColors =
        {
            new Color(1f, 0.95f, 0.6f), new Color(1f, 0.8f, 0.25f), new Color(1f, 0.65f, 0.15f),
            new Color(1f, 0.45f, 0.08f), new Color(1f, 0.3f, 0.06f), new Color(0.9f, 0.12f, 0.04f),
        };
        private static readonly Color[] TongueColors = { new Color(1f, 0.55f, 0.1f), new Color(1f, 0.35f, 0.06f), new Color(0.95f, 0.2f, 0.05f) };

        private Transform[] parts;
        private Vector3[] spins;
        private float[] phases;
        private float[] frequencies;
        private Transform[] tongues;
        private float[] tonguePhases;
        private Vector3[] tongueScales;
        private float seed;

        public static VfxFireCore Create(Transform parent)
        {
            var coreObject = new GameObject("Fire Core");
            coreObject.transform.SetParent(parent, false);
            coreObject.transform.localScale = Vector3.one * 1.25f;
            var core = coreObject.AddComponent<VfxFireCore>();
            core.Build();
            return core;
        }

        private void Build()
        {
            seed = Random.value * 100f;
            int count = PartShapes.Length;
            parts = new Transform[count];
            spins = new Vector3[count];
            phases = new float[count];
            frequencies = new float[count];
            for (int i = 0; i < count; i++)
            {
                float alpha = i < 2 ? 1f : 0.85f;
                var part = StylizedFx.MakeShape(transform, "Fire Part", PartShapes[i], PartColors[i], PartSizes[i], alpha, PartIntensities[i]);
                part.transform.localPosition = i == 0 ? Vector3.zero : Random.insideUnitSphere * 0.12f;
                part.transform.localRotation = Random.rotation;
                parts[i] = part.transform;
                spins[i] = Random.onUnitSphere * Random.Range(180f, 420f);
                phases[i] = Random.value * 6.28f;
                frequencies[i] = Random.Range(9f, 16f);
            }

            tongues = new Transform[TongueColors.Length];
            tonguePhases = new float[TongueColors.Length];
            tongueScales = new Vector3[TongueColors.Length];
            for (int i = 0; i < TongueColors.Length; i++)
            {
                float angle = i * 120f + Random.Range(-20f, 20f);
                var spread = Quaternion.AngleAxis(angle, Vector3.forward) * Vector3.up * 0.35f;
                var direction = new Vector3(spread.x, spread.y, -1f).normalized;
                var tongue = StylizedFx.MakeShape(transform, "Fire Tongue", VfxShape.CrystalForward, TongueColors[i], 1f, 0.75f, 3f);
                tongue.transform.localPosition = direction * 0.85f;
                tongue.transform.localRotation = Quaternion.LookRotation(direction);
                tongueScales[i] = new Vector3(0.32f, 0.32f, 1.5f - i * 0.2f);
                tongue.transform.localScale = tongueScales[i];
                tongues[i] = tongue.transform;
                tonguePhases[i] = Random.value * 6.28f;
            }
        }

        private void Update()
        {
            if (parts == null)
                return;

            float time = Time.time;
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i].Rotate(spins[i] * Time.deltaTime, Space.Self);
                parts[i].localScale = Vector3.one * PartSizes[i] * (1f + 0.25f * Mathf.Sin(time * frequencies[i] + phases[i]));
            }

            for (int i = 0; i < tongues.Length; i++)
            {
                float flicker = 0.65f + 0.7f * Mathf.PerlinNoise(time * 14f + tonguePhases[i], seed);
                var scale = tongueScales[i];
                tongues[i].localScale = new Vector3(scale.x * (0.8f + 0.4f * flicker), scale.y * (0.8f + 0.4f * flicker), scale.z * flicker);
            }
        }
    }
}
