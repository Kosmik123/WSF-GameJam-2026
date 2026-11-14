using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>Pulse of an orb when it hits: the body jumps to 1.4 of its size and shrinks back in 0.2 s.</summary>
    public class VfxOrbPulse : MonoBehaviour
    {
        private const float Duration = 0.2f;
        private const float PulseScale = 1.4f;

        private Transform body;
        private float timeLeft;

        public void Init(Transform newBody)
        {
            body = newBody;
        }

        public void Pulse()
        {
            timeLeft = Duration;
            if (body)
                body.localScale = Vector3.one * PulseScale;
        }

        private void Update()
        {
            if (timeLeft <= 0f || body == null)
                return;

            timeLeft -= Time.deltaTime;
            body.localScale = Vector3.one * Mathf.Lerp(1f, PulseScale, Mathf.Clamp01(timeLeft / Duration));
        }
    }
}
