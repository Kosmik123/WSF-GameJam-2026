using UnityEngine;
using UnityEngine.Serialization;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Head
{
    /// <summary>
    /// Makes the terrain around the head follow the outline of the head's neck, so the body grows out of the head
    /// without a step. The outline is measured from the head's meshes on the first frame.
    /// The head may rotate around its own forward axis: the outline and the height map turn with the reference transform.
    /// </summary>
    [RequireComponent(typeof(HeadMovement))]
    public class HeadTerrainBlend : MonoBehaviour
    {
        private const int OutlineResolution = 64;

        [SerializeField]
        private TorusWorld world;

        [Header("Neck")]
        [Tooltip("Length of the transition from the head outline to the normal terrain behind the neck, in degrees along the snake body.")]
        [SerializeField]
        [Min(0f)]
        private float headBlendLengthDegrees = 25f;

        [Tooltip("Thickness of the slice of the head, measured from its rear end, whose widest points make the outline, in degrees along the snake body.")]
        [SerializeField]
        [Min(0.01f)]
        private float neckSliceDepthDegrees = 3f;

        [Tooltip("Added to the measured length of the head, in degrees along the snake body. Negative moves the front end (white gizmo) back, so the terrain stops blending earlier.")]
        [SerializeField]
        private float headFrontOffsetDegrees;

        [Tooltip("Added to the measured height of the outline. Positive lifts the terrain at the neck.")]
        [SerializeField]
        private float neckHeightOffset;

        [Header("Head surface")]
        [Tooltip("The head's rotation around its forward axis is read from this transform: its local Z is the forward axis of the head and its local Y shows the roll. Empty = this object.")]
        [SerializeField]
        private Transform rollReference;

        [Tooltip("The player walks on the real surface of the head, baked from its meshes into a height map. Off = the neck outline stretched along the head.")]
        [SerializeField]
        private bool useHeadHeightMap = true;

        [Tooltip("Size of a cell of the head height map along the body, in world units. Make it not larger than the terrain mesh vertices spacing.")]
        [SerializeField]
        [Min(0.05f)]
        private float headMapCellSize = 0.25f;

        [Tooltip("Number of cells of the head height map around the tube.")]
        [SerializeField]
        [Min(8)]
        private int headMapColumns = 128;

        [Header("Tail")]
        [Tooltip("Length of the zone ahead of the head where the tail thins out just before it is eaten, in degrees along the snake body. 0 = no thinning.")]
        [SerializeField]
        [Min(0f)]
        private float tailTaperLengthDegrees = 30f;

        [Tooltip("Factor of the tail's tube radius along the taper zone. X: 0 = the front of the head, 1 = start of the zone (away from the head). Y: 0 = centre of the tube, 1 = original radius.")]
        [SerializeField]
        private AnimationCurve tailTaperCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        private HeadMovement movement;
        private bool isMeasured;
        private float measuredRoll;

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

            if (isMeasured == false)
                return;

            world.HeadTerrain.SetTheta(movement.Theta);
            world.HeadTerrain.SetRoll(GetRoll() - measuredRoll);
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
            if (HeadNeckProfiler.TryMeasure(gizmoWorld, theta, GetComponentsInChildren<Renderer>(), GetSliceDepth(gizmoWorld), heights, out float neckOffset, out float headLength) == false)
                return;

            var neckTheta = theta - neckOffset;
            var sliceTheta = neckTheta + neckSliceDepthDegrees * Mathf.Deg2Rad;
            var headEndTheta = neckTheta + GetAdjustedHeadLength(headLength);
            var blendLength = headBlendLengthDegrees * Mathf.Deg2Rad;

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

            // Where the blend ends behind the neck (red). Ahead of the head the terrain is not touched.
            Gizmos.color = Color.red;
            DrawRing(gizmoWorld, neckTheta - blendLength, null);

            // Where the tail starts thinning out ahead of the head (magenta), with the radius at the middle of the zone.
            if (tailTaperLengthDegrees > 0f)
            {
                var tailLength = tailTaperLengthDegrees * Mathf.Deg2Rad;
                Gizmos.color = Color.magenta;
                DrawRing(gizmoWorld, headEndTheta + tailLength, null);
                DrawScaledRing(gizmoWorld, headEndTheta + tailLength * 0.5f, tailTaperCurve.Evaluate(0.5f));
            }

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

        /// <summary>Draws the plain tube at theta with its radius multiplied by the given factor.</summary>
        private static void DrawScaledRing(TorusWorld world, float theta, float scale)
        {
            var heights = new float[OutlineResolution];
            for (int i = 0; i < heights.Length; i++)
                heights[i] = world.MinorRadius * (Mathf.Max(0f, scale) - 1f);

            DrawRing(world, theta, heights);
        }

        private static Vector3 GetOutlinePoint(TorusWorld world, float theta, int index, float height)
        {
            var phi = index * TorusGeometry.TwoPi / OutlineResolution;
            return TorusGeometry.GetCenterPoint(world.MajorRadius, theta) + TorusGeometry.GetNormal(theta, phi) * (world.MinorRadius + height);
        }

        /// <summary>Slice thickness converted from degrees along the body to world units on the centre line.</summary>
        private float GetSliceDepth(TorusWorld targetWorld)
        {
            return neckSliceDepthDegrees * Mathf.Deg2Rad * targetWorld.MajorRadius;
        }

        /// <summary>Measured head length (radians) with <see cref="headFrontOffsetDegrees"/> applied.</summary>
        private float GetAdjustedHeadLength(float measuredLength)
        {
            return Mathf.Max(0f, measuredLength + headFrontOffsetDegrees * Mathf.Deg2Rad);
        }

        /// <summary>Angle (radians of phi) of the up direction of the reference transform in the cross-section of the tube at the head.</summary>
        private float GetRoll()
        {
            var reference = rollReference ? rollReference : transform;
            var localUp = world.transform.InverseTransformDirection(reference.up);
            var outward = TorusGeometry.GetNormal(movement.Theta, 0f);
            var up = TorusGeometry.GetNormal(movement.Theta, Mathf.PI * 0.5f);
            return Mathf.Atan2(Vector3.Dot(localUp, up), Vector3.Dot(localUp, outward));
        }

        private void BakeHeadMap(Renderer[] renderers)
        {
            if (useHeadHeightMap == false)
                return;

            if (HeadHeightMapBaker.TryBake(world, movement.Theta, renderers, headMapCellSize, headMapColumns, out float[,] map, out float mapLength) == false)
            {
                Debug.LogWarning("Could not bake the height map of the head, the neck outline is used instead.", this);
                return;
            }

            for (int row = 0; row < map.GetLength(0); row++)
                for (int column = 0; column < map.GetLength(1); column++)
                    map[row, column] += neckHeightOffset;

            world.HeadTerrain.ConfigureHeadMap(map, mapLength, neckSliceDepthDegrees * Mathf.Deg2Rad);
        }

        private bool Measure()
        {
            movement.Refresh();
            measuredRoll = GetRoll();

            var heights = new float[OutlineResolution];
            var renderers = GetComponentsInChildren<Renderer>();
            if (HeadNeckProfiler.TryMeasure(world, movement.Theta, renderers, GetSliceDepth(world), heights, out float neckOffset, out float headLength) == false)
            {
                Debug.LogWarning("Head has no mesh to measure the neck outline from.", this);
                enabled = false;
                return false;
            }

            for (int i = 0; i < heights.Length; i++)
                heights[i] += neckHeightOffset;

            headLength = GetAdjustedHeadLength(headLength);
            world.HeadTerrain.Configure(neckOffset, headLength, headBlendLengthDegrees * Mathf.Deg2Rad, heights);
            BakeHeadMap(renderers);
            world.HeadTerrain.ConfigureTail(tailTaperLengthDegrees * Mathf.Deg2Rad, tailTaperCurve);
            world.HeadTerrain.SetTheta(movement.Theta);
            return true;
        }
    }
}
