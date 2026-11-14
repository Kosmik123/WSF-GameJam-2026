using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Puts the assets of the biome models (FBX from Assets/Bioms: one terrain mesh plus the scattered assets) on the torus.
    /// Segment i uses model i % Length. The terrain mesh of the model (its name starts with the terrain prefix) gives the mapping:
    /// its X runs along the segment and its Z around the tube, exactly like in <see cref="MeshTorusSegmentProfile"/>,
    /// so every asset lands where it stood on the flat terrain. Assets are drawn with GPU instancing
    /// (one draw per mesh, sub-mesh and material), without GameObjects, so thousands of them stay cheap.
    /// </summary>
    [ExecuteAlways]
    public class TorusBiomeDecorations : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;

        [SerializeField]
        private TorusWorld world;

        [Tooltip("Biome models (FBX from Assets/Bioms). Segment i uses model i % Length. Use the same order as the world's segment profiles.")]
        [SerializeField]
        private GameObject[] biomeModels;

        [Tooltip("Name prefix of the terrain mesh object inside a biome model. Everything else in the model is an asset.")]
        [SerializeField]
        private string terrainPrefix = "Teren_";

        [Header("Rendering")]
        [SerializeField]
        private ShadowCastingMode shadowCasting = ShadowCastingMode.On;

        [SerializeField]
        private bool receiveShadows = true;

        private readonly List<Batch> batches = new List<Batch>();
        private readonly Dictionary<Material, Material> instancedMaterials = new Dictionary<Material, Material>();
        private bool isBuilt;
        private Matrix4x4 builtWorldMatrix;

        /// <summary>Number of placed assets.</summary>
        public int InstanceCount { get; private set; }

        private class Batch
        {
            public Mesh Mesh;
            public int SubMesh;
            public Material Material;
            public readonly List<Matrix4x4> LocalMatrices = new List<Matrix4x4>();
            public Matrix4x4[] WorldMatrices;
        }

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
        }

        private void OnEnable()
        {
            if (world)
                world.Rebuilt += MarkDirty;

            RenderPipelineManager.beginCameraRendering += Draw;
            MarkDirty();
        }

        private void OnDisable()
        {
            if (world)
                world.Rebuilt -= MarkDirty;

            RenderPipelineManager.beginCameraRendering -= Draw;
            Clear();
        }

        private void OnValidate()
        {
            MarkDirty();
        }

        /// <summary>Places the assets again on the next frame (after the world or the models changed).</summary>
        public void MarkDirty()
        {
            isBuilt = false;
        }

        [ContextMenu("Rebuild")]
        public void Rebuild()
        {
            Clear();
            isBuilt = true;
            if (world == null || biomeModels == null || biomeModels.Length == 0)
                return;

            var surface = world.Surface;
            var batchLookup = new Dictionary<(Mesh, int, Material), Batch>();
            for (int segment = 0; segment < surface.SegmentCount; segment++)
            {
                var model = biomeModels[segment % biomeModels.Length];
                if (model)
                    AddBiome(surface, segment, model, batchLookup);
            }

            UpdateWorldMatrices();
        }

        private void AddBiome(TorusSurface surface, int segment, GameObject model, Dictionary<(Mesh, int, Material), Batch> batchLookup)
        {
            if (TryFindTerrain(model, out var terrain) == false)
            {
                Debug.LogWarning($"Biome model '{model.name}' has no mesh named '{terrainPrefix}...', its assets are skipped.", this);
                return;
            }

            var own = surface.GetProfile(segment);
            var previous = surface.GetProfile(segment - 1);
            var next = surface.GetProfile(segment + 1);
            float blendWidth = surface.BlendWidth;
            var terrainBounds = terrain.sharedMesh.bounds;
            var toTerrain = terrain.transform.worldToLocalMatrix;

            int index = 0;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject == terrain.gameObject || renderer.TryGetComponent<MeshFilter>(out var filter) == false || filter.sharedMesh == null)
                    continue;

                index++;
                var onTerrain = toTerrain * renderer.transform.localToWorldMatrix;
                Vector3 local = onTerrain.GetColumn(3);
                float along = Mathf.Clamp01((local.x - terrainBounds.min.x) / Mathf.Max(terrainBounds.size.x, 1e-4f));
                float around = Mathf.Repeat((local.z - terrainBounds.min.z) / Mathf.Max(terrainBounds.size.z, 1e-4f), 1f);

                // Near a border the biomes overlap: an asset stays with the weight of its biome there,
                // and appears mirrored inside the neighbour with the (lower) weight its biome has on that side.
                bool isEndBorder = along > 0.5f;
                float distance = isEndBorder ? 1f - along : along;
                var neighbour = isEndBorder ? next : previous;
                float ownWeight = blendWidth > 0f ? TorusSegmentBlend.GetOwnWeight(own, neighbour, distance / blendWidth, around, isEndBorder) : 1f;
                if (Hash01(segment, index, 0) < ownWeight)
                    AddPlaced(surface, segment + along, around, onTerrain, filter.sharedMesh, renderer.sharedMaterials, batchLookup);

                if (blendWidth <= 0f || neighbour == null || neighbour.ClampsAlong == false)
                    continue;

                float mirroredWeight = TorusSegmentBlend.GetOwnWeight(own, neighbour, -distance / blendWidth, around, isEndBorder);
                if (mirroredWeight > 0.001f && Hash01(segment, index, 1) < mirroredWeight)
                {
                    float mirroredSegmentPosition = isEndBorder ? segment + 1f + distance : segment - distance;
                    AddPlaced(surface, mirroredSegmentPosition, around, onTerrain, filter.sharedMesh, renderer.sharedMaterials, batchLookup);
                }
            }
        }

        private void AddPlaced(TorusSurface surface, float segmentPosition, float around, Matrix4x4 onTerrain, Mesh mesh, Material[] materials, Dictionary<(Mesh, int, Material), Batch> batchLookup)
        {
            AddInstance(mesh, materials, GetTorusMatrix(surface, segmentPosition, around, onTerrain), batchLookup);
            InstanceCount++;
        }

        /// <summary>
        /// Moves an asset from the flat terrain (matrix in the terrain's local space) onto the torus, in the local space of the world.
        /// Position: terrain X -> along the segment, terrain Z -> around the tube, height = the blended terrain there.
        /// Rotation: terrain up becomes the tube normal, like <see cref="TorusObject"/> uses for anything standing on the torus.
        /// </summary>
        /// <param name="segmentPosition">Segment index plus the position inside it (0-1), may point into a neighbouring segment.</param>
        private Matrix4x4 GetTorusMatrix(TorusSurface surface, float segmentPosition, float around, Matrix4x4 onTerrain)
        {
            float theta = segmentPosition * surface.SegmentAngle;
            float phi = around * TorusGeometry.TwoPi;
            var up = surface.GetBaseNormal(theta, phi);
            var position = surface.GetBasePoint(theta, phi) + up * GetTerrainHeight(surface, segmentPosition, around, theta, phi);

            // Terrain X maps to growing theta and Z to growing phi; with the normal as up that frame is mirrored,
            // so Z points to falling phi here. Positions follow the terrain exactly, only the asset yaw is mirrored.
            var frame = Quaternion.LookRotation(-TorusGeometry.GetPhiDirection(theta, phi), up);
            return Matrix4x4.TRS(position, frame * onTerrain.rotation, onTerrain.lossyScale);
        }

        /// <summary>Terrain height of the segment profiles (with border blending, without the head's influence).</summary>
        private float GetTerrainHeight(TorusSurface surface, float segmentPosition, float around, float theta, float phi)
        {
            int segment = Mathf.FloorToInt(segmentPosition);
            float along = segmentPosition - segment;
            return TorusSegmentBlend.GetHeight(surface.GetProfile(segment), surface.GetProfile(segment - 1), surface.GetProfile(segment + 1),
                along, around, surface.BlendWidth, surface.GetBasePoint(theta, phi));
        }

        /// <summary>Deterministic random 0-1 per asset, so the blend looks the same after every rebuild.</summary>
        private static float Hash01(int segment, int index, int salt)
        {
            unchecked
            {
                uint hash = (uint)(segment * 73856093) ^ (uint)(index * 19349663) ^ (uint)(salt * 83492791);
                hash ^= hash >> 15;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                hash *= 3266489917u;
                hash ^= hash >> 16;
                return (hash & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private void AddInstance(Mesh mesh, Material[] materials, Matrix4x4 matrix, Dictionary<(Mesh, int, Material), Batch> batchLookup)
        {
            if (materials == null || materials.Length == 0)
                return;

            for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                var material = GetInstancedMaterial(materials[Mathf.Min(subMesh, materials.Length - 1)]);
                if (material == null)
                    continue;

                var key = (mesh, subMesh, material);
                if (batchLookup.TryGetValue(key, out var batch) == false)
                {
                    batch = new Batch { Mesh = mesh, SubMesh = subMesh, Material = material };
                    batchLookup.Add(key, batch);
                    batches.Add(batch);
                }

                batch.LocalMatrices.Add(matrix);
            }
        }

        private Material GetInstancedMaterial(Material material)
        {
            if (material == null || material.enableInstancing)
                return material;

            if (instancedMaterials.TryGetValue(material, out var instanced) == false)
            {
                instanced = new Material(material) { enableInstancing = true, hideFlags = HideFlags.DontSave };
                instancedMaterials.Add(material, instanced);
            }

            return instanced;
        }

        private bool TryFindTerrain(GameObject model, out MeshFilter terrain)
        {
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh && filter.name.StartsWith(terrainPrefix))
                {
                    terrain = filter;
                    return true;
                }
            }

            terrain = null;
            return false;
        }

        private void UpdateWorldMatrices()
        {
            builtWorldMatrix = world.transform.localToWorldMatrix;
            foreach (var batch in batches)
            {
                batch.WorldMatrices = new Matrix4x4[batch.LocalMatrices.Count];
                for (int i = 0; i < batch.WorldMatrices.Length; i++)
                    batch.WorldMatrices[i] = builtWorldMatrix * batch.LocalMatrices[i];
            }
        }

        private void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (world == null || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
                return;

            if (isBuilt == false)
                Rebuild();

            if (world.transform.localToWorldMatrix != builtWorldMatrix)
                UpdateWorldMatrices();

            float radius = (world.MajorRadius + world.MinorRadius) * 2f + 50f;
            var bounds = new Bounds(world.transform.position, Vector3.one * radius * Mathf.Max(1f, world.transform.lossyScale.x));
            foreach (var batch in batches)
            {
                var renderParams = new RenderParams(batch.Material)
                {
                    camera = camera,
                    layer = gameObject.layer,
                    shadowCastingMode = shadowCasting,
                    receiveShadows = receiveShadows,
                    worldBounds = bounds,
                };

                for (int start = 0; start < batch.WorldMatrices.Length; start += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, batch.WorldMatrices.Length - start);
                    Graphics.RenderMeshInstanced(renderParams, batch.Mesh, batch.SubMesh, batch.WorldMatrices, count, start);
                }
            }
        }

        private void Clear()
        {
            batches.Clear();
            InstanceCount = 0;
            foreach (var instanced in instancedMaterials.Values)
            {
                if (Application.isPlaying)
                    Destroy(instanced);
                else
                    DestroyImmediate(instanced);
            }

            instancedMaterials.Clear();
        }
    }
}
