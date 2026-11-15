using System.Collections.Generic;
using System.Data;
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

        [Tooltip("Material of the water surface of segments whose profile has water.")]
        [SerializeField]
        private Material waterMaterial;

        [Tooltip("Colliders are built from the terrain without the head blend, so they do not follow the terrain near the head.")]
        [SerializeField]
        private bool generateColliders = true;

        [Header("Editor")]
        [Tooltip("Rebuilds the world after any change in the Inspector. Turn off if it gets slow.")]
        [SerializeField]
        private bool shouldRebuildOnValidate = true;

        private TorusSurface surface;
        private TorusHeadMeshBlender headMeshBlender;
        private TorusWaterSurfaces waterSurfaces;

        /// <summary>Called after the segments were built again (shape, profiles or terrain changed).</summary>
        public event System.Action Rebuilt;

        /// <summary>Terrain shaped by the snake's head. It belongs to the world, so it survives rebuilds.</summary>
        public TorusHeadTerrain HeadTerrain { get; } = new TorusHeadTerrain();

        /// <summary>The pseudo colliders of the props. Filled by <see cref="TorusBiomeDecorations"/>, used when objects move on the torus.</summary>
        public TorusPropObstacles Obstacles { get; } = new TorusPropObstacles();

        /// <summary>The wall at the theta of the head. Set by <see cref="WSFGameJam2026.Head.HeadBarrier"/>, stops the objects that are blocked by it.</summary>
        public TorusThetaBarrier HeadBarrier { get; } = new TorusThetaBarrier();

        public TorusSurface Surface
        {
            get
            {
                surface ??= new TorusSurface(majorRadius, minorRadius, segmentCount, segmentProfiles, blendWidth, HeadTerrain);
                return surface;
            }
        }

        private void OnEnable()
        {
            majorRadius = Mathf.Max(majorRadius, minorRadius + 0.1f);
            Rebuild();
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();
            surface = new TorusSurface(majorRadius, minorRadius, segmentCount, segmentProfiles, blendWidth, HeadTerrain);

            InvalidateProfiles();

            var meshes = new Mesh[segmentCount];
            for (int i = 0; i < segmentCount; i++)
                meshes[i] = CreateMesh(i);

            TorusNormalStitcher.Stitch(meshes, sidesResolution);

            for (int i = 0; i < segmentCount; i++)
                CreateSegment(i, meshes[i]);

            headMeshBlender = new TorusHeadMeshBlender(surface, meshes, sidesResolution, HeadTerrain);
            headMeshBlender.Update();

            waterSurfaces = new TorusWaterSurfaces(surface, HeadTerrain);
            waterSurfaces.Build(transform, waterMaterial, sidesResolution);
            Rebuilt?.Invoke();
        }

        private void LateUpdate()
        {
            headMeshBlender?.Update();
            waterSurfaces?.Update();
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
            var profile = surface.GetProfile(segmentIndex);
            var mesh = TorusSegmentMeshBuilder.Build(surface, segmentIndex, profile.LengthResolution, sidesResolution);
            mesh.hideFlags = HideFlags.DontSave;

            profile.Deform(mesh, surface.GetProfile(segmentIndex - 1), surface.GetProfile(segmentIndex + 1), surface.BlendWidth);
            return mesh;
        }

        private void CreateSegment(int segmentIndex, Mesh mesh)
        {
            var profile = surface.GetProfile(segmentIndex);

            var segmentObject = new GameObject($"Segment_{segmentIndex}", typeof(MeshFilter), typeof(MeshRenderer));
            segmentObject.hideFlags = HideFlags.DontSave;
            segmentObject.transform.SetParent(transform, false);

            segmentObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = segmentObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ResolveMaterial(profile);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            if (generateColliders)
                segmentObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private Material ResolveMaterial(TorusSegmentProfile profile)
        {
            if (profile && profile.MaterialOverride)
                return profile.MaterialOverride;

            if (defaultMaterial)
                return defaultMaterial;

            var pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline ? pipeline.defaultMaterial : null;
        }

        private void Clear()
        {
            headMeshBlender = null;
            waterSurfaces = null;

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (child.hideFlags != HideFlags.DontSave)
                    continue;

                if (child.TryGetComponent<MeshFilter>(out var meshFilter))
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
            //majorRadius = Mathf.Max(majorRadius, minorRadius + 0.1f);
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
