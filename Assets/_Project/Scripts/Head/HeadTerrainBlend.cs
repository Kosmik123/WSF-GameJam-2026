using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Makes the terrain around the head follow the outline of the head's neck, so the body grows out of the head
    /// without a step. The outline is measured from the head's meshes on the first frame.
    /// The head must not rotate around its own forward axis.
    /// </summary>
    [RequireComponent(typeof(HeadMovement))]
    public class HeadTerrainBlend : MonoBehaviour
    {
        private const int OutlineResolution = 64;

        [SerializeField]
        private TorusWorld world;

        [Tooltip("Length of the transition from the head outline to the normal terrain (behind the neck and in front of the head), in degrees along the snake body.")]
        [SerializeField]
        [Min(0f)]
        private float blendLengthDegrees = 25f;

        [Tooltip("Thickness of the slice of the head, measured from its rear end, whose widest points make the outline.")]
        [SerializeField]
        [Min(0.01f)]
        private float neckSliceDepth = 1f;

        [Tooltip("Added to the measured height of the outline. Positive lifts the terrain at the neck.")]
        [SerializeField]
        private float neckHeightOffset;

        private HeadMovement movement;
        private bool isMeasured;

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

        private void OnDisable()
        {
            if (world)
                world.HeadTerrain.Deactivate();

            isMeasured = false;
        }

        private void LateUpdate()
        {
            if (world == null)
                return;

            if (isMeasured == false)
                isMeasured = Measure();

            if (isMeasured)
                world.HeadTerrain.SetTheta(movement.Theta);
        }

        private void OnDrawGizmosSelected()
        {
            var gizmoWorld = world ? world : FindAnyObjectByType<TorusWorld>();
            if (gizmoWorld == null)
                return;

            // The head is measured where it stands right now, so the gizmos also work in edit mode (theta 0 is on +Z, growing towards +X).
            var localPosition = gizmoWorld.transform.InverseTransformPoint(transform.position);
            var theta = Mathf.Atan2(localPosition.x, localPosition.z);
            var heights = new float[OutlineResolution];
            if (HeadNeckProfiler.TryMeasure(gizmoWorld, theta, GetComponentsInChildren<Renderer>(), neckSliceDepth, heights, out float neckOffset, out float headLength) == false)
                return;

            var majorRadius = gizmoWorld.MajorRadius;
            var neckTheta = theta - neckOffset;
            var sliceTheta = neckTheta + neckSliceDepth / majorRadius;
            var headEndTheta = neckTheta + headLength;
            var blendLength = blendLengthDegrees * Mathf.Deg2Rad;

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = gizmoWorld.transform.localToWorldMatrix;

            var outline = new float[OutlineResolution];
            for (int i = 0; i < outline.Length; i++)
                outline[i] = heights[i] + neckHeightOffset;

            // Measured slice (cyan): the widest points of the head between the rear end and the slice depth make the outline.
            Gizmos.color = Color.cyan;
            DrawRing(gizmoWorld, sliceTheta, heights);
            for (int i = 0; i < heights.Length; i += 4)
                Gizmos.DrawLine(GetOutlinePoint(gizmoWorld, neckTheta, i, heights[i]), GetOutlinePoint(gizmoWorld, sliceTheta, i, heights[i]));

            // Measured outline at the rear end of the head (green).
            Gizmos.color = Color.green;
            DrawRing(gizmoWorld, neckTheta, heights);

            // The outline the terrain blends into, with the height offset applied (yellow).
            if (Mathf.Approximately(neckHeightOffset, 0f) == false)
            {
                Gizmos.color = Color.yellow;
                DrawRing(gizmoWorld, neckTheta, outline);
            }

            // Front end of the head (white).
            Gizmos.color = Color.white;
            DrawRing(gizmoWorld, headEndTheta, null);

            // Where the blend starts, behind the neck and in front of the head (red).
            Gizmos.color = Color.red;
            DrawRing(gizmoWorld, neckTheta - blendLength, null);
            DrawRing(gizmoWorld, headEndTheta + blendLength, null);

            Gizmos.matrix = previousMatrix;
        }

        /// <summary>Draws a closed outline around the tube at theta. Null heights draw the plain tube (height 0).</summary>
        private static void DrawRing(TorusWorld world, float theta, float[] heights)
        {
            var count = heights == null ? OutlineResolution : heights.Length;
            var previous = GetOutlinePoint(world, theta, count - 1, heights == null ? 0f : heights[count - 1]);
            for (int i = 0; i < count; i++)
            {
                var current = GetOutlinePoint(world, theta, i, heights == null ? 0f : heights[i]);
                Gizmos.DrawLine(previous, current);
                previous = current;
            }
        }

        private static Vector3 GetOutlinePoint(TorusWorld world, float theta, int index, float height)
        {
            var phi = index * TorusGeometry.TwoPi / OutlineResolution;
            return TorusGeometry.GetCenterPoint(world.MajorRadius, theta) + TorusGeometry.GetNormal(theta, phi) * (world.MinorRadius + height);
        }

        private bool Measure()
        {
            movement.Refresh();

            var heights = new float[OutlineResolution];
            var renderers = GetComponentsInChildren<Renderer>();
            if (HeadNeckProfiler.TryMeasure(world, movement.Theta, renderers, neckSliceDepth, heights, out float neckOffset, out float headLength) == false)
            {
                Debug.LogWarning("Head has no mesh to measure the neck outline from.", this);
                enabled = false;
                return false;
            }

            for (int i = 0; i < heights.Length; i++)
                heights[i] += neckHeightOffset;

            world.HeadTerrain.Configure(neckOffset, headLength, blendLengthDegrees * Mathf.Deg2Rad, heights);
            world.HeadTerrain.SetTheta(movement.Theta);
            return true;
        }
    }
}
