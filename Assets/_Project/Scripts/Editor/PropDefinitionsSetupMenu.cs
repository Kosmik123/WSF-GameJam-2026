using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Creates a <see cref="TorusPropDefinition"/> for every mesh of the props found in the biome models of <see cref="TorusBiomeDecorations"/>,
    /// puts them into a <see cref="TorusPropCatalog"/> and connects it to the decorations. The definition asset is named like the mesh.
    /// Safe to run again: definitions that exist are kept with their settings (an old definition without the mesh reference gets it),
    /// only the missing ones are created. The first values are a guess from the size of the mesh: props lower than
    /// <see cref="SmallPropHeight"/> are visible only near the player (<see cref="SmallPropVisibleRange"/>) and can be walked through,
    /// taller ones block a circle of about a third of their width. Adjust them in the assets.
    /// </summary>
    public static class PropDefinitionsSetupMenu
    {
        private const string Folder = "Assets/_Project/Data/Props";
        private const string CatalogPath = Folder + "/Prop Catalog.asset";
        private const float SmallPropHeight = 1f;
        private const float SmallPropVisibleRange = 25f;
        private const float MaxBlockRadius = 3f;

        [MenuItem("WSF GameJam/Setup/Create Prop Definitions")]
        private static void CreateDefinitions()
        {
            var decorations = Object.FindAnyObjectByType<TorusBiomeDecorations>();
            if (decorations == null)
            {
                EditorUtility.DisplayDialog("Prop definitions", "The scene needs the TorusBiomeDecorations (WSF GameJam/Setup/Biomes On Torus).", "OK");
                return;
            }

            var meshes = FindPropMeshes(decorations);
            if (meshes.Count == 0)
            {
                EditorUtility.DisplayDialog("Prop definitions", "No props found. Check the biome models of the decorations.", "OK");
                return;
            }

            Directory.CreateDirectory(Folder);
            var catalog = GetOrCreateCatalog();
            var catalogSerialized = new SerializedObject(catalog);
            var list = catalogSerialized.FindProperty("definitions");

            var inCatalog = new HashSet<Object>();
            for (int i = 0; i < list.arraySize; i++)
                inCatalog.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

            int created = 0;
            foreach (var mesh in meshes)
            {
                var definition = GetOrCreateDefinition(mesh, out bool isNew);
                created += isNew ? 1 : 0;
                if (inCatalog.Add(definition) == false)
                    continue;

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
            }

            catalogSerialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var decorationsSerialized = new SerializedObject(decorations);
            decorationsSerialized.FindProperty("propCatalog").objectReferenceValue = catalog;
            decorationsSerialized.ApplyModifiedProperties();
            decorations.MarkDirty();

            EditorSceneManager.MarkSceneDirty(decorations.gameObject.scene);
            Selection.activeObject = catalog;
            Debug.Log($"Prop definitions: {created} created, {meshes.Count} prop meshes in total, catalog connected to the decorations.");
        }

        private static List<Mesh> FindPropMeshes(TorusBiomeDecorations decorations)
        {
            var result = new List<Mesh>();
            var serialized = new SerializedObject(decorations);
            var terrainPrefix = serialized.FindProperty("terrainPrefix").stringValue;
            var world = serialized.FindProperty("world").objectReferenceValue as TorusWorld;
            if (world == null)
                return result;

            var surface = world.Surface;
            var models = new List<GameObject>();
            for (int segment = 0; segment < surface.SegmentCount; segment++)
            {
                var profile = surface.GetProfile(segment) as MeshTorusSegmentProfile;
                if (profile && profile.DecorationModel && models.Contains(profile.DecorationModel) == false)
                    models.Add(profile.DecorationModel);
            }

            foreach (var model in models)
            {
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh && filter.name.StartsWith(terrainPrefix) == false && result.Contains(filter.sharedMesh) == false)
                        result.Add(filter.sharedMesh);
                }
            }

            return result;
        }

        private static TorusPropCatalog GetOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<TorusPropCatalog>(CatalogPath);
            if (catalog)
                return catalog;

            catalog = ScriptableObject.CreateInstance<TorusPropCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            return catalog;
        }

        private static TorusPropDefinition GetOrCreateDefinition(Mesh mesh, out bool isNew)
        {
            var path = $"{Folder}/{GetFileName(mesh.name)}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<TorusPropDefinition>(path);
            isNew = definition == null;
            if (isNew)
            {
                definition = ScriptableObject.CreateInstance<TorusPropDefinition>();
                AssetDatabase.CreateAsset(definition, path);
                SetFirstValues(definition, mesh);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("mesh").objectReferenceValue = mesh;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void SetFirstValues(TorusPropDefinition definition, Mesh mesh)
        {
            var size = mesh.bounds.size;
            bool isSmall = size.y < SmallPropHeight;
            float radius = isSmall ? 0f : Mathf.Min(MaxBlockRadius, Mathf.Max(size.x, size.z) * 0.35f);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("blockRadius").floatValue = radius;
            serialized.FindProperty("visibleRangeDegrees").floatValue = isSmall ? SmallPropVisibleRange : 180f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GetFileName(string meshName)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
                meshName = meshName.Replace(invalid, '_');

            return meshName;
        }
    }
}
