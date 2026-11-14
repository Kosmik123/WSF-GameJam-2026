using System;
using UnityEngine;

namespace WSFGameJam2026.Vfx
{
    /// <summary>Calls a tick with the progress (0-1) every interval for a time, then removes itself. The first tick is in the first frame.</summary>
    public class VfxRepeater : MonoBehaviour
    {
        private Action<float> tick;
        private float duration;
        private float interval;
        private float elapsed;
        private float nextTick;

        public void Play(float newDuration, float newInterval, Action<float> newTick)
        {
            duration = Mathf.Max(0.01f, newDuration);
            interval = Mathf.Max(0.01f, newInterval);
            tick = newTick;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            while (nextTick <= duration + 0.0001f && nextTick <= elapsed)
            {
                tick?.Invoke(Mathf.Clamp01(nextTick / duration));
                nextTick += interval;
            }

            if (nextTick > duration + 0.0001f)
                Destroy(gameObject);
        }
    }
}
