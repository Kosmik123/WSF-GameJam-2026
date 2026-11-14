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
