using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Scales a renderer of an effect from one size to another (OutCubic), fades it out at the end, can turn it around
    /// its local up, then removes the object. Used by shockwave rings, the spin arc and the sword arc.
    /// </summary>
    public class VfxFade : MonoBehaviour
    {
        private Renderer target;
        private Color color;
        private float startAlpha;
        private float intensity;
        private Vector3 startScale;
        private Vector3 endScale;
        private float duration;
        private float fadeTime;
        private float degreesPerSecond;
        private float elapsed;

        public void Play(Renderer renderer, Color newColor, float alpha, float newIntensity, Vector3 fromScale, Vector3 toScale,
            float seconds, float fadeSeconds, float spinDegreesPerSecond = 0f)
        {
            target = renderer;
            color = newColor;
            startAlpha = alpha;
            intensity = newIntensity;
            startScale = fromScale;
            endScale = toScale;
            duration = Mathf.Max(0.02f, seconds);
            fadeTime = Mathf.Clamp(fadeSeconds, 0.01f, duration);
            degreesPerSecond = spinDegreesPerSecond;
            elapsed = 0f;
            Apply();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (degreesPerSecond != 0f)
                transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);

            Apply();
            if (elapsed >= duration)
                Destroy(gameObject);
        }

        private void Apply()
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.LerpUnclamped(startScale, endScale, VfxEasing.OutCubic(progress));
            if (target)
                VfxMaterials.SetColor(target, color, startAlpha * Mathf.Clamp01((duration - elapsed) / fadeTime), intensity);
        }
    }
}
