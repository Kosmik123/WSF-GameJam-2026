using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Creates a <see cref="TorusPropDefinition"/> for every class of props found in the biome models of <see cref="TorusBiomeDecorations"/>
    /// (a class = mesh name without the variant suffix), puts them into a <see cref="TorusPropCatalog"/> and connects it to the decorations.
    /// Safe to run again: it only adds the definitions that are missing, so settings you changed are kept.
    /// The first values are a guess from the size of the mesh: props lower than <see cref="SmallPropHeight"/> are hidden when far and
    /// can be walked through, taller ones block a circle of about a third of their width. Adjust them in the assets.
    /// </summary>
    public static class PropDefinitionsSetupMenu
    {
        private const string Folder = "Assets/_Project/Data/Props";
        private const string CatalogPath = Folder + "/Prop Catalog.asset";
        private const float SmallPropHeight = 1f;
        private const float DefaultHideDistance = 120f;
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

            var known = new HashSet<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var existing = list.GetArrayElementAtIndex(i).objectReferenceValue as TorusPropDefinition;
                if (existing)
                    known.Add(existing.MeshName);
            }

            int created = 0;
            foreach (var pair in meshes)
            {
                if (known.Contains(pair.Key))
                    continue;

                var definition = CreateDefinition(pair.Key, pair.Value);
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
                created++;
            }

            catalogSerialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            var decorationsSerialized = new SerializedObject(decorations);
            decorationsSerialized.FindProperty("propCatalog").objectReferenceValue = catalog;
            decorationsSerialized.ApplyModifiedProperties();
            decorations.MarkDirty();

            EditorSceneManager.MarkSceneDirty(decorations.gameObject.scene);
            Selection.activeObject = catalog;
            Debug.Log($"Prop definitions: {created} created, {meshes.Count} classes of props in total, catalog connected to the decorations.");
        }

        // Class name (mesh name without the variant suffix) -> one mesh of the class, used to guess the first values.
        private static Dictionary<string, Mesh> FindPropMeshes(TorusBiomeDecorations decorations)
        {
            var result = new Dictionary<string, Mesh>();
            var serialized = new SerializedObject(decorations);
            var models = serialized.FindProperty("biomeModels");
            var terrainPrefix = serialized.FindProperty("terrainPrefix").stringValue;

            for (int i = 0; i < models.arraySize; i++)
            {
                var model = models.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (model == null)
                    continue;

                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.name.StartsWith(terrainPrefix))
                        continue;

                    var key = TorusPropCatalog.GetKey(filter.sharedMesh.name);
                    result.TryAdd(key, filter.sharedMesh);
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

        private static TorusPropDefinition CreateDefinition(string meshName, Mesh mesh)
        {
            var path = $"{Folder}/{meshName}.asset";
            var definition = ScriptableObject.CreateInstance<TorusPropDefinition>();
            AssetDatabase.CreateAsset(definition, path);

            var size = mesh.bounds.size;
            bool isSmall = size.y < SmallPropHeight;
            float radius = isSmall ? 0f : Mathf.Min(MaxBlockRadius, Mathf.Max(size.x, size.z) * 0.35f);

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("meshName").stringValue = meshName;
            serialized.FindProperty("blockRadius").floatValue = radius;
            serialized.FindProperty("hideWhenFar").boolValue = isSmall;
            serialized.FindProperty("hideDistance").floatValue = DefaultHideDistance;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            return definition;
        }
    }
}
