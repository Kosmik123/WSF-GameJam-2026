using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Proposals of biome shaping (plateaus with cliffs, sunken water and lava) and their previews.
    /// The menu applies one variant to the biome profiles and gives the world its water material.
    /// The batch mode entry renders all variants side by side, without saving them.
    /// </summary>
    public static class BiomeShapingSetup
    {
        private const string BiomeFolder = "Assets/_Project/Terrain Profiles/Biomes";
        private const string WaterMaterialPath = "Assets/_Project/Materials/Water.mat";
        private const string WaterShaderName = "WSF/Water";
        private const string GameScenePath = "Assets/_Project/Scenes/Game/Game.unity";
        private const int ShotWidth = 640;
        private const int ShotHeight = 360;

        private class Shape
        {
            public float TierHeight;
            public int TierCount = 1;
            public float PlateauSize = 70f;
            public float Coverage = 0.35f;
            public float CliffSoftness = 0.03f;
            public float Seed;
            public bool HasBasins;
            public float BasinLevel;
            public float ShoreWidth = 0.25f;
            public float BasinDepth;
            public bool HasWater;
            public float SurfaceOffset = 0.25f;
        }

        private static readonly string[] Biomes = { "Las", "Pustynia", "Lod", "Wulkan", "Bagno", "Wybrzeze" };
        private static readonly string[] VariantNames = { "Obecnie", "A", "B", "C" };

        /// <summary>Shapes per variant (0 = no shaping), in the order of <see cref="Biomes"/>.</summary>
        private static Shape[] GetVariant(int variant)
        {
            switch (variant)
            {
                case 1:
                    return new[]
                    {
                        new Shape { TierHeight = 5f, PlateauSize = 70f, Coverage = 0.3f, Seed = 1f },
                        new Shape { TierHeight = 6f, PlateauSize = 90f, Coverage = 0.25f, Seed = 2f },
                        new Shape { TierHeight = 5f, PlateauSize = 70f, Coverage = 0.3f, Seed = 3f },
                        new Shape { TierHeight = 6f, PlateauSize = 70f, Coverage = 0.3f, Seed = 4f, HasBasins = true, BasinLevel = -0.2f, BasinDepth = 1.5f },
                        new Shape { Seed = 5f, HasBasins = true, BasinLevel = -0.2f, BasinDepth = 1.5f, HasWater = true },
                        new Shape { TierHeight = 6f, PlateauSize = 80f, Coverage = 0.3f, Seed = 6f, HasBasins = true, BasinLevel = -0.25f, BasinDepth = 2f, HasWater = true, SurfaceOffset = 0.2f },
                    };
                case 2:
                    return new[]
                    {
                        new Shape { TierHeight = 9f, PlateauSize = 60f, Coverage = 0.35f, CliffSoftness = 0.025f, Seed = 1f },
                        new Shape { TierHeight = 10f, TierCount = 2, PlateauSize = 80f, Coverage = 0.35f, CliffSoftness = 0.02f, Seed = 2f },
                        new Shape { TierHeight = 8f, TierCount = 2, PlateauSize = 70f, Coverage = 0.4f, CliffSoftness = 0.025f, Seed = 3f },
                        new Shape { TierHeight = 10f, TierCount = 2, PlateauSize = 70f, Coverage = 0.35f, CliffSoftness = 0.02f, Seed = 4f, HasBasins = true, BasinLevel = -0.2f, BasinDepth = 2.5f },
                        new Shape { TierHeight = 4f, PlateauSize = 45f, Coverage = 0.25f, Seed = 5f, HasBasins = true, BasinLevel = -0.2f, BasinDepth = 3f, HasWater = true },
                        new Shape { TierHeight = 10f, PlateauSize = 80f, Coverage = 0.35f, CliffSoftness = 0.02f, Seed = 6f, HasBasins = true, BasinLevel = -0.25f, BasinDepth = 4f, HasWater = true, SurfaceOffset = 0.2f },
                    };
                case 3:
                    return new[]
                    {
                        new Shape { TierHeight = 8f, TierCount = 2, PlateauSize = 60f, Coverage = 0.5f, CliffSoftness = 0.02f, Seed = 1f },
                        new Shape { TierHeight = 14f, TierCount = 2, PlateauSize = 70f, Coverage = 0.45f, CliffSoftness = 0.015f, Seed = 2f },
                        new Shape { TierHeight = 12f, TierCount = 2, PlateauSize = 60f, Coverage = 0.5f, CliffSoftness = 0.02f, Seed = 3f },
                        new Shape { TierHeight = 14f, TierCount = 2, PlateauSize = 60f, Coverage = 0.45f, CliffSoftness = 0.015f, Seed = 4f, HasBasins = true, BasinLevel = -0.2f, BasinDepth = 4f },
                        new Shape { TierHeight = 7f, PlateauSize = 55f, Coverage = 0.3f, CliffSoftness = 0.025f, Seed = 5f, HasBasins = true, BasinLevel = -0.2f, BasinDepth = 5f, HasWater = true },
                        new Shape { TierHeight = 9f, TierCount = 2, PlateauSize = 70f, Coverage = 0.45f, CliffSoftness = 0.015f, Seed = 6f, HasBasins = true, BasinLevel = -0.25f, BasinDepth = 6f, HasWater = true, SurfaceOffset = 0.2f },
                    };
                default:
                    return new[] { new Shape(), new Shape(), new Shape(), new Shape(), new Shape(), new Shape() };
            }
        }

        [MenuItem("WSF GameJam/Setup/Biome Shaping/Variant A")]
        private static void ApplyVariantA() => ApplyAndSave(1);

        [MenuItem("WSF GameJam/Setup/Biome Shaping/Variant B")]
        private static void ApplyVariantB() => ApplyAndSave(2);

        [MenuItem("WSF GameJam/Setup/Biome Shaping/Variant C")]
        private static void ApplyVariantC() => ApplyAndSave(3);

        [MenuItem("WSF GameJam/Setup/Biome Shaping/Remove Shaping")]
        private static void RemoveShaping() => ApplyAndSave(0);

        /// <summary>Batch mode entry: renders every variant of every biome into -previewOutput. Nothing is saved.</summary>
        public static void RenderProposalsFromCommandLine()
        {
            string output = GetCommandLineValue("-previewOutput") ?? "BiomeShapingPreviews";
            EditorSceneManager.OpenScene(GameScenePath);
            var world = Object.FindAnyObjectByType<TorusWorld>();
            if (world == null)
            {
                Debug.LogError("Biome shaping: the game scene has no Torus World.");
                EditorApplication.Exit(1);
                return;
            }

            SetWaterMaterial(world, GetOrCreateWaterMaterial());
            HideHead();
            Directory.CreateDirectory(output);

            var sheets = new Texture2D[Biomes.Length];
            for (int i = 0; i < Biomes.Length; i++)
                sheets[i] = new Texture2D(ShotWidth * VariantNames.Length, ShotHeight * 2, TextureFormat.RGB24, false);

            for (int variant = 0; variant < VariantNames.Length; variant++)
            {
                ApplyVariant(variant);
                world.HeadTerrain.Deactivate();
                world.Rebuild();
                for (int biome = 0; biome < Biomes.Length; biome++)
                    RenderBiome(world, biome, variant, sheets[biome]);
            }

            for (int biome = 0; biome < Biomes.Length; biome++)
            {
                sheets[biome].Apply();
                File.WriteAllBytes(Path.Combine(output, $"biome_{biome}_{Biomes[biome]}.png"), sheets[biome].EncodeToPNG());
                Object.DestroyImmediate(sheets[biome]);
            }

            Debug.Log($"Biome shaping previews saved to {output}");
        }

        private static void ApplyAndSave(int variant)
        {
            ApplyVariant(variant);
            AssetDatabase.SaveAssets();

            var world = Object.FindAnyObjectByType<TorusWorld>();
            if (world != null)
            {
                SetWaterMaterial(world, GetOrCreateWaterMaterial());
                world.Rebuild();
                EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            }

            Debug.Log($"Biome shaping: variant {VariantNames[variant]} applied." + (world == null ? " Open the game scene and run it again to set the water material." : string.Empty));
        }

        private static void ApplyVariant(int variant)
        {
            var shapes = GetVariant(variant);
            for (int i = 0; i < Biomes.Length; i++)
            {
                var profile = LoadProfile(Biomes[i]);
                if (profile == null)
                {
                    Debug.LogWarning($"Biome shaping: no profile for {Biomes[i]}.");
                    continue;
                }

                WriteShape(profile, shapes[i]);
                profile.Invalidate();
            }
        }

        private static GridTorusSegmentProfile LoadProfile(string biome)
        {
            return AssetDatabase.LoadAssetAtPath<GridTorusSegmentProfile>($"{BiomeFolder}/Biome {biome}.asset");
        }

        private static void WriteShape(GridTorusSegmentProfile profile, Shape shape)
        {
            var serialized = new SerializedObject(profile);
            serialized.FindProperty("shaping.tierHeight").floatValue = shape.TierHeight;
            serialized.FindProperty("shaping.tierCount").intValue = shape.TierCount;
            serialized.FindProperty("shaping.plateauSize").floatValue = shape.PlateauSize;
            serialized.FindProperty("shaping.coverage").floatValue = shape.Coverage;
            serialized.FindProperty("shaping.cliffSoftness").floatValue = shape.CliffSoftness;
            serialized.FindProperty("shaping.seed").floatValue = shape.Seed;
            serialized.FindProperty("shaping.hasBasins").boolValue = shape.HasBasins;
            serialized.FindProperty("shaping.basinLevel").floatValue = shape.BasinLevel;
            serialized.FindProperty("shaping.shoreWidth").floatValue = shape.ShoreWidth;
            serialized.FindProperty("shaping.basinDepth").floatValue = shape.BasinDepth;
            serialized.FindProperty("shaping.hasWater").boolValue = shape.HasWater;
            serialized.FindProperty("shaping.surfaceOffset").floatValue = shape.SurfaceOffset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material GetOrCreateWaterMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (material != null)
                return material;

            var shader = Shader.Find(WaterShaderName);
            if (shader == null)
            {
                Debug.LogError($"Biome shaping: shader {WaterShaderName} not found.");
                return null;
            }

            material = new Material(shader) { name = "Water" };
            AssetDatabase.CreateAsset(material, WaterMaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void SetWaterMaterial(TorusWorld world, Material material)
        {
            var serialized = new SerializedObject(world);
            serialized.FindProperty("waterMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void HideHead()
        {
            var head = Object.FindAnyObjectByType<WSFGameJam2026.Head.HeadMovement>();
            if (head != null)
                head.gameObject.SetActive(false);
        }

        private static void RenderBiome(TorusWorld world, int biome, int variant, Texture2D sheet)
        {
            var profile = LoadProfile(Biomes[biome]);
            var surface = world.Surface;
            int segment = -1;
            for (int i = 0; i < surface.SegmentCount; i++)
            {
                if (surface.GetProfile(i) == profile)
                {
                    segment = i;
                    break;
                }
            }

            if (segment < 0)
                return;

            float meter = 1f / world.MajorRadius;
            float centre = (segment + 0.5f) * surface.SegmentAngle;
            float top = Mathf.PI * 0.5f;

            // Aerial: high above the top of the tube, looking along the segment.
            var aerialEye = surface.GetPoint(centre - 110f * meter, top) + surface.GetBaseNormal(centre - 110f * meter, top) * 75f;
            var aerialLook = surface.GetPoint(centre, top);
            RenderShot(world, aerialEye, aerialLook, surface.GetBaseNormal(centre - 110f * meter, top), sheet, variant, 1);

            // Ground: like the game camera, a few meters above the player.
            float eyeTheta = centre - 45f * meter;
            var groundEye = surface.GetPoint(eyeTheta, top) + surface.GetBaseNormal(eyeTheta, top) * 9f;
            var groundLook = surface.GetPoint(centre + 15f * meter, top) + surface.GetBaseNormal(centre + 15f * meter, top) * 2f;
            RenderShot(world, groundEye, groundLook, surface.GetBaseNormal(eyeTheta, top), sheet, variant, 0);
        }

        private static void RenderShot(TorusWorld world, Vector3 localEye, Vector3 localLook, Vector3 localUp, Texture2D sheet, int column, int row)
        {
            var cameraObject = new GameObject("Biome Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 4000f;
            camera.nearClipPlane = 0.3f;
            camera.fieldOfView = 60f;
            var target = new RenderTexture(ShotWidth, ShotHeight, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            try
            {
                var worldTransform = world.transform;
                var eye = worldTransform.TransformPoint(localEye);
                var look = worldTransform.TransformPoint(localLook);
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, worldTransform.TransformDirection(localUp)));
                camera.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                sheet.ReadPixels(new Rect(0, 0, ShotWidth, ShotHeight), column * ShotWidth, row * ShotHeight);
                RenderTexture.active = previous;
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
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
    }
}
