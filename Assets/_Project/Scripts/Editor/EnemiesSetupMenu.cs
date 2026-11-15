using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Enemies;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Creates the new enemies in the style of the LD57 enemies (low-poly primitives, LD57 look of materials, procedural
    /// <see cref="EnemyVisualAnimator"/>): the golem (armored), bats (swarm) and the shaman (support caster), and adds them
    /// to the <see cref="EnemySpawner"/> of the scene. Safe to run again: the prefabs are rebuilt, spawner entries are not duplicated.
    /// </summary>
    public static class EnemiesSetupMenu
    {
        private const string MaterialsFolder = "Assets/_Project/Materials/Enemies/LD57";

        [MenuItem("WSF GameJam/Setup/Create Golem Bats Shaman")]
        internal static void CreateEnemies()
        {
            var golem = CreateGolem();
            var bat = CreateBat();
            var shaman = CreateShaman();

            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner)
            {
                var serialized = new SerializedObject(spawner);
                AddEntry(serialized, golem, 1f, 0.35f, new Vector2Int(1, 1));
                AddEntry(serialized, bat, 1.5f, 0.1f, new Vector2Int(4, 6));
                AddEntry(serialized, shaman, 0.8f, 0.25f, new Vector2Int(1, 1));

                // Bats come in groups, so the spawner needs room for more enemies at once.
                var maxAlive = serialized.FindProperty("maxAlive");
                maxAlive.intValue = Mathf.Max(maxAlive.intValue, 14);
                serialized.ApplyModifiedProperties();
                EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            }

            // The prefabs are rebuilt, so they get their sounds again (the audio setup only fills what is missing).
            var playerCombat = Object.FindAnyObjectByType<Player.PlayerCombat>();
            if (playerCombat && Object.FindAnyObjectByType<Audio.SfxHooks>())
                AudioSetupMenu.Run(playerCombat);

            AssetDatabase.SaveAssets();
            Debug.Log("Enemies setup done: Golem, Bat and Shaman prefabs" + (spawner ? ", added to the spawner." : " (no EnemySpawner in the scene)."));
        }

        private static Enemy CreateGolem()
        {
            var root = new GameObject("Golem");
            CombatSetupMenu.AddEnemyComponents(root, groundOffset: 1.1f, maxHealth: 80, bodyRadius: 0.9f, experience: 40, knockbackMultiplier: 0.15f);
            var behaviour = root.AddComponent<GolemEnemy>();
            SetValues(behaviour, serialized =>
            {
                serialized.FindProperty("moveSpeed").floatValue = 2.2f;
                serialized.FindProperty("turnSpeedDegrees").floatValue = 120f;
            });

            // Rock body with a head, glowing eyes and two heavy arms.
            var visual = CreateVisual(root, 1.1f);
            var stone = GetMaterial("Golem_Stone", new Color(0.42f, 0.4f, 0.45f), Color.black);
            var moss = GetMaterial("Golem_Moss", new Color(0.25f, 0.4f, 0.2f), Color.black);
            var glow = GetMaterial("Golem_Eye", new Color(1f, 0.6f, 0.15f), new Color(2f, 1f, 0.2f));
            CreatePart("Legs", PrimitiveType.Cube, visual, new Vector3(0f, 0.35f, 0f), new Vector3(0.9f, 0.7f, 0.6f), stone);
            CreatePart("Body", PrimitiveType.Cube, visual, new Vector3(0f, 1.2f, 0f), new Vector3(1.4f, 1.1f, 0.9f), stone);
            CreatePart("Moss", PrimitiveType.Cube, visual, new Vector3(0f, 1.8f, -0.05f), new Vector3(1.2f, 0.15f, 0.8f), moss);
            CreatePart("Head", PrimitiveType.Cube, visual, new Vector3(0f, 2.05f, 0.15f), new Vector3(0.6f, 0.5f, 0.55f), stone);
            var eyes = CreateGroup("Eyes", visual);
            CreatePart("EyeL", PrimitiveType.Cube, eyes, new Vector3(0.14f, 2.1f, 0.43f), Vector3.one * 0.1f, glow);
            CreatePart("EyeR", PrimitiveType.Cube, eyes, new Vector3(-0.14f, 2.1f, 0.43f), Vector3.one * 0.1f, glow);
            var armLeft = CreateArm("ArmL", visual, new Vector3(-0.95f, 1.6f, 0f), stone);
            var armRight = CreateArm("ArmR", visual, new Vector3(0.95f, 1.6f, 0f), stone);

            ConfigureAnimator(root, visual, null, new[] { armLeft, armRight }, serialized =>
            {
                serialized.FindProperty("hopsWhenMoving").boolValue = false;
                serialized.FindProperty("breathAmount").floatValue = 0.015f;
                serialized.FindProperty("swayDegrees").floatValue = 3f;
                serialized.FindProperty("swayHz").floatValue = 0.8f;
                serialized.FindProperty("windupScale").floatValue = 1.08f;
                serialized.FindProperty("windupSeconds").floatValue = 1f;
                serialized.FindProperty("releaseKick").floatValue = 0.3f;
                serialized.FindProperty("hitSquash").floatValue = 0.95f;
                serialized.FindProperty("hitPunch").floatValue = 0.04f;
                serialized.FindProperty("flapDegrees").floatValue = 4f;
                serialized.FindProperty("flapHz").floatValue = 0.8f;
                serialized.FindProperty("windupFlapDegrees").floatValue = 110f;
            });

            return CombatSetupMenu.SavePrefab(root, "Golem").GetComponent<Enemy>();
        }

        private static Enemy CreateBat()
        {
            var root = new GameObject("Bat");
            CombatSetupMenu.AddEnemyComponents(root, groundOffset: 0.6f, maxHealth: 8, bodyRadius: 0.35f, experience: 4, knockbackMultiplier: 1.5f);
            var behaviour = root.AddComponent<BatEnemy>();
            SetValues(behaviour, serialized =>
            {
                serialized.FindProperty("moveSpeed").floatValue = 6.5f;
                serialized.FindProperty("turnSpeedDegrees").floatValue = 540f;
            });

            // Small dark body with big ears and two flapping wings, flying above the ground.
            var visual = CreateVisual(root, 0.6f);
            visual.localPosition += Vector3.up * 0.9f;
            var fur = GetMaterial("Bat_Fur", new Color(0.22f, 0.15f, 0.28f), Color.black);
            var wing = GetMaterial("Bat_Wing", new Color(0.35f, 0.2f, 0.4f), Color.black);
            var eye = GetMaterial("Bat_Eye", new Color(1f, 0.25f, 0.4f), new Color(2f, 0.3f, 0.6f));
            CreatePart("Body", PrimitiveType.Sphere, visual, new Vector3(0f, 0.3f, 0f), new Vector3(0.45f, 0.4f, 0.5f), fur);
            CreatePart("EarL", PrimitiveType.Cube, visual, new Vector3(0.1f, 0.55f, 0.05f), new Vector3(0.08f, 0.2f, 0.06f), fur);
            CreatePart("EarR", PrimitiveType.Cube, visual, new Vector3(-0.1f, 0.55f, 0.05f), new Vector3(0.08f, 0.2f, 0.06f), fur);
            var eyes = CreateGroup("Eyes", visual);
            CreatePart("EyeL", PrimitiveType.Sphere, eyes, new Vector3(0.08f, 0.38f, 0.22f), Vector3.one * 0.08f, eye);
            CreatePart("EyeR", PrimitiveType.Sphere, eyes, new Vector3(-0.08f, 0.38f, 0.22f), Vector3.one * 0.08f, eye);
            var wingLeft = CreateWing("WingL", visual, -1f, wing);
            var wingRight = CreateWing("WingR", visual, 1f, wing);

            ConfigureAnimator(root, visual, null, new[] { wingLeft, wingRight }, serialized =>
            {
                serialized.FindProperty("hopsWhenMoving").boolValue = false;
                serialized.FindProperty("breathAmount").floatValue = 0.05f;
                serialized.FindProperty("breathPeriod").floatValue = 0.4f;
                serialized.FindProperty("swayDegrees").floatValue = 8f;
                serialized.FindProperty("swayHz").floatValue = 1.3f;
                serialized.FindProperty("windupScale").floatValue = 1.25f;
                serialized.FindProperty("windupSeconds").floatValue = 0.35f;
                serialized.FindProperty("releaseKick").floatValue = 0.4f;
                serialized.FindProperty("flapDegrees").floatValue = 45f;
                serialized.FindProperty("flapHz").floatValue = 7f;
            });

            return CombatSetupMenu.SavePrefab(root, "Bat").GetComponent<Enemy>();
        }

        private static Enemy CreateShaman()
        {
            var root = new GameObject("Shaman");
            CombatSetupMenu.AddEnemyComponents(root, groundOffset: 0.9f, maxHealth: 30, bodyRadius: 0.5f, experience: 25, knockbackMultiplier: 1f);
            var behaviour = root.AddComponent<ShamanEnemy>();
            SetValues(behaviour, serialized => serialized.FindProperty("moveSpeed").floatValue = 3f);

            // Hooded robe with a bone mask and a staff topped with a glowing orb (the core that grows while casting).
            var visual = CreateVisual(root, 0.9f);
            var robe = GetMaterial("Shaman_Robe", new Color(0.45f, 0.15f, 0.2f), Color.black);
            var mask = GetMaterial("Shaman_Mask", new Color(0.9f, 0.85f, 0.7f), Color.black);
            var wood = GetMaterial("Shaman_Staff", new Color(0.35f, 0.22f, 0.12f), Color.black);
            var orb = GetMaterial("Shaman_Orb", new Color(0.75f, 0.3f, 1f), new Color(1.5f, 0.5f, 2.5f));
            CreatePart("Robe", PrimitiveType.Cylinder, visual, new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.55f, 0.7f), robe);
            CreatePart("Body", PrimitiveType.Capsule, visual, new Vector3(0f, 1.1f, 0f), new Vector3(0.5f, 0.45f, 0.5f), robe);
            CreatePart("Hood", PrimitiveType.Sphere, visual, new Vector3(0f, 1.55f, -0.02f), Vector3.one * 0.45f, robe);
            CreatePart("Mask", PrimitiveType.Cube, visual, new Vector3(0f, 1.52f, 0.2f), new Vector3(0.3f, 0.32f, 0.08f), mask);
            var eyes = CreateGroup("Eyes", visual);
            CreatePart("EyeL", PrimitiveType.Sphere, eyes, new Vector3(0.07f, 1.56f, 0.25f), Vector3.one * 0.05f, orb);
            CreatePart("EyeR", PrimitiveType.Sphere, eyes, new Vector3(-0.07f, 1.56f, 0.25f), Vector3.one * 0.05f, orb);
            CreatePart("Staff", PrimitiveType.Cylinder, visual, new Vector3(0.45f, 1f, 0.15f), new Vector3(0.06f, 0.85f, 0.06f), wood);
            var core = CreatePart("Core", PrimitiveType.Sphere, visual, new Vector3(0.45f, 1.95f, 0.15f), Vector3.one * 0.22f, orb);

            var lightObject = new GameObject("Core Light", typeof(Light));
            lightObject.transform.SetParent(core, false);
            var coreLight = lightObject.GetComponent<Light>();
            coreLight.type = LightType.Point;
            coreLight.color = new Color(0.75f, 0.3f, 1f);
            coreLight.range = 3f;
            coreLight.intensity = 1.5f;
            coreLight.shadows = LightShadows.None;

            ConfigureAnimator(root, visual, core, new Transform[0], serialized =>
            {
                serialized.FindProperty("hopsWhenMoving").boolValue = false;
                serialized.FindProperty("swayDegrees").floatValue = 5f;
                serialized.FindProperty("swayHz").floatValue = 0.9f;
                serialized.FindProperty("windupScale").floatValue = 1.1f;
                serialized.FindProperty("coreWindupScale").floatValue = 2.2f;
                serialized.FindProperty("windupSeconds").floatValue = 1.1f;
                serialized.FindProperty("releaseKick").floatValue = 0.15f;
            });

            return CombatSetupMenu.SavePrefab(root, "Shaman").GetComponent<Enemy>();
        }

        // Arm pivot at the shoulder, the stone hanging down from it.
        private static Transform CreateArm(string name, Transform visual, Vector3 shoulder, Material material)
        {
            var pivot = CreateGroup(name, visual);
            pivot.localPosition = shoulder;
            CreatePart("Upper", PrimitiveType.Cube, pivot, new Vector3(0f, -0.4f, 0f), new Vector3(0.45f, 0.8f, 0.45f), material);
            CreatePart("Fist", PrimitiveType.Cube, pivot, new Vector3(0f, -0.95f, 0.05f), new Vector3(0.6f, 0.5f, 0.6f), material);
            return pivot;
        }

        // Wing pivot next to the body, a flat membrane reaching out to the side.
        private static Transform CreateWing(string name, Transform visual, float side, Material material)
        {
            var pivot = CreateGroup(name, visual);
            pivot.localPosition = new Vector3(0.18f * side, 0.35f, 0f);
            CreatePart("Membrane", PrimitiveType.Cube, pivot, new Vector3(0.35f * side, 0f, -0.05f), new Vector3(0.7f, 0.03f, 0.4f), material);
            CreatePart("Tip", PrimitiveType.Cube, pivot, new Vector3(0.7f * side, -0.02f, -0.12f), new Vector3(0.25f, 0.03f, 0.25f), material);
            return pivot;
        }

        private static Transform CreateVisual(GameObject root, float groundOffset)
        {
            var visual = CreateGroup("Visual", root.transform);
            visual.localPosition = new Vector3(0f, -groundOffset, 0f);
            return visual;
        }

        private static void ConfigureAnimator(GameObject root, Transform visual, Transform core, Transform[] flappingParts, System.Action<SerializedObject> configure)
        {
            var animator = root.AddComponent<EnemyVisualAnimator>();
            var serialized = new SerializedObject(animator);
            serialized.FindProperty("visual").objectReferenceValue = visual;
            serialized.FindProperty("core").objectReferenceValue = core;
            var parts = serialized.FindProperty("flappingParts");
            parts.arraySize = flappingParts.Length;
            for (int i = 0; i < flappingParts.Length; i++)
                parts.GetArrayElementAtIndex(i).objectReferenceValue = flappingParts[i];

            configure(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<Combat.StatusEffects>();
        }

        private static void AddEntry(SerializedObject spawner, Enemy prefab, float weight, float minDifficulty, Vector2Int groupSize)
        {
            var entries = spawner.FindProperty("entries");
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab)
                    return;

            entries.arraySize++;
            CombatSetupMenu.SetEntry(entries.GetArrayElementAtIndex(entries.arraySize - 1), prefab, weight, minDifficulty, groupSize);
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        private static Transform CreatePart(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;

            // Hits are measured on the torus surface, colliders are not needed.
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        // URP Lit like the LD57 enemy materials: flat colour, a little smoothness, optional glow.
        private static Material GetMaterial(string name, Color color, Color emission)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.25f);

            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetValues(Object target, System.Action<SerializedObject> change)
        {
            var serialized = new SerializedObject(target);
            change(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
