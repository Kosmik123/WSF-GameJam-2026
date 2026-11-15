using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>Bright flash: a glowing low poly sphere growing with OutBack and shrinking with OutCubic, with a point light.</summary>
    public class VfxFlash : MonoBehaviour
    {
        private const float RiseShare = 0.35f;
        private const float LightIntensity = 3f;

        private float size;
        private float duration = 0.18f;
        private float elapsed;
        private Vector3 spin;
        private Light glow;

        public void Play(Vector3 position, Color color, float newSize, float newDuration)
        {
            size = newSize;
            duration = Mathf.Max(0.05f, newDuration);
            spin = Random.onUnitSphere * 300f;
            transform.SetPositionAndRotation(position, Random.rotation);
            transform.localScale = Vector3.zero;

            gameObject.AddComponent<MeshFilter>().sharedMesh = VfxMeshes.Get(VfxShape.Sphere);
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            VfxMaterials.Prepare(meshRenderer, VfxMaterials.Solid);
            VfxMaterials.SetColor(meshRenderer, Color.Lerp(color, Color.white, 0.35f), 0.9f, 4f);

            glow = gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = Mathf.Max(3f, size * 5f);
            glow.shadows = LightShadows.None;
            glow.intensity = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            if (progress >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float scale = progress < RiseShare
                ? VfxEasing.OutBack(progress / RiseShare)
                : 1f - VfxEasing.OutCubic((progress - RiseShare) / (1f - RiseShare));
            transform.localScale = Vector3.one * size * Mathf.Max(0f, scale);
            transform.Rotate(spin * Time.deltaTime, Space.Self);
            if (glow)
                glow.intensity = LightIntensity * Mathf.Clamp01(scale);
        }
    }
}
