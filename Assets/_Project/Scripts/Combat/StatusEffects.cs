using UnityEngine;

namespace WSFGameJam2026.Combat
{
    /// <summary>
    /// Slow and stun put on a body by skills (port of the LD57 frost nova slow and the SkillHit stun, without the NavMesh).
    /// Enemy AI reads <see cref="SpeedMultiplier"/> and <see cref="IsStunned"/>. Without this component a body ignores the effects.
    /// </summary>
    public class StatusEffects : MonoBehaviour
    {
        [Header("States")]
        [SerializeField]
        private float slowMultiplier = 1f;

        [SerializeField]
        private float slowedUntil = -1f;

        [SerializeField]
        private float stunnedUntil = -1f;

        public bool IsSlowed => Time.time < slowedUntil;
        public bool IsStunned => Time.time < stunnedUntil;

        /// <summary>Movement speed multiplier: 0 while stunned, the slow while slowed, 1 otherwise.</summary>
        public float SpeedMultiplier
        {
            get
            {
                if (IsStunned)
                    return 0f;

                return IsSlowed ? slowMultiplier : 1f;
            }
        }

        /// <summary>Slows the body to the given part of its speed. A stronger or longer slow wins over the current one.</summary>
        public void ApplySlow(float multiplier, float seconds)
        {
            if (seconds <= 0f)
                return;

            slowMultiplier = IsSlowed ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
            slowedUntil = Mathf.Max(slowedUntil, Time.time + seconds);
        }

        /// <summary>The body does not move or attack for the given time.</summary>
        public void ApplyStun(float seconds)
        {
            if (seconds <= 0f)
                return;

            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
        }

        public void Clear()
        {
            slowedUntil = -1f;
            stunnedUntil = -1f;
            slowMultiplier = 1f;
        }
    }
}
