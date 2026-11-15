using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Player;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Puts the hero "Kaplan Pierzastego Weza" (Q'uq'umatz priest, built by Tools/Hero/build_quqmatz_priest.py) into the game:
    /// imports the FBX as a humanoid (so the RPG Tiny Hero Duo animations play on it), gives it the vertex color material,
    /// makes the prefab, its size like the old hero, and sets it as the model of both classes. Each class hides the parts
    /// of the other one (female body, staff, long plume and cape for the Mage; male body, spear, shield, short plume and cape
    /// for the Warrior). Safe to run again.
    /// </summary>
    public static class HeroSetupMenu
    {
        private const string ModelPath = "Assets/_Project/Models/Hero/QuqumatzPriest.fbx";
        private const string PrefabPath = "Assets/_Project/Prefabs/Hero/Ququmatz Priest.prefab";
        private const string MaterialPath = "Assets/_Project/Materials/Player/Ququmatz Vertex Color.mat";
        private const string SourceMaterialName = "Ququmatz Vertex Color";
        private const string ReferenceHeroPath = "Assets/RPG Tiny Hero Duo/Prefab/FemaleCharacterPolyart.prefab";
        private const string FallbackAvatarPath = "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx";
        private const string ClassesFolder = "Assets/_Project/Data/Classes";

        private static readonly string[] MageHidden =
        {
            "Body05", "Head01_Male", "Eye01", "Mouth01",
            "Warrior_Plume", "Warrior_Cape", "Warrior_Spear", "Warrior_Shield", "Warrior_CheekGuards",
        };

        private static readonly string[] WarriorHidden =
        {
            "Body10", "Head02_Female", "Eye02", "Mouth02",
            "Priest_Plume", "Priest_Cape", "Priest_Staff",
        };

        [MenuItem("WSF GameJam/Setup/Apply Quetzal Priest Hero")]
        private static void ApplyFromMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            {
                EditorUtility.DisplayDialog("Hero setup", $"There is no {ModelPath}. Build it with Tools/Hero/build_quqmatz_priest.py.", "OK");
                return;
            }

            ApplyPriest();
        }

        /// <summary>Returns false when the model does not exist yet.</summary>
        internal static bool ApplyPriest()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
                return false;

            var material = GetMaterial();
            ConfigureImporter(material);
            var prefab = CreatePrefab();
            SetClass("Mage", prefab, MageHidden);
            SetClass("Warrior", prefab, WarriorHidden);
            AssetDatabase.SaveAssets();
            Debug.Log("Hero setup done: the quetzal priest is the model of the Mage and the Warrior.");
            return true;
        }

        internal static bool IsPriestPrefab(Object model)
        {
            return model && AssetDatabase.GetAssetPath(model) == PrefabPath;
        }

        private static Material GetMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material)
                return material;

            EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            var shader = Shader.Find("WSF/Vertex Color Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");

            material = new Material(shader) { name = "Ququmatz Vertex Color" };
            if (material.HasProperty("_SrgbVertexColor"))
                material.SetFloat("_SrgbVertexColor", 1f);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // Humanoid with the bones mapped automatically: the rig is the one of RPG Tiny Hero Duo, so its clips retarget.
        private static void ConfigureImporter(Material material)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), SourceMaterialName), material);
            importer.SaveAndReimport();
            MatchReferenceHeight(importer);
        }

        // The size of the new hero follows the old one, whatever unit the FBX was written in.
        private static void MatchReferenceHeight(ModelImporter importer)
        {
            float target = MeasureHeight(AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceHeroPath));
            float current = MeasureHeight(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));
            if (target <= 0f || current <= 0f)
                return;

            float ratio = target / current;
            if (Mathf.Abs(ratio - 1f) < 0.05f)
                return;

            importer.globalScale *= ratio;
            importer.SaveAndReimport();
            Debug.Log($"Hero setup: scale of the priest FBX changed by {ratio:0.###} to match the height of the old hero.");
        }

        // Height of the female body only, which both models share (the plume and weapons would change the bounds).
        private static float MeasureHeight(GameObject asset)
        {
            if (asset == null)
                return 0f;

            var instance = Object.Instantiate(asset);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            float height = 0f;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "Body10")
                    height = renderer.bounds.size.y;
            }

            Object.DestroyImmediate(instance);
            return height;
        }

        private static GameObject CreatePrefab()
        {
            EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Ququmatz Priest";

            // The avatar made from this model, or (when the automatic mapping failed) the avatar of the old hero's animations.
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();

            if (animator.avatar == null || animator.avatar.isValid == false || animator.avatar.isHuman == false)
            {
                animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(FallbackAvatarPath);
                Debug.LogWarning("Hero setup: the priest has no valid humanoid avatar of its own, the avatar of the old hero is used. Check the Rig tab of QuqumatzPriest.fbx.");
            }

            animator.applyRootMotion = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static void SetClass(string className, GameObject prefab, string[] hidden)
        {
            var playerClass = AssetDatabase.LoadAssetAtPath<PlayerClass>($"{ClassesFolder}/{className}.asset");
            if (playerClass == null)
                return;

            var serialized = new SerializedObject(playerClass);
            serialized.FindProperty("modelPrefab").objectReferenceValue = prefab;
            serialized.FindProperty("modelMaterial").objectReferenceValue = null;
            var parts = serialized.FindProperty("hiddenParts");
            parts.arraySize = hidden.Length;
            for (int i = 0; i < hidden.Length; i++)
                parts.GetArrayElementAtIndex(i).stringValue = hidden[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
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
