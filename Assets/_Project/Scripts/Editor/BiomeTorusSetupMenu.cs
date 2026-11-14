using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Puts the biomes exported from Blender (Assets/Bioms/&lt;Biome&gt;.fbx, see docs/PSG.md) on the torus of the open scene:
    /// 1. the FBX files get Read/Write (needed by the terrain profiles) and their Blender materials are remapped to vertex color materials,
    /// 2. every biome gets a Mesh Torus Segment Profile made from its terrain mesh,
    /// 3. the world gets one segment per biome (a biome is one 314 m segment of the 300 m torus) and the profiles,
    /// 4. the world gets <see cref="TorusBiomeDecorations"/>, which draws the biome assets on the torus.
    /// Safe to run again: existing assets and components are updated.
    /// </summary>
    public static class BiomeTorusSetupMenu
    {
        private const string BiomeFolder = "Assets/Bioms";
        private const string ProfileFolder = "Assets/_Project/Terrain Profiles/Biomes";
        private const string MaterialFolder = "Assets/_Project/Materials/Biomes";
        private const string ShaderName = "WSF/Vertex Color Lit";
        private const string TerrainPrefix = "Teren_";

        // Order around the torus, the same as in Blender (Tools/Blender/psg_examples/build_biomes.py).
        private static readonly string[] biomes = { "Pustynia", "Las", "Bagno", "Wybrzeze", "Wulkan", "Lod" };

        // Biome terrain: 314 x 354 m with a 2 m mesh spacing -> 158 x 178 vertex columns and rows.
        private const int GridAlong = 158;
        private const int GridAround = 178;
        private const int SegmentLengthResolution = 160;
        private const int SidesResolution = 180;

        // Edge Level of the biomes in Blender: their flat border becomes the base tube.
        private const float HeightOffset = -0.5f;

        private const string GameScenePath = "Assets/_Project/Scenes/Game/Game.unity";

        [MenuItem("WSF GameJam/Setup/Biomes On Torus")]
        private static void SetupBiomes()
        {
            if (TrySetupBiomes(out var world, out string error) == false)
            {
                EditorUtility.DisplayDialog("Biomes on torus", error, "OK");
                return;
            }

            Selection.activeObject = world.gameObject;
        }

        /// <summary>
        /// Batch mode entry: opens the game scene, runs the setup, saves it and renders preview images.
        /// Unity.exe -batchmode -projectPath &lt;project&gt; -executeMethod WSFGameJam2026.Editor.BiomeTorusSetupMenu.SetupAndRenderFromCommandLine -previewOutput &lt;folder&gt; -quit
        /// </summary>
        public static void SetupAndRenderFromCommandLine()
        {
            EditorSceneManager.OpenScene(GameScenePath);
            if (TrySetupBiomes(out var world, out string error) == false)
            {
                Debug.LogError("Biomes on torus: " + error);
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.SaveScene(world.gameObject.scene);
            AssetDatabase.SaveAssets();

            string output = GetCommandLineValue("-previewOutput");
            if (string.IsNullOrEmpty(output) == false)
                BiomeTorusPreview.Render(world, output);
        }

        private static string GetCommandLineValue(string name)
        {
            string[] arguments = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (arguments[i] == name)
                    return arguments[i + 1];
            }

            return null;
        }

        private static bool TrySetupBiomes(out TorusWorld world, out string error)
        {
            error = null;
            world = Object.FindAnyObjectByType<TorusWorld>();
            if (world == null)
            {
                error = "Open the game scene: it needs a Torus World.";
                return false;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                error = $"Shader '{ShaderName}' not found (Assets/_Project/Shaders/VertexColorLit.shader).";
                return false;
            }

            EnsureFolder(ProfileFolder);
            EnsureFolder(MaterialFolder);
            var assetMaterial = GetOrCreateMaterial("Biome Vertex Color", shader, 0f, 0f);
            var glowMaterial = GetOrCreateMaterial("Biome Vertex Color Glow", shader, 2f, 0f);
            // One terrain material for every biome: lava glows also where it reaches into a neighbour (border blend).
            var terrainMaterial = GetOrCreateMaterial("Biome Terrain", shader, 0f, 2f);

            var profiles = new List<TorusSegmentProfile>();
            foreach (string biome in biomes)
            {
                string modelPath = $"{BiomeFolder}/{biome}.fbx";
                if (File.Exists(modelPath) == false)
                {
                    Debug.LogWarning($"Biome '{biome}' skipped: {modelPath} not found. Export it from Blender (docs/PSG.md).");
                    continue;
                }

                ConfigureImporter(modelPath, assetMaterial, glowMaterial);
                var terrainMesh = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().FirstOrDefault(mesh => mesh.name.StartsWith(TerrainPrefix));
                if (terrainMesh == null)
                {
                    Debug.LogWarning($"Biome '{biome}' skipped: no mesh named '{TerrainPrefix}...' in {modelPath}.");
                    continue;
                }

                profiles.Add(GetOrCreateProfile(biome, terrainMesh, AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), terrainMaterial));
            }

            if (profiles.Count == 0)
            {
                error = $"No biome FBX found in {BiomeFolder}.";
                return false;
            }

            ConfigureWorld(world, profiles);
            ConfigureDecorations(world);

            world.Rebuild();
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            Debug.Log($"Biomes on torus: {profiles.Count} biomes, one segment each. Save the scene to keep it.");
            return true;
        }

        private static void ConfigureImporter(string modelPath, Material assetMaterial, Material glowMaterial)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.isReadable = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importAnimation = false;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "LD57 Stylized"), assetMaterial);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "LD57 Stylized Glow"), glowMaterial);
            importer.SaveAndReimport();
        }

        private static Material GetOrCreateMaterial(string name, Shader shader, float emission, float warmGlow)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.enableInstancing = true;
            material.SetFloat("_Emission", emission);
            material.SetFloat("_WarmGlow", warmGlow);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static TorusSegmentProfile GetOrCreateProfile(string biome, Mesh terrainMesh, GameObject decorationModel, Material material)
        {
            string path = $"{ProfileFolder}/Biome {biome}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<MeshTorusSegmentProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MeshTorusSegmentProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            var serialized = new SerializedObject(profile);
            serialized.FindProperty("terrainMesh").objectReferenceValue = terrainMesh;
            serialized.FindProperty("decorationModel").objectReferenceValue = decorationModel;
            serialized.FindProperty("gridResolutionAlong").intValue = GridAlong;
            serialized.FindProperty("gridResolutionAround").intValue = GridAround;
            serialized.FindProperty("heightScale").floatValue = 1f;
            serialized.FindProperty("heightOffset").floatValue = HeightOffset;
            serialized.FindProperty("lengthResolution").intValue = SegmentLengthResolution;
            serialized.FindProperty("materialOverride").objectReferenceValue = material;
            serialized.FindProperty("aroundTiling").intValue = 1;
            serialized.FindProperty("seamBlendWidth").floatValue = 0f; // biome borders are already flat
            serialized.ApplyModifiedPropertiesWithoutUndo();
            profile.Invalidate();
            return profile;
        }

        private static void ConfigureWorld(TorusWorld world, List<TorusSegmentProfile> profiles)
        {
            var serialized = new SerializedObject(world);
            int previousCount = serialized.FindProperty("segmentCount").intValue;
            int previousSides = serialized.FindProperty("sidesResolution").intValue;

            serialized.FindProperty("segmentCount").intValue = profiles.Count;
            serialized.FindProperty("sidesResolution").intValue = SidesResolution;
            var profileArray = serialized.FindProperty("segmentProfiles");
            profileArray.arraySize = profiles.Count;
            for (int i = 0; i < profiles.Count; i++)
                profileArray.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];

            serialized.ApplyModifiedProperties();
            Debug.Log($"Torus World: segments {previousCount} -> {profiles.Count}, sides resolution {previousSides} -> {SidesResolution}.");
        }

        private static void ConfigureDecorations(TorusWorld world)
        {
            if (world.TryGetComponent<TorusBiomeDecorations>(out var decorations) == false)
                decorations = Undo.AddComponent<TorusBiomeDecorations>(world.gameObject);

            var serialized = new SerializedObject(decorations);
            serialized.FindProperty("world").objectReferenceValue = world;
            serialized.FindProperty("terrainPrefix").stringValue = TerrainPrefix;

            serialized.ApplyModifiedProperties();
            decorations.MarkDirty();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
