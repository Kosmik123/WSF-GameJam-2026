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

        // The Mage built from primitives as in the concept drawing (Tools/Hero/build_mage_primitives.py), same skeleton.
        private const string MageModelPath = "Assets/_Project/Models/Hero/QuqumatzMage.fbx";
        private const string MagePrefabPath = "Assets/_Project/Prefabs/Hero/Ququmatz Mage.prefab";
        private const string MaterialPath = "Assets/_Project/Materials/Player/Ququmatz Vertex Color.mat";
        private const string SourceMaterialName = "Ququmatz Vertex Color";
        private const string ReferenceHeroPath = "Assets/RPG Tiny Hero Duo/Prefab/FemaleCharacterPolyart.prefab";
        private const string FallbackAvatarPath = "Assets/RPG Tiny Hero Duo/Animation/SwordAndShield/Idle_Battle_SwordAndShiled.fbx";
        private const string ClassesFolder = "Assets/_Project/Data/Classes";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";

        // Children of the player made by Create Player And Camera / Create Combat And Enemies as its placeholder body.
        private static readonly string[] PlaceholderNames = { "Capsule", "Cube", "Body", "Nose" };

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
            var priestImporter = ConfigureImporter(ModelPath, material);
            MatchReferenceHeight(priestImporter);
            var priest = CreatePrefab(ModelPath, PrefabPath, "Ququmatz Priest");
            SetClass("Warrior", priest, WarriorHidden);

            // The Mage gets the primitive model when it was built, otherwise the Mage variant of the priest.
            var mage = priest;
            var mageHidden = MageHidden;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MageModelPath))
            {
                // Same skeleton and units as the priest, so the same scale.
                var mageImporter = ConfigureImporter(MageModelPath, material);
                mageImporter.globalScale = priestImporter.globalScale;
                mageImporter.SaveAndReimport();
                mage = CreatePrefab(MageModelPath, MagePrefabPath, "Ququmatz Mage");
                mageHidden = new string[0];
            }

            SetClass("Mage", mage, mageHidden);
            AddPreviewToPlayer(mage, mageHidden);
            AssetDatabase.SaveAssets();
            Debug.Log("Hero setup done: " + (mage == priest ? "the quetzal priest is the model of the Mage and the Warrior." : "the primitive Mage and the quetzal priest Warrior."));
            return true;
        }

        internal static bool IsPriestPrefab(Object model)
        {
            return model && AssetDatabase.GetAssetPath(model) == PrefabPath;
        }

        /// <summary>True when the primitive Mage model exists but the Mage class does not use it yet.</summary>
        internal static bool NeedsPrimitiveMage(Object mageModel)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(MageModelPath);
            return exists && (mageModel == null || AssetDatabase.GetAssetPath(mageModel) != MagePrefabPath);
        }

        private static Material GetMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material)
            {
                HeroShaderAutoSetup.Apply(material);
                return material;
            }

            EnsureFolder(Path.GetDirectoryName(MaterialPath).Replace('\\', '/'));
            var shader = Shader.Find(HeroShaderAutoSetup.ShaderName);
            if (shader == null)
                shader = Shader.Find("WSF/Vertex Color Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");

            material = new Material(shader) { name = "Ququmatz Vertex Color" };
            if (material.HasProperty("_SrgbVertexColor"))
                material.SetFloat("_SrgbVertexColor", 1f);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // Humanoid with the bones mapped automatically: the rig is the one of RPG Tiny Hero Duo, so its clips retarget.
        private static ModelImporter ConfigureImporter(string modelPath, Material material)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.autoGenerateAvatarMappingIfUnspecified = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), SourceMaterialName), material);
            importer.SaveAndReimport();
            return importer;
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

        private static GameObject CreatePrefab(string modelPath, string prefabPath, string name)
        {
            EnsureFolder(Path.GetDirectoryName(prefabPath).Replace('\\', '/'));
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;

            // The avatar made from this model, or (when the automatic mapping failed) the avatar of the old hero's animations.
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();

            if (animator.avatar == null || animator.avatar.isValid == false || animator.avatar.isHuman == false)
            {
                animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(FallbackAvatarPath);
                Debug.LogWarning($"Hero setup: {name} has no valid humanoid avatar of its own, the avatar of the old hero is used. Check the Rig tab of {modelPath}.");
            }

            animator.applyRootMotion = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        /// <summary>True when the player prefab has no hero model to look at in the editor.</summary>
        internal static bool NeedsPreview()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (player == null || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
                return false;

            var modelRoot = player.transform.Find("Model");
            if (modelRoot && modelRoot.childCount == 0)
                return true;

            // A placeholder still visible hides the hero standing inside it.
            foreach (string name in PlaceholderNames)
            {
                var part = player.transform.Find(name);
                if (part && part.gameObject.activeSelf)
                    return true;
            }

            return false;
        }

        // The hero placed in the player prefab, so it is visible without Play. PlayerClassApplier replaces it with the model
        // of the chosen class when the game starts. The preview shows the Mage, the class the player starts with.
        private static void AddPreviewToPlayer(GameObject prefab, string[] hiddenParts)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
                return;

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            var modelRoot = root.transform.Find("Model");
            if (modelRoot)
            {
                PlacePreview(modelRoot, prefab, hiddenParts);
                SetPlaceholders(root);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        /// <summary>
        /// Gives the class applier the placeholder parts of the player (the capsule and its nose, children of the player)
        /// and hides them, because the hero stands inside the bigger capsule and would not be seen.
        /// </summary>
        internal static void SetPlaceholders(GameObject player)
        {
            var applier = player.GetComponent<PlayerClassApplier>();
            if (applier == null)
                return;

            var placeholders = new System.Collections.Generic.List<GameObject>();
            foreach (string name in PlaceholderNames)
            {
                var part = player.transform.Find(name);
                if (part)
                    placeholders.Add(part.gameObject);
            }

            var serialized = new SerializedObject(applier);
            var array = serialized.FindProperty("placeholders");
            array.arraySize = placeholders.Count;
            for (int i = 0; i < placeholders.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = placeholders[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < placeholders.Count; i++)
                placeholders[i].SetActive(false);

            if (player.TryGetComponent<MeshRenderer>(out var rootRenderer))
                rootRenderer.enabled = false;
        }

        private static void PlacePreview(Transform modelRoot, GameObject prefab, string[] hiddenParts)
        {
            for (int i = modelRoot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(modelRoot.GetChild(i).gameObject);

            var preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab, modelRoot);
            preview.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var part in preview.GetComponentsInChildren<Transform>(true))
                if (System.Array.IndexOf(hiddenParts, part.name) >= 0)
                    part.gameObject.SetActive(false);
        }

        /// <summary>
        /// The main menu (Create Main Menu) hides the children of the player while it is open and turns them on again when
        /// the game starts, the placeholder capsule too, which then covers the hero. This takes the placeholders out of that
        /// list and puts the hero's model root in. Returns true when the scene was changed.
        /// </summary>
        internal static bool FixMainMenuHiddenObjects()
        {
            var menu = Object.FindAnyObjectByType<Menu.MainMenu>(FindObjectsInactive.Include);
            var applier = Object.FindAnyObjectByType<PlayerClassApplier>();
            if (menu == null || applier == null)
                return false;

            var serializedApplier = new SerializedObject(applier);
            var modelRoot = serializedApplier.FindProperty("modelRoot").objectReferenceValue as Transform;
            var serialized = new SerializedObject(menu);
            var hidden = serialized.FindProperty("hiddenInMenu");
            if (hidden == null)
                return false;

            bool changed = false;
            bool hasModel = false;
            for (int i = hidden.arraySize - 1; i >= 0; i--)
            {
                var entry = hidden.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (entry && modelRoot && entry == modelRoot.gameObject)
                    hasModel = true;

                bool isPlaceholder = entry && entry.transform.parent == applier.transform && System.Array.IndexOf(PlaceholderNames, entry.name) >= 0;
                if (isPlaceholder)
                {
                    hidden.DeleteArrayElementAtIndex(i);
                    changed = true;
                }
            }

            if (modelRoot && hasModel == false)
            {
                hidden.arraySize++;
                hidden.GetArrayElementAtIndex(hidden.arraySize - 1).objectReferenceValue = modelRoot.gameObject;
                changed = true;
            }

            if (changed == false)
                return false;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
            Debug.Log("Hero setup: the main menu no longer turns the placeholder capsule on when the game starts; it hides and shows the hero instead.");
            return true;
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
