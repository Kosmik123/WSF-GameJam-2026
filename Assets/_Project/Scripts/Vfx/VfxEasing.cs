using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>Easing curves of the effects (DOTween is not in the project).</summary>
    public static class VfxEasing
    {
        public static float OutCubic(float t)
        {
            float inverse = 1f - Mathf.Clamp01(t);
            return 1f - inverse * inverse * inverse;
        }

        public static float OutBack(float t)
        {
            const float Overshoot = 1.70158f;
            const float Factor = Overshoot + 1f;
            float shifted = Mathf.Clamp01(t) - 1f;
            return 1f + Factor * shifted * shifted * shifted + Overshoot * shifted * shifted;
        }
    }
}
