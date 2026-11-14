using UnityEngine;
using WSFGameJam2026.Combat;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Death zone of the jaw, checked with angles only (no physics, no colliders). Its shape is not a plain band:
    /// how far it reaches in front of the head and behind it depends on phi (around the tube), so the jaws can
    /// reach forward while the cheeks stay back. Whoever stands in it is killed with <see cref="Health.Kill"/>,
    /// which ignores HP and i-frames.
    /// </summary>
    public class MawKillZone : MonoBehaviour
    {
        [SerializeField]
        private HeadMovement head;

        [Tooltip("Who the jaw can kill. Usually only the player.")]
        [SerializeField]
        private Health[] victims;

        [Header("Shape")]
        [Tooltip("Reach in front of the head (towards growing theta), in degrees of theta. X = phi around the tube (0-1 = 0-360°), Y = reach.")]
        [SerializeField]
        private AnimationCurve frontReachDegrees = CreateDefaultCurve(2f, 10f);

        [Tooltip("Reach behind the head (towards falling theta), in degrees of theta. X = phi around the tube (0-1 = 0-360°), Y = reach.")]
        [SerializeField]
        private AnimationCurve backReachDegrees = CreateDefaultCurve(8f, 2f);

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

        /// <summary>True when the object stands in the zone at its own phi.</summary>
        public bool IsInside(Health target)
        {
            var angles = target.TorusObject.Angles;
            var phiNormalized = angles.PhiDegrees / 360f;

            // Positive = the target is in front of the head.
            var deltaTheta = Mathf.DeltaAngle(head.Theta * Mathf.Rad2Deg, angles.ThetaDegrees);
            return deltaTheta <= frontReachDegrees.Evaluate(phiNormalized)
                && -deltaTheta <= backReachDegrees.Evaluate(phiNormalized);
        }

        // Jaws are at the top and the bottom of the tube (phi 90° and 270°), cheeks at the sides (0° and 180°).
        private static AnimationCurve CreateDefaultCurve(float cheeks, float jaws)
        {
            var curve = new AnimationCurve(
                new Keyframe(0f, cheeks),
                new Keyframe(0.25f, jaws),
                new Keyframe(0.5f, cheeks),
                new Keyframe(0.75f, jaws),
                new Keyframe(1f, cheeks));

            for (var i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);

            return curve;
        }
    }
}
