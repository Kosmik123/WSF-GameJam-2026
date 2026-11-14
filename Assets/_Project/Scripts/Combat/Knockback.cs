using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Combat
{
    /// <summary>
    /// Push after a hit (port of the LD57 knockback): moves the object by a distance over a short time with OutCubic easing,
    /// along the surface, without turning it. AI reads <see cref="IsActive"/> and does not move while being pushed.
    /// </summary>
    [RequireComponent(typeof(TorusObject))]
    public class Knockback : MonoBehaviour
    {
        [Tooltip("Multiplier of the knockback distance (heavy enemies < 1).")]
        [SerializeField]
        [Min(0f)]
        private float distanceMultiplier = 1f;

        [SerializeField]
        [Min(0.02f)]
        private float duration = 0.15f;

        private TorusObject torusObject;
        private Vector3 direction;
        private float totalDistance;
        private float traveledDistance;
        private float elapsed;

        public bool IsActive { get; private set; }

        private void Awake()
        {
            torusObject = GetComponent<TorusObject>();
        }

        private void OnDisable()
        {
            IsActive = false;
        }

        public void Apply(Vector3 worldDirection, float distance)
        {
            float scaledDistance = distance * distanceMultiplier;
            if (worldDirection.sqrMagnitude < 0.0001f || scaledDistance <= 0f)
                return;

            direction = worldDirection.normalized;
            totalDistance = scaledDistance;
            traveledDistance = 0f;
            elapsed = 0f;
            IsActive = true;
        }

        private void Update()
        {
            if (IsActive == false)
                return;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            float targetDistance = totalDistance * eased;

            torusObject.MoveInDirection(direction, targetDistance - traveledDistance);
            traveledDistance = targetDistance;

            if (progress >= 1f)
                IsActive = false;
        }
    }
}
