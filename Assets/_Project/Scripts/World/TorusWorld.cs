using System.Collections.Generic;
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
        [Min(1f)] [Delayed]
        private float majorRadius = 40f;
        public float MajorRadius => majorRadius;

        [SerializeField]
        [Min(0.5f)] [Delayed]
        private float minorRadius = 10f;
        public float MinorRadius => minorRadius;

        [SerializeField]
        [Range(3, 64)][Delayed]
        private int segmentCount = 14;
        public int SegmentCount => segmentCount;

        [Tooltip("Subdivisions around the tube. Shared by all segments so their borders match.")]
        [SerializeField]
        [Range(8, 256)][Delayed]
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

        [Header("Editor")]
        [Tooltip("Rebuilds the world after any change in the Inspector. Turn off if it gets slow.")]
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

            InvalidateProfiles();

            var meshes = new Mesh[segmentCount];
            for (int i = 0; i < segmentCount; i++)
                meshes[i] = CreateMesh(i);

            TorusNormalStitcher.Stitch(meshes, sidesResolution);

            for (int i = 0; i < segmentCount; i++)
                CreateSegment(i, meshes[i]);
        }

        private void InvalidateProfiles()
        {
            var invalidated = new HashSet<TorusSegmentProfile>();
            for (int i = 0; i < segmentCount; i++)
                if (invalidated.Add(surface.GetProfile(i)))
                    surface.GetProfile(i).Invalidate();
        }

        private Mesh CreateMesh(int segmentIndex)
        {
            TorusSegmentProfile profile = surface.GetProfile(segmentIndex);
            Mesh mesh = TorusSegmentMeshBuilder.Build(surface, segmentIndex, profile.LengthResolution, sidesResolution);
            mesh.hideFlags = HideFlags.DontSave;

            profile.Deform(mesh, surface.GetProfile(segmentIndex - 1), surface.GetProfile(segmentIndex + 1), surface.BlendWidth);
            return mesh;
        }

        private void CreateSegment(int segmentIndex, Mesh mesh)
        {
            TorusSegmentProfile profile = surface.GetProfile(segmentIndex);

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

#if UNITY_EDITOR
        // Objects can't be created inside OnValidate, so the rebuild is postponed.
        private void RebuildDelayed()
        {
            UnityEditor.EditorApplication.delayCall -= RebuildDelayed;
            if (this == null || isActiveAndEnabled == false)
                return;

            Rebuild();
        }
#endif

        private void OnDisable()
        {
            Clear();
        }

        private void OnValidate()
        {
            majorRadius = Mathf.Max(majorRadius, minorRadius + 0.1f);
            surface = null;

#if UNITY_EDITOR
            if (shouldRebuildOnValidate == false)
                return;

            UnityEditor.EditorApplication.delayCall -= RebuildDelayed;
            UnityEditor.EditorApplication.delayCall += RebuildDelayed;
#endif
        }
    }
}
