using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WSFGameJam2026.Player;

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

        [Tooltip("Settings of the props by mesh name: blocking radius, hiding when far, turning a class off. Empty = every prop is placed, never hidden, does not block.")]
        [SerializeField]
        private TorusPropCatalog propCatalog;

        [Header("Rendering")]
        [Tooltip("Shadow mode used when there is no shadow target (it is found automatically, so normally this is not used).")]
        [SerializeField]
        private ShadowCastingMode shadowCasting = ShadowCastingMode.On;

        [Tooltip("Only assets within the range below around this object (along the body) cast shadows, all the others cast none. Empty = the player is found automatically.")]
        [SerializeField]
        private TorusObject shadowTarget;

        [Tooltip("Range of theta around the shadow target in which assets cast shadows, in degrees (plus and minus).")]
        [SerializeField]
        [Range(1f, 180f)]
        private float nearShadowRangeDegrees = 30f;

        [SerializeField]
        private bool receiveShadows = true;

        private readonly List<Batch> batches = new List<Batch>();
        private readonly List<Placement> placements = new List<Placement>();
        private int appliedHeadVersion = -1;
        private Matrix4x4[] shadowBuffer = new Matrix4x4[0];
        private Matrix4x4[] plainBuffer = new Matrix4x4[0];
        private readonly Dictionary<Material, Material> instancedMaterials = new Dictionary<Material, Material>();
        private bool isBuilt;
        private Matrix4x4 builtWorldMatrix;

        /// <summary>Number of placed assets.</summary>
        public int InstanceCount { get; private set; }

        /// <summary>Where an asset stands on the terrain and which slots of the batches hold its matrices (one per sub-mesh).</summary>
        private class Placement
        {
            public float Theta;
            public float Phi;
            public float Height;
            public Vector3 Up;
            public Matrix4x4 BaseMatrix;
            public bool IsMoved;
            public readonly List<Slot> Slots = new List<Slot>();
        }

        private class Slot
        {
            public readonly Batch Batch;
            public int Index;

            public Slot(Batch batch, int index)
            {
                Batch = batch;
                Index = index;
            }
        }

        private class Batch
        {
            public Mesh Mesh;
            public int SubMesh;
            public Material Material;
            public readonly List<Matrix4x4> LocalMatrices = new List<Matrix4x4>();
            public readonly List<Placement> Owners = new List<Placement>();
            public float[] Thetas;
            public float VisibleRange;
            public Matrix4x4[] WorldMatrices;
        }

        private void Reset()
        {
            world = FindAnyObjectByType<TorusWorld>();
            shadowTarget = FindShadowTarget();
        }

        private void OnEnable()
        {
            if (world)
                world.Rebuilt += MarkDirty;

            if (shadowTarget == null)
                shadowTarget = FindShadowTarget();

            RenderPipelineManager.beginCameraRendering += Draw;
            MarkDirty();
        }

        private static TorusObject FindShadowTarget()
        {
            var player = FindAnyObjectByType<TorusPlayerMovement>();
            return player ? player.GetComponent<TorusObject>() : null;
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

            foreach (var batch in batches)
                SortByTheta(batch);

            world.Obstacles.Build(surface);
            UpdateWorldMatrices();
        }

        /// <summary>
        /// Sorts the instances of a batch by theta, so the ones near the shadow target form one continuous range
        /// (two when it wraps around the torus) and can be drawn with one call with shadows and the rest without.
        /// </summary>
        private static void SortByTheta(Batch batch)
        {
            int count = batch.LocalMatrices.Count;
            var order = new int[count];
            for (int i = 0; i < count; i++)
                order[i] = i;

            System.Array.Sort(order, (first, second) => batch.Owners[first].Theta.CompareTo(batch.Owners[second].Theta));

            var matrices = new List<Matrix4x4>(count);
            var owners = new List<Placement>(count);
            batch.Thetas = new float[count];
            for (int i = 0; i < count; i++)
            {
                var owner = batch.Owners[order[i]];
                matrices.Add(batch.LocalMatrices[order[i]]);
                owners.Add(owner);
                batch.Thetas[i] = owner.Theta;
                foreach (var slot in owner.Slots)
                    if (slot.Batch == batch)
                        slot.Index = i;
            }

            batch.LocalMatrices.Clear();
            batch.LocalMatrices.AddRange(matrices);
            batch.Owners.Clear();
            batch.Owners.AddRange(owners);
        }

        private void AddBiome(TorusSurface surface, int segment, GameObject model, Dictionary<(Mesh, int, Material), Batch> batchLookup)
        {
            if (TryFindTerrain(model, out var terrain) == false)
            {
                Debug.LogWarning($"Biome model '{model.name}' has no mesh named '{terrainPrefix}...', its assets are skipped.", this);
                return;
            }

            var profile = surface.GetProfile(segment) as MeshTorusSegmentProfile;
            float heightScale = profile ? profile.HeightScale : 1f;
            float heightOffset = profile ? profile.HeightOffset : 0f;
            var terrainBounds = terrain.sharedMesh.bounds;
            var toTerrain = terrain.transform.worldToLocalMatrix;

            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.gameObject == terrain.gameObject || renderer.TryGetComponent<MeshFilter>(out var filter) == false || filter.sharedMesh == null)
                    continue;

                TorusPropDefinition definition = null;
                if (propCatalog)
                    propCatalog.TryGetDefinition(filter.sharedMesh.name, out definition);

                if (definition && definition.IsDisabled)
                    continue;

                var onTerrain = toTerrain * renderer.transform.localToWorldMatrix;
                var placement = new Placement();
                placement.BaseMatrix = GetTorusMatrix(surface, segment, terrainBounds, onTerrain, heightScale, heightOffset, placement);
                float visibleRange = definition ? definition.VisibleRangeDegrees * Mathf.Deg2Rad : TorusGeometry.TwoPi;
                AddInstance(filter.sharedMesh, renderer.sharedMaterials, placement, visibleRange, batchLookup);
                placements.Add(placement);
                InstanceCount++;

                if (definition && definition.BlockRadius > 0f)
                    world.Obstacles.Add(new TorusAngles(placement.Theta, placement.Phi), definition.BlockRadius * onTerrain.lossyScale.x);
            }
        }

        /// <summary>
        /// Moves an asset from the flat terrain (matrix in the terrain's local space) onto the torus, in the local space of the world.
        /// Position: terrain X -> along the segment, terrain Z -> around the tube, terrain Y -> height (as the segment profile).
        /// Rotation: terrain up becomes the tube normal, like <see cref="TorusObject"/> uses for anything standing on the torus.
        /// </summary>
        private Matrix4x4 GetTorusMatrix(TorusSurface surface, int segment, Bounds terrainBounds, Matrix4x4 onTerrain, float heightScale, float heightOffset, Placement placement)
        {
            Vector3 local = onTerrain.GetColumn(3);
            float along = Mathf.Clamp01((local.x - terrainBounds.min.x) / Mathf.Max(terrainBounds.size.x, 1e-4f));
            float around = Mathf.Repeat((local.z - terrainBounds.min.z) / Mathf.Max(terrainBounds.size.z, 1e-4f), 1f);
            float theta = (segment + along) * surface.SegmentAngle;
            float phi = around * TorusGeometry.TwoPi;

            var up = surface.GetBaseNormal(theta, phi);
            float height = local.y * heightScale + heightOffset;
            placement.Theta = Mathf.Repeat(theta, TorusGeometry.TwoPi);
            placement.Phi = phi;
            placement.Height = height;
            placement.Up = up;
            var position = surface.GetBasePoint(theta, phi) + up * height;

            // Terrain X maps to growing theta and Z to growing phi; with the normal as up that frame is mirrored,
            // so Z points to falling phi here. Positions follow the terrain exactly, only the asset yaw is mirrored.
            var frame = Quaternion.LookRotation(-TorusGeometry.GetPhiDirection(theta, phi), up);
            return Matrix4x4.TRS(position, frame * onTerrain.rotation, onTerrain.lossyScale);
        }

        private void AddInstance(Mesh mesh, Material[] materials, Placement placement, float visibleRange, Dictionary<(Mesh, int, Material), Batch> batchLookup)
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
                    batch = new Batch { Mesh = mesh, SubMesh = subMesh, Material = material, VisibleRange = visibleRange };
                    batchLookup.Add(key, batch);
                    batches.Add(batch);
                }

                batch.LocalMatrices.Add(placement.BaseMatrix);
                batch.Owners.Add(placement);
                placement.Slots.Add(new Slot(batch, batch.LocalMatrices.Count - 1));
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

            ApplyHeadTerrain();

            float radius = (world.MajorRadius + world.MinorRadius) * 2f + 50f;
            var bounds = new Bounds(world.transform.position, Vector3.one * radius * Mathf.Max(1f, world.transform.lossyScale.x));
            foreach (var batch in batches)
                DrawBatch(batch, camera, bounds);
        }

        // Instances near the shadow target cast shadows, all the others do not: two groups of draws per batch.
        private void DrawBatch(Batch batch, Camera camera, Bounds bounds)
        {
            if (batch.VisibleRange < Mathf.PI && shadowTarget != null)
            {
                DrawLimited(batch, camera, bounds);
                return;
            }

            int count = batch.WorldMatrices.Length;
            if (shadowTarget == null)
            {
                DrawRange(CreateRenderParams(batch, camera, bounds, shadowCasting), batch, 0, count);
                return;
            }

            var shadowParams = CreateRenderParams(batch, camera, bounds, ShadowCastingMode.On);
            if (nearShadowRangeDegrees >= 180f)
            {
                DrawRange(shadowParams, batch, 0, count);
                return;
            }

            var noShadowParams = CreateRenderParams(batch, camera, bounds, ShadowCastingMode.Off);
            float range = nearShadowRangeDegrees * Mathf.Deg2Rad;
            float start = Mathf.Repeat(shadowTarget.Angles.Theta - range, TorusGeometry.TwoPi);
            float end = start + range * 2f;
            int first = FindFirstIndex(batch.Thetas, start, false);

            if (end <= TorusGeometry.TwoPi)
            {
                int last = FindFirstIndex(batch.Thetas, end, true);
                DrawRange(shadowParams, batch, first, last);
                DrawRange(noShadowParams, batch, 0, first);
                DrawRange(noShadowParams, batch, last, count);
                return;
            }

            // The range wraps around theta = 0: [first, count) and [0, last) are near.
            int wrappedLast = FindFirstIndex(batch.Thetas, end - TorusGeometry.TwoPi, true);
            DrawRange(shadowParams, batch, first, count);
            DrawRange(shadowParams, batch, 0, wrappedLast);
            DrawRange(noShadowParams, batch, wrappedLast, first);
        }

        // Props with a limited visible range (grass...): only the ones within the range of theta around the shadow target are drawn.
        // The visible ones are copied into two buffers, with and without shadows.
        private void DrawLimited(Batch batch, Camera camera, Bounds bounds)
        {
            int count = batch.WorldMatrices.Length;
            if (shadowBuffer.Length < count)
            {
                shadowBuffer = new Matrix4x4[count];
                plainBuffer = new Matrix4x4[count];
            }

            float targetTheta = shadowTarget.Angles.Theta;
            float shadowRange = nearShadowRangeDegrees * Mathf.Deg2Rad;

            int shadowCount = 0;
            int plainCount = 0;
            for (int i = 0; i < count; i++)
            {
                float distance = Mathf.Abs(Mathf.Repeat(batch.Thetas[i] - targetTheta + Mathf.PI, TorusGeometry.TwoPi) - Mathf.PI);
                if (distance > batch.VisibleRange)
                    continue;

                if (distance <= shadowRange)
                {
                    shadowBuffer[shadowCount++] = batch.WorldMatrices[i];
                    continue;
                }

                plainBuffer[plainCount++] = batch.WorldMatrices[i];
            }

            DrawBuffer(CreateRenderParams(batch, camera, bounds, ShadowCastingMode.On), batch, shadowBuffer, shadowCount);
            DrawBuffer(CreateRenderParams(batch, camera, bounds, ShadowCastingMode.Off), batch, plainBuffer, plainCount);
        }

        private static void DrawBuffer(RenderParams renderParams, Batch batch, Matrix4x4[] buffer, int count)
        {
            for (int start = 0; start < count; start += MaxInstancesPerDraw)
                Graphics.RenderMeshInstanced(renderParams, batch.Mesh, batch.SubMesh, buffer, Mathf.Min(MaxInstancesPerDraw, count - start), start);
        }

        private RenderParams CreateRenderParams(Batch batch, Camera camera, Bounds bounds, ShadowCastingMode shadowMode)
        {
            return new RenderParams(batch.Material)
            {
                camera = camera,
                layer = gameObject.layer,
                shadowCastingMode = shadowMode,
                receiveShadows = receiveShadows,
                worldBounds = bounds,
            };
        }

        private static void DrawRange(RenderParams renderParams, Batch batch, int from, int to)
        {
            for (int start = from; start < to; start += MaxInstancesPerDraw)
            {
                int count = Mathf.Min(MaxInstancesPerDraw, to - start);
                Graphics.RenderMeshInstanced(renderParams, batch.Mesh, batch.SubMesh, batch.WorldMatrices, count, start);
            }
        }

        /// <summary>First index of the sorted array whose value is greater than the given one (or not smaller, when strict is false).</summary>
        private static int FindFirstIndex(float[] sorted, float value, bool strictlyGreater)
        {
            int low = 0;
            int high = sorted.Length;
            while (low < high)
            {
                int middle = (low + high) / 2;
                bool isBefore = strictlyGreater ? sorted[middle] <= value : sorted[middle] < value;
                if (isBefore)
                {
                    low = middle + 1;
                    continue;
                }

                high = middle;
            }

            return low;
        }

        /// <summary>
        /// The head shapes the terrain near it (the tail ahead of it thins out, the terrain behind it blends into the neck), but the assets
        /// are placed on the terrain without the head. So whenever the head terrain changes, assets in the range it affects are moved along
        /// the tube normal by the change of the terrain height at their spot. In the blend between the terrain and the head they also shrink:
        /// full size on the terrain, down to nothing at the head (assets under the head itself are not drawn).
        /// Assets that left the range go back to their original place and size.
        /// </summary>
        private void ApplyHeadTerrain()
        {
            var headTerrain = world.HeadTerrain;
            if (headTerrain == null || headTerrain.Version == appliedHeadVersion)
                return;

            appliedHeadVersion = headTerrain.Version;
            headTerrain.GetAffectedRange(out float rangeStart, out float rangeLength);
            float minorRadius = world.MinorRadius;

            foreach (var placement in placements)
            {
                bool isInRange = headTerrain.IsActive && Mathf.Repeat(placement.Theta - rangeStart, TorusGeometry.TwoPi) <= rangeLength;
                if (isInRange == false && placement.IsMoved == false)
                    continue;

                var matrix = placement.BaseMatrix;
                if (isInRange)
                {
                    matrix = GetMovedMatrix(headTerrain, placement, minorRadius);
                    placement.IsMoved = true;
                }
                else
                {
                    placement.IsMoved = false;
                }

                foreach (var slot in placement.Slots)
                {
                    slot.Batch.LocalMatrices[slot.Index] = matrix;
                    slot.Batch.WorldMatrices[slot.Index] = builtWorldMatrix * matrix;
                }
            }
        }

        private static Matrix4x4 GetMovedMatrix(TorusHeadTerrain headTerrain, Placement placement, float minorRadius)
        {
            float newHeight = headTerrain.Apply(placement.Height, placement.Theta, placement.Phi, minorRadius);
            var matrix = placement.BaseMatrix;
            var position = (Vector3)matrix.GetColumn(3) + placement.Up * (newHeight - placement.Height);

            // The pivot of an asset is on the ground, so scaling the axes shrinks it towards the terrain.
            float scale = 1f - headTerrain.GetWeight(placement.Theta);
            for (int axis = 0; axis < 3; axis++)
                matrix.SetColumn(axis, matrix.GetColumn(axis) * scale);

            matrix.SetColumn(3, new Vector4(position.x, position.y, position.z, 1f));
            return matrix;
        }

        private void Clear()
        {
            if (world)
                world.Obstacles.Clear();

            placements.Clear();
            appliedHeadVersion = -1;
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
