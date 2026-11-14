using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Danger zone of the jaw, checked with angles only (no physics, no colliders). Its shape is not a plain band:
    /// how far it reaches in front of the head and behind it depends on phi (around the tube), so the jaws can
    /// reach forward while the cheeks stay back. Whoever stands in it gradually loses hit points
    /// (<see cref="Health.LoseHealth"/>, which ignores i-frames) until the jaw kills them.
    /// </summary>
    public class MawKillZone : MonoBehaviour
    {
        [SerializeField]
        private HeadMovement head;

        [Tooltip("Who the jaw hurts. Usually only the player.")]
        [SerializeField]
        private Health[] victims;

        [Tooltip("Hit points taken per second while standing in the zone. Ignores i-frames, so the dodge does not help.")]
        [SerializeField]
        [Min(0f)]
        private float damagePerSecond = 15f;

        [Header("Shape")]
        [Tooltip("Reach in front of the head (towards growing theta), in degrees of theta. X = phi around the tube (0-1 = 0-360°), Y = reach.")]
        [SerializeField]
        private AnimationCurve frontReachDegrees = CreateDefaultCurve(2f, 10f);

        [Tooltip("Reach behind the head (towards falling theta), in degrees of theta. X = phi around the tube (0-1 = 0-360°), Y = reach.")]
        [SerializeField]
        private AnimationCurve backReachDegrees = CreateDefaultCurve(8f, 2f);

        [Header("Editor")]
        [SerializeField]
        private bool drawGizmos = true;

        private float[] pendingDamage;

        private void Reset()
        {
            head = GetComponent<HeadMovement>();
        }

        private void Update()
        {
            if (head == null)
                return;

            if (pendingDamage == null || pendingDamage.Length != victims.Length)
                pendingDamage = new float[victims.Length];

            for (var i = 0; i < victims.Length; i++)
            {
                var victim = victims[i];
                if (victim == null || victim.IsDead || victim.isActiveAndEnabled == false || IsInside(victim) == false)
                {
                    pendingDamage[i] = 0f;
                    continue;
                }

                // Health is whole, so the damage is collected until it makes at least one point.
                pendingDamage[i] += damagePerSecond * Time.deltaTime;
                var wholeDamage = Mathf.FloorToInt(pendingDamage[i]);
                if (wholeDamage <= 0)
                    continue;

                pendingDamage[i] -= wholeDamage;
                victim.LoseHealth(wholeDamage);
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

#if UNITY_EDITOR
        // Not optimised on purpose: the outline of the zone is rebuilt every frame in the editor.
        private void OnDrawGizmos()
        {
            if (drawGizmos == false || head == null || head.World == null)
                return;

            const int Steps = 72;
            var surface = head.World.Surface;
            var worldTransform = head.World.transform;
            var isAnyoneInside = false;
            for (var i = 0; victims != null && i < victims.Length; i++)
                isAnyoneInside |= victims[i] != null && victims[i].IsDead == false && IsInside(victims[i]);

            var front = new Vector3[Steps + 1];
            var back = new Vector3[Steps + 1];
            for (var i = 0; i <= Steps; i++)
            {
                var phi = (float)i / Steps * Mathf.PI * 2f;
                var phiNormalized = (float)i / Steps;
                var frontTheta = head.Theta + frontReachDegrees.Evaluate(phiNormalized) * Mathf.Deg2Rad;
                var backTheta = head.Theta - backReachDegrees.Evaluate(phiNormalized) * Mathf.Deg2Rad;
                front[i] = worldTransform.TransformPoint(GetLiftedPoint(surface, frontTheta, phi));
                back[i] = worldTransform.TransformPoint(GetLiftedPoint(surface, backTheta, phi));
            }

            Gizmos.color = isAnyoneInside ? Color.red : new Color(1f, 0.5f, 0f);
            for (var i = 0; i < Steps; i++)
            {
                Gizmos.DrawLine(front[i], front[i + 1]);
                Gizmos.DrawLine(back[i], back[i + 1]);
            }

            // Ribs between the front and the back edge show the width of the zone.
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.5f);
            for (var i = 0; i < Steps; i += 3)
                Gizmos.DrawLine(front[i], back[i]);
        }

        private static Vector3 GetLiftedPoint(TorusSurface surface, float theta, float phi)
        {
            return surface.GetPoint(theta, phi) + surface.GetBaseNormal(theta, phi) * 0.2f;
        }
#endif

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
