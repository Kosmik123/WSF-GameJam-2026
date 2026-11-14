using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.Head;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Adds the dust storm in front of the snake's mouth to the Snake Head prefab (see <see cref="HeadDustStorm"/>):
    /// three particle systems (dust at the mouth, flying debris, a wall of haze further ahead) and their materials.
    /// Safe to run again: the existing systems are configured from scratch.
    /// </summary>
    public static class HeadDustStormSetupMenu
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Snake Head.prefab";
        private const string MaterialFolder = "Assets/_Project/Materials/Effects";
        private const string ContainerName = "Dust Storm";
        private const string GameScenePath = "Assets/_Project/Scenes/Game/Game.unity";

        [MenuItem("WSF GameJam/Setup/Head Dust Storm")]
        private static void SetupFromMenu()
        {
            if (TrySetup(out string error) == false)
                EditorUtility.DisplayDialog("Head dust storm", error, "OK");
        }

        /// <summary>
        /// Batch mode entry: sets up the prefab, then (with -previewOutput) renders the effect and the toppling trees in the game scene.
        /// </summary>
        public static void SetupAndRenderFromCommandLine()
        {
            if (TrySetup(out string error) == false)
            {
                Debug.LogError("Head dust storm: " + error);
                EditorApplication.Exit(1);
                return;
            }

            string output = GetCommandLineValue("-previewOutput");
            if (string.IsNullOrEmpty(output) == false)
            {
                EditorSceneManager.OpenScene(GameScenePath);
                HeadDustStormPreview.Render(output);
            }
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

        private static bool TrySetup(out string error)
        {
            error = null;
            var dustShader = Shader.Find("WSF/Particle Dust");
            var litShader = Shader.Find("WSF/Vertex Color Lit");
            if (dustShader == null || litShader == null)
            {
                error = "Shaders 'WSF/Particle Dust' and 'WSF/Vertex Color Lit' are needed (Assets/_Project/Shaders).";
                return false;
            }

            EnsureFolder(MaterialFolder);
            var dustMaterial = GetOrCreateMaterial("Head Dust", dustShader);
            dustMaterial.SetFloat("_Softness", 0.8f);
            dustMaterial.SetFloat("_Lumpiness", 0.6f);
            var hazeMaterial = GetOrCreateMaterial("Head Storm Haze", dustShader);
            hazeMaterial.SetFloat("_Softness", 1f);
            hazeMaterial.SetFloat("_Lumpiness", 0.35f);
            var debrisMaterial = GetOrCreateMaterial("Head Debris", litShader);
            debrisMaterial.SetFloat("_SrgbVertexColor", 1f);
            debrisMaterial.enableInstancing = false;
            AssetDatabase.SaveAssets();

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var head = root.GetComponent<HeadMovement>();
                if (head == null)
                {
                    error = $"{PrefabPath} has no Head Movement.";
                    return false;
                }

                var container = root.transform.Find(ContainerName);
                if (container == null)
                {
                    container = new GameObject(ContainerName).transform;
                    container.SetParent(root.transform, false);
                }

                var dust = CreateDust(container, dustMaterial);
                var debris = CreateDebris(container, debrisMaterial);
                var haze = CreateHaze(container, hazeMaterial);

                if (root.TryGetComponent<HeadDustStorm>(out var storm) == false)
                    storm = root.AddComponent<HeadDustStorm>();

                var serialized = new SerializedObject(storm);
                serialized.FindProperty("head").objectReferenceValue = head;
                var emitters = serialized.FindProperty("emitters");
                emitters.arraySize = 3;
                SetEmitter(emitters.GetArrayElementAtIndex(0), dust, 4f, 1f, 0.65f);
                SetEmitter(emitters.GetArrayElementAtIndex(1), debris, 2f, 0.5f, 0.4f);
                SetEmitter(emitters.GetArrayElementAtIndex(2), haze, 40f, 8f, 0.5f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log("Head dust storm: set up on " + PrefabPath);
            return true;
        }

        private static void SetEmitter(SerializedProperty emitter, ParticleSystem particles, float aheadDistance, float radiusOffset, float terrainTint)
        {
            emitter.FindPropertyRelative("Particles").objectReferenceValue = particles;
            emitter.FindPropertyRelative("AheadDistance").floatValue = aheadDistance;
            emitter.FindPropertyRelative("RadiusOffset").floatValue = radiusOffset;
            emitter.FindPropertyRelative("TerrainTint").floatValue = terrainTint;
        }

        /// <summary>Thick dust thrown out of the ground right in front of the mouth, rolling ahead of the head.</summary>
        private static ParticleSystem CreateDust(Transform container, Material material)
        {
            var particles = GetOrCreateSystem(container, "Mouth Dust");
            var main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(6f, 15f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.7f, 0.55f, 0.45f), new Color(0.65f, 0.55f, 0.42f, 0.7f));
            main.maxParticles = 1200;

            var emission = particles.emission;
            emission.rateOverTime = 150f;

            SetRing(particles, 0.3f);
            SetForwardVelocity(particles, 10f, 24f, 4f, -10f);

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 1.6f));

            SetFade(particles, 0.15f, 0.55f);
            SetRotation(particles, 25f);
            SetNoise(particles, 4f, 0.15f);
            SetDrag(particles, 0.5f);
            SetBillboard(particles, material);
            return particles;
        }

        /// <summary>Small pieces (stones, bark, leaves) flying out and tumbling, pulled back to the ground.</summary>
        private static ParticleSystem CreateDebris(Transform container, Material material)
        {
            var particles = GetOrCreateSystem(container, "Debris");
            var main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 26f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.2f, 1f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.36f, 0.29f, 0.2f, 1f), new Color(0.28f, 0.36f, 0.16f, 1f));
            main.maxParticles = 700;

            var emission = particles.emission;
            emission.rateOverTime = 110f;

            SetRing(particles, 0.45f);
            SetForwardVelocity(particles, 12f, 30f, 0f, -26f);

            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rotation.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
            rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));

            SetNoise(particles, 7f, 0.35f);

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.SetMeshes(new[]
            {
                Resources.GetBuiltinResource<Mesh>("Cube.fbx"),
                Resources.GetBuiltinResource<Mesh>("Quad.fbx"),
            });
            renderer.sharedMaterial = material;
            renderer.enableGPUInstancing = false;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return particles;
        }

        /// <summary>Faint, huge haze rolling along further ahead: the front of the coming storm.</summary>
        private static ParticleSystem CreateHaze(Transform container, Material material)
        {
            var particles = GetOrCreateSystem(container, "Storm Haze");
            var main = particles.main;
            main.duration = 8f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(25f, 45f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.85f, 0.75f, 0.6f, 0.12f), new Color(0.7f, 0.6f, 0.48f, 0.24f));
            main.maxParticles = 400;

            var emission = particles.emission;
            emission.rateOverTime = 45f;

            SetRing(particles, 0.6f);
            SetForwardVelocity(particles, 18f, 28f, 0f, 0f);

            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.4f));

            SetFade(particles, 0.25f, 0.6f);
            SetRotation(particles, 8f);
            SetNoise(particles, 2f, 0.08f);
            SetBillboard(particles, material);
            return particles;
        }

        private static ParticleSystem GetOrCreateSystem(Transform container, string name)
        {
            var child = container.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(container, false);
            }

            if (child.TryGetComponent<ParticleSystem>(out var particles) == false)
                particles = child.gameObject.AddComponent<ParticleSystem>();

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.playOnAwake = true;
            main.gravityModifier = 0f;
            main.startRotation3D = false;
            main.startSize3D = false;
            return particles;
        }

        /// <summary>Ring around the tube (the emitter faces along the body), emitting outwards from its edge.</summary>
        private static void SetRing(ParticleSystem particles, float randomDirection)
        {
            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 50f;
            shape.radiusThickness = 0f;
            shape.arc = 360f;
            shape.randomDirectionAmount = randomDirection;
            shape.rotation = Vector3.zero;
            shape.position = Vector3.zero;
        }

        /// <summary>Velocity ahead along the body (local Z) plus a radial pull: positive pushes away from the tube, negative back down.</summary>
        private static void SetForwardVelocity(ParticleSystem particles, float forwardMin, float forwardMax, float radialStart, float radialEnd)
        {
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(forwardMin, forwardMax);
            velocity.radial = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, radialStart, 1f, radialEnd));
        }

        private static void SetFade(ParticleSystem particles, float fadeIn, float fadeOutStart)
        {
            var color = particles.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOutStart), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }

        private static void SetRotation(ParticleSystem particles, float degreesPerSecond)
        {
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = false;
            float radians = degreesPerSecond * Mathf.Deg2Rad;
            rotation.z = new ParticleSystem.MinMaxCurve(-radians, radians);
        }

        private static void SetNoise(ParticleSystem particles, float strength, float frequency)
        {
            var noise = particles.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        private static void SetDrag(ParticleSystem particles, float drag)
        {
            var limit = particles.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
        }

        private static void SetBillboard(ParticleSystem particles, Material material)
        {
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 1.5f;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Material GetOrCreateMaterial(string name, Shader shader)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
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
