using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Ice of the frost nova (port of the LD57 MageIceSpikes): crystals grow out of the ground (OutBack, 0.15 s), stand for 1.2 s
    /// and shatter, while a frosty mist of half transparent solids drifts up. Everything grows along the local up of the spot.
    /// </summary>
    public class VfxIceSpikes : MonoBehaviour
    {
        private const float GrowTime = 0.15f;
        private const float StandTime = 1.2f;
        private const float MistLife = 2.2f;

        private Transform[] spikes;
        private float[] heights;
        private float[] widths;
        private Vector3[] basePositions;
        private Transform[] mist;
        private MeshRenderer[] mistRenderers;
        private Vector3[] mistVelocities;
        private float[] mistSizes;
        private Color iceColor;
        private Color mistColor;
        private Vector3 up;
        private float elapsed;
        private bool isShattered;

        public void Play(Vector3 newUp, float radius, Color color, int count, int mistCount)
        {
            up = newUp.normalized;
            iceColor = color;
            mistColor = Color.Lerp(color, Color.white, 0.4f);
            VfxGround.GetTangents(up, out var right, out var forward);
            var tilt = Quaternion.FromToRotation(Vector3.up, up);

            spikes = new Transform[count];
            heights = new float[count];
            widths = new float[count];
            basePositions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = radius * 0.9f * Mathf.Sqrt(Random.Range(0.25f, 1f));
                basePositions[i] = transform.position + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * distance;
                heights[i] = Random.Range(0.8f, 1.5f);
                widths[i] = Random.Range(0.22f, 0.36f);

                var spike = StylizedFx.MakeShape(transform, "Ice Spike", VfxShape.Crystal, Color.Lerp(color, Color.white, Random.value * 0.5f), 1f, 0.95f, 2.2f);
                spike.transform.SetPositionAndRotation(basePositions[i],
                    tilt * Quaternion.Euler(Random.Range(-10f, 10f), Random.value * 360f, Random.Range(-10f, 10f)));
                spike.transform.localScale = new Vector3(widths[i], 0.001f, widths[i]);
                spikes[i] = spike.transform;
            }

            mist = new Transform[mistCount];
            mistRenderers = new MeshRenderer[mistCount];
            mistVelocities = new Vector3[mistCount];
            mistSizes = new float[mistCount];
            for (int i = 0; i < mistCount; i++)
            {
                var disc = Random.insideUnitCircle * radius * 0.8f;
                var shape = i % 2 == 0 ? VfxShape.Sphere : VfxShape.Octa;
                var cloud = StylizedFx.MakeShape(transform, "Frost Mist", shape, mistColor, 1f, 0.2f, 1.3f);
                cloud.transform.SetPositionAndRotation(transform.position + right * disc.x + forward * disc.y + up * Random.Range(0.15f, 0.5f),
                    Random.rotation);
                mistSizes[i] = Random.Range(0.6f, 1.1f);
                cloud.transform.localScale = Vector3.one * mistSizes[i];
                mistVelocities[i] = up * Random.Range(0.2f, 0.4f) + (right * Random.Range(-1f, 1f) + forward * Random.Range(-1f, 1f)) * 0.15f;
                mist[i] = cloud.transform;
                mistRenderers[i] = cloud.GetComponent<MeshRenderer>();
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (isShattered == false)
                UpdateSpikes();

            float fade = Mathf.Clamp01(elapsed / 0.3f) * Mathf.Clamp01(1f - elapsed / MistLife);
            for (int i = 0; i < mist.Length; i++)
            {
                if (mist[i] == null)
                    continue;

                mist[i].position += mistVelocities[i] * Time.deltaTime;
                mist[i].localScale = Vector3.one * mistSizes[i] * (1f + elapsed * 0.25f);
                VfxMaterials.SetColor(mistRenderers[i], mistColor, 0.22f * fade, 1.3f);
            }

            if (elapsed >= MistLife)
                Destroy(gameObject);
        }

        private void UpdateSpikes()
        {
            float growth = elapsed < GrowTime ? VfxEasing.OutBack(elapsed / GrowTime) : 1f;
            for (int i = 0; i < spikes.Length; i++)
            {
                if (spikes[i] == null)
                    continue;

                float height = heights[i] * Mathf.Max(0.001f, growth);
                spikes[i].localScale = new Vector3(widths[i], height, widths[i]);
                spikes[i].position = basePositions[i] + spikes[i].up * height * 0.5f;
            }

            if (elapsed < GrowTime + StandTime)
                return;

            isShattered = true;
            for (int i = 0; i < spikes.Length; i++)
            {
                if (spikes[i] == null)
                    continue;

                StylizedFx.Burst(basePositions[i] + up * heights[i] * 0.6f, up, iceColor, 3, 0.09f, 2.5f, 0.5f);
                Destroy(spikes[i].gameObject);
            }
        }
    }
}
