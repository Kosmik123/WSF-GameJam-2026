using System.Collections.Generic;
using UnityEngine;
using WSFGameJam2026.Combat;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Danger zone of the jaw, checked with angles only (no physics, no colliders). Its shape is not a plain band:
    /// how far it reaches in front of the head and behind it depends on phi (around the tube), so the jaws can
    /// reach forward while the cheeks stay back. It affects every active <see cref="Health"/>, and each one chooses
    /// in its <see cref="Health.MawDamageMode"/> whether it is killed at once (<see cref="Health.Kill"/>) or loses
    /// hit points over time (<see cref="Health.LoseHealth"/>). Both ignore i-frames.
    /// </summary>
    public class MawKillZone : MonoBehaviour
    {
        [SerializeField]
        private HeadMovement head;

        [Tooltip("Hit points per second taken from bodies set to Gradual in their Health. Ignores i-frames, so the dodge does not help.")]
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

        [Tooltip("Turns the direction in which the shape follows the rotation of this object. Use it when the shape turns the other way than the object (for example when the object itself is turned around in its parent).")]
        [SerializeField]
        private bool flipRotation;

        [Header("Editor")]
        [SerializeField]
        private bool drawGizmos = true;

        private readonly Dictionary<Health, float> pendingDamage = new Dictionary<Health, float>();
        private readonly List<Health> buffer = new List<Health>();

        private void Reset()
        {
            head = GetComponent<HeadMovement>();
        }

        private void Update()
        {
            if (head == null)
                return;

            // A copy, because killing a body can disable it and change the list of active bodies.
            buffer.Clear();
            buffer.AddRange(Health.Active);

            for (var i = 0; i < buffer.Count; i++)
            {
                var victim = buffer[i];
                if (victim == null || victim.IsDead || IsInside(victim) == false)
                {
                    pendingDamage.Remove(victim);
                    continue;
                }

                if (victim.MawDamageMode == MawDamageMode.Immune)
                    continue;

                if (victim.MawDamageMode == MawDamageMode.Instant)
                {
                    victim.Kill();
                    pendingDamage.Remove(victim);
                    continue;
                }

                // Health is whole, so the damage is collected until it makes at least one point.
                pendingDamage.TryGetValue(victim, out var pending);
                pending += damagePerSecond * Time.deltaTime;
                var wholeDamage = Mathf.FloorToInt(pending);
                pendingDamage[victim] = pending - wholeDamage;
                if (wholeDamage > 0)
                    victim.LoseHealth(wholeDamage);
            }
        }

        /// <summary>
        /// How much the shape is turned around the head (degrees around the tube), taken from the rotation of this
        /// object relative to the head. Put the component on a child of the head and roll it around its forward axis.
        /// On the head itself it is 0.
        /// </summary>
        public float GetShapeRotationDegrees()
        {
            // This is the roll around the head's forward axis. The head's right points to the inner side of the torus
            // (phi 180) and its up to phi 90, so growing phi turns against the roll: see GetShapePosition.
            var relative = Quaternion.Inverse(head.transform.rotation) * transform.rotation;
            var right = relative * Vector3.right;
            var roll = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;
            return flipRotation ? -roll : roll;
        }

        /// <summary>
        /// Theta of the centre of the zone in radians: the head's theta moved by the offset of this object
        /// from the head along the body (towards the front of the head = growing theta). On the head itself it is the head's theta.
        /// </summary>
        public float GetCenterTheta()
        {
            var world = head.World;
            if (world == null)
                return head.Theta;

            var localOffset = world.transform.InverseTransformVector(transform.position - head.transform.position);
            var alongBody = Vector3.Dot(localOffset, TorusGeometry.GetThetaDirection(head.Theta));
            return head.Theta + alongBody / world.MajorRadius;
        }

        // Position on the shape curves (0-1) for a real phi in degrees: the shape is turned by the rotation of the zone.
        private float GetShapePosition(float phiDegrees)
        {
            return Mathf.Repeat(phiDegrees + GetShapeRotationDegrees(), 360f) / 360f;
        }

        /// <summary>True when the object stands in the zone at its own phi.</summary>
        public bool IsInside(Health target)
        {
            var angles = target.TorusObject.Angles;
            var phiNormalized = GetShapePosition(angles.PhiDegrees);

            // Positive = the target is in front of the head.
            var deltaTheta = Mathf.DeltaAngle(GetCenterTheta() * Mathf.Rad2Deg, angles.ThetaDegrees);
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
            foreach (var health in Health.Active)
                isAnyoneInside |= health.IsDead == false && IsInside(health);

            var centerTheta = GetCenterTheta();
            var front = new Vector3[Steps + 1];
            var back = new Vector3[Steps + 1];
            for (var i = 0; i <= Steps; i++)
            {
                var phi = (float)i / Steps * Mathf.PI * 2f;
                var phiNormalized = GetShapePosition((float)i / Steps * 360f);
                var frontTheta = centerTheta + frontReachDegrees.Evaluate(phiNormalized) * Mathf.Deg2Rad;
                var backTheta = centerTheta - backReachDegrees.Evaluate(phiNormalized) * Mathf.Deg2Rad;
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
