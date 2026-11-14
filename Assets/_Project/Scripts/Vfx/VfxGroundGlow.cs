using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>Glowing spot on the ground: a big soft decal, soft blobs rising along the local up and a light, for some seconds.</summary>
    public class VfxGroundGlow : MonoBehaviour
    {
        private float radius;
        private float duration;
        private float rise;
        private float elapsed;
        private float carry;
        private Color color;
        private Vector3 up;
        private Vector3 right;
        private Vector3 forward;
        private Light glow;

        public void Play(Vector3 newUp, float newRadius, Color newColor, float seconds, float newRise)
        {
            up = newUp.normalized;
            VfxGround.GetTangents(up, out right, out forward);
            radius = Mathf.Max(0.3f, newRadius);
            color = newColor;
            duration = Mathf.Max(0.1f, seconds);
            rise = newRise;

            var lightObject = new GameObject("Glow Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.position = transform.position + up * 0.6f;
            glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = color;
            glow.range = radius * 2f + 1f;
            glow.shadows = LightShadows.None;

            VfxParticles.Decal(transform.position + up * 0.04f, up, Vector3.zero, color, radius * 2.2f, duration);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration)
            {
                Destroy(gameObject);
                return;
            }

            float fade = Mathf.Clamp01((duration - elapsed) / Mathf.Min(0.5f, duration));
            glow.intensity = 1.8f * fade;
            carry += Mathf.Clamp(radius * radius * 1.6f, 4f, 30f) * fade * Time.deltaTime;
            while (carry >= 1f)
            {
                carry -= 1f;
                var disc = Random.insideUnitCircle * radius;
                var position = transform.position + right * disc.x + forward * disc.y + up * 0.06f;
                VfxParticles.Soft(position, up * rise * Random.Range(0.5f, 1.4f), Color.Lerp(color, Color.white, Random.value * 0.25f),
                    Random.Range(0.4f, 0.8f), Random.Range(0.5f, 0.85f));
            }
        }
    }
}
