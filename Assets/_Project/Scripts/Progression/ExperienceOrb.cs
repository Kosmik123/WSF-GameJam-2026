using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Progression
{
    /// <summary>
    /// Glowing XP crystal dropped by enemies (port of the LD57 EssencePickup, on the torus surface instead of physics):
    /// it jumps out in an arc, then flies to the player when the player is within the magnet radius, and gives its XP
    /// when it reaches the player. Disappears after its lifetime when nobody takes it.
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    public class ExperienceOrb : MonoBehaviour
    {
        [SerializeField]
        private Transform visual;

        [SerializeField]
        [Min(0f)]
        private float magnetRadius = 4f;

        [SerializeField]
        [Min(0f)]
        private float collectRadius = 0.7f;

        [Tooltip("Time of the jump out of the enemy, before the orb can fly to the player.")]
        [SerializeField]
        [Min(0f)]
        private float scatterTime = 0.5f;

        [SerializeField]
        [Min(0f)]
        private float scatterDistance = 1.5f;

        [SerializeField]
        [Min(0f)]
        private float scatterHeight = 0.8f;

        [SerializeField]
        [Min(0f)]
        private float flySpeed = 14f;

        [SerializeField]
        [Min(1f)]
        private float lifetime = 30f;

        [Tooltip("Degrees per second.")]
        [SerializeField]
        private float spinSpeed = 180f;

        private TorusObject torusObject;
        private Experience target;
        private TorusObject targetObject;
        private Vector3 scatterDirection;
        private Vector3 visualBasePosition;
        private float age;
        private int value;
        private bool isFlying;

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
            if (visual)
                visualBasePosition = visual.localPosition;
        }

        /// <summary>Places the orb at the enemy and gives it its XP value and the player who can take it.</summary>
        public void Launch(TorusWorld world, TorusAngles angles, Vector3 direction, int xp, Experience player)
        {
            torusObject.Place(world, angles, direction);
            scatterDirection = direction;
            value = Mathf.Max(1, xp);
            target = player;
            targetObject = player ? player.GetComponent<TorusObject>() : null;

            // Orbs left from the last run do not give XP to the new one.
            if (target)
                target.OnProgressReset += Target_OnProgressReset;
            age = 0f;
            isFlying = false;
        }

        private void Target_OnProgressReset()
        {
            Destroy(gameObject);
        }

        private void Update()
        {
            age += Time.deltaTime;
            if (target == null || targetObject == null || age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            if (age < scatterTime)
            {
                Scatter();
                return;
            }

            var offset = torusObject.GetSurfaceOffsetTo(targetObject);
            float distance = offset.magnitude;
            if (distance <= collectRadius)
            {
                target.AddXp(value);
                Destroy(gameObject);
                return;
            }

            if (isFlying || distance <= magnetRadius)
            {
                // Once attracted, it keeps flying to the player even when the player runs out of the radius.
                isFlying = true;
                torusObject.MoveInDirection(offset, Mathf.Min(distance, flySpeed * Time.deltaTime));
            }

            Animate(0f);
        }

        private void Scatter()
        {
            float progress = age / scatterTime;
            torusObject.MoveInDirection(scatterDirection, scatterDistance / scatterTime * Time.deltaTime * (1f - progress));
            Animate(scatterHeight * Mathf.Sin(progress * Mathf.PI));
        }

        private void Animate(float lift)
        {
            if (visual == null)
                return;

            float bob = 0.12f * Mathf.Sin(age * 4f);
            visual.localPosition = visualBasePosition + Vector3.up * (lift + bob);
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        private void OnDestroy()
        {
            if (target)
                target.OnProgressReset -= Target_OnProgressReset;
        }
    }
}
