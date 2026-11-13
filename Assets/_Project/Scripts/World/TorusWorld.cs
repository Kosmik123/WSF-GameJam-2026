using UnityEngine;
using UnityEngine.Rendering;

namespace WSFGameJam2026.World
{
    /// <summary>Builds the snake-shaped torus world out of procedurally generated segments.</summary>
    [ExecuteAlways]
    public class TorusWorld : MonoBehaviour
    {
        [Header("Shape")]
        [SerializeField]
        [Min(1f)]
        private float majorRadius = 40f;
        public float MajorRadius => majorRadius;

        [SerializeField]
        [Min(0.5f)]
        private float minorRadius = 10f;
        public float MinorRadius => minorRadius;

        [SerializeField]
        [Range(3, 64)]
        private int segmentCount = 14;
        public int SegmentCount => segmentCount;

        [Tooltip("Subdivisions around the tube. Shared by all segments so their borders match.")]
        [SerializeField]
        [Range(8, 256)]
        private int sidesResolution = 64;

        [Header("Terrain")]
        [Tooltip("Segment i uses profile i % Length. Empty = flat world.")]
        [SerializeField]
        private TorusSegmentProfile[] segmentProfiles;

        [Tooltip("Part of a segment (from each end) where terrain of neighbouring segments is cross-faded.")]
        [SerializeField]
        [Range(0f, 0.5f)]
        private float blendWidth = 0.25f;

        [Header("Rendering")]
        [SerializeField]
        private Material defaultMaterial;

        [SerializeField]
        private bool generateColliders = true;

        [SerializeField]
        private bool shouldRebuildOnValidate = true;

        private TorusSurface surface;

        public TorusSurface Surface
        {
            get
            {
                surface ??= new TorusSurface(majorRadius, minorRadius, segmentCount, segmentProfiles, blendWidth);
                return surface;
            }
        }

        private void OnEnable()
        {
            Rebuild();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();
            surface = new TorusSurface(majorRadius, minorRadius, segmentCount, segmentProfiles, blendWidth);

            for (int i = 0; i < segmentCount; i++)
                CreateSegment(i);
        }

        private void CreateSegment(int segmentIndex)
        {
            var profile = surface.GetProfile(segmentIndex);
            int lengthResolution = profile ? profile.LengthResolution : 16;
            var mesh = TorusSegmentMeshBuilder.Build(surface, segmentIndex, lengthResolution, sidesResolution);
            mesh.hideFlags = HideFlags.DontSave;

            var segmentObject = new GameObject($"Segment_{segmentIndex}", typeof(MeshFilter), typeof(MeshRenderer));
            segmentObject.hideFlags = HideFlags.DontSave;
            segmentObject.transform.SetParent(transform, false);

            segmentObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            segmentObject.GetComponent<MeshRenderer>().sharedMaterial = ResolveMaterial(profile);

            if (generateColliders)
                segmentObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private Material ResolveMaterial(TorusSegmentProfile profile)
        {
            if (profile && profile.MaterialOverride)
                return profile.MaterialOverride;

            if (defaultMaterial)
                return defaultMaterial;

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline ? pipeline.defaultMaterial : null;
        }

        private void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.hideFlags != HideFlags.DontSave)
                    continue;

                if (child.TryGetComponent(out MeshFilter meshFilter))
                    DestroyObject(meshFilter.sharedMesh);

                DestroyObject(child);
            }
        }

        private void DestroyObject(Object target)
        {
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            majorRadius = Mathf.Max(majorRadius, minorRadius + 0.1f); 
            if (shouldRebuildOnValidate)
            {
                Rebuild();
            }
            surface = null;
        }

    }
}
