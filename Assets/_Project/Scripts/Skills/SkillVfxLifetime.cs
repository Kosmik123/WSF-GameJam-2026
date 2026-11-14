using UnityEngine;

namespace WSFGameJam2026.Skills
{
    /// <summary>Thins out and fades a line of <see cref="SkillVfx"/>, then removes it.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SkillVfxLifetime : MonoBehaviour
    {
        private LineRenderer line;
        private Color startColor;
        private float startWidth;
        private float duration = 0.3f;
        private float elapsed;

        public void Play(LineRenderer newLine, float width, float newDuration)
        {
            line = newLine;
            startColor = line.startColor;
            startWidth = width;
            duration = Mathf.Max(0.01f, newDuration);
            elapsed = 0f;
            line.widthMultiplier = width;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (line)
            {
                var color = startColor;
                color.a *= 1f - progress;
                line.startColor = color;
                line.endColor = color;
                line.widthMultiplier = Mathf.Lerp(startWidth, startWidth * 0.2f, progress);
            }

            if (progress >= 1f)
                Destroy(gameObject);
        }
    }
}
