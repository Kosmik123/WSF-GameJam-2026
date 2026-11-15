using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// A wall at the theta of the head that the player can not cross, whatever phi it has: it can not get onto the head or inside it.
    /// Objects that are stopped by it have "Is Blocked By Head Barrier" on their <see cref="TorusObject"/>.
    /// Put it on the head. The wall moves with the head.
    /// </summary>
    [RequireComponent(typeof(HeadMovement))]
    public class HeadBarrier : MonoBehaviour
    {
        [SerializeField]
        private TorusWorld world;

        [Tooltip("Position of the wall relative to the pivot of the head, in degrees of theta. Positive = ahead of the head. Keep it ahead of the weak point (which stands behind the pivot), or the player can not reach it.")]
        [SerializeField]
        private float offsetDegrees;

        private HeadMovement movement;

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
        }

        private void Awake()
        {
            movement = GetComponent<HeadMovement>();
            if (world == null)
                world = FindAnyObjectByType<TorusWorld>();
        }

        private void LateUpdate()
        {
            if (world)
                world.HeadBarrier.Set(movement.Theta + offsetDegrees * Mathf.Deg2Rad);
        }

        private void OnDisable()
        {
            if (world)
                world.HeadBarrier.Deactivate();
        }

        private void OnDrawGizmosSelected()
        {
            var gizmoWorld = world ? world : FindAnyObjectByType<TorusWorld>();
            if (gizmoWorld == null)
                return;

            // The wall is a ring around the tube. In edit mode the head is measured where it stands (theta 0 is on +Z, growing towards +X).
            var localPosition = gizmoWorld.transform.InverseTransformPoint(transform.position);
            float theta = Mathf.Atan2(localPosition.x, localPosition.z) + offsetDegrees * Mathf.Deg2Rad;

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = gizmoWorld.transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            const int segments = 48;
            var previous = GetRingPoint(gizmoWorld, theta, segments - 1, segments);
            for (int i = 0; i < segments; i++)
            {
                var current = GetRingPoint(gizmoWorld, theta, i, segments);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }

            Gizmos.matrix = previousMatrix;
        }

        private static Vector3 GetRingPoint(TorusWorld targetWorld, float theta, int index, int count)
        {
            float phi = index * TorusGeometry.TwoPi / count;
            return TorusGeometry.GetPoint(targetWorld.MajorRadius, targetWorld.MinorRadius, theta, phi);
        }
    }
}
