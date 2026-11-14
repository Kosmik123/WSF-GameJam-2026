using UnityEngine;
using WSFGameJam2026.Combat;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Death zone of the jaw: a band of theta around the head, checked with angles only (no physics, no colliders).
    /// Whoever stands in it is killed with <see cref="Health.Kill"/>, which ignores HP and i-frames.
    /// </summary>
    public class MawKillZone : MonoBehaviour
    {
        [SerializeField]
        private HeadMovement head;

        [Tooltip("Who the jaw can kill. Usually only the player.")]
        [SerializeField]
        private Health[] victims;

        [Tooltip("Half of the width of the zone along the snake body, in degrees. Counted both ways from the head.")]
        [SerializeField]
        [Min(0f)]
        private float halfWidthDegrees = 6f;

        private void Reset()
        {
            head = GetComponent<HeadMovement>();
        }

        private void Update()
        {
            if (head == null)
                return;

            for (var i = 0; i < victims.Length; i++)
            {
                var victim = victims[i];
                if (victim == null || victim.IsDead || victim.isActiveAndEnabled == false)
                    continue;

                if (IsInside(victim))
                    victim.Kill();
            }
        }

        /// <summary>True when the object stands in the zone. Only theta matters: the zone covers the whole tube.</summary>
        public bool IsInside(Health target)
        {
            var headDegrees = head.Theta * Mathf.Rad2Deg;
            var targetDegrees = target.TorusObject.Angles.ThetaDegrees;
            return Mathf.Abs(Mathf.DeltaAngle(headDegrees, targetDegrees)) <= halfWidthDegrees;
        }
    }
}
