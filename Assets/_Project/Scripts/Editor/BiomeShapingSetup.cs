using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Shaping of the biome terrains (plateaus with cliffs and a free path, sunken water and lava) and its painting
    /// by the rules of the biome assets (docs/WSF_Biom.md, palettes from ld57_biomes.py, sRGB).
    /// The menu writes the settings to the biome profiles and gives the world its water material.
    /// The batch mode entry renders the terrain before and after, then saves the settings.
    /// </summary>
    public static class BiomeShapingSetup
    {
        private const string BiomeFolder = "Assets/_Project/Terrain Profiles/Biomes";
        private const string WaterMaterialPath = "Assets/_Project/Materials/Water.mat";
        private const string WaterShaderName = "WSF/Water";
        private const string GameScenePath = "Assets/_Project/Scenes/Game/Game.unity";
        private const int ShotWidth = 800;
        private const int ShotHeight = 450;

        private class Shape
        {
            public float TierHeight;
            public int TierCount = 1;
            public float PlateauSize = 70f;
            public float Coverage = 0.25f;
            public float CliffSoftness = 0.025f;
            public float Seed;
            public float PathHalfWidth = 0.05f;
            public float PathWander = 0.07f;
            public bool HasBasins;
            public float BasinLevel;
            public float ShoreWidth = 0.25f;
            public float BasinDepth;
            public bool HasWater;
            public float SurfaceOffset = 0.25f;
            public bool IsPainted;
            public Color CliffDark = new Color(0.30f, 0.32f, 0.36f);
            public Color CliffLight = new Color(0.62f, 0.64f, 0.68f);
            public Color[] StrataColors = new Color[0];
            public float StrataBand = 2f;
            public Color TopCover = new Color(1f, 1f, 1f, 0f);
            public float TierLighten = 0.06f;
            public float LavaCrackChance;
            public Color LavaColor = new Color(1f, 0.45f, 0.1f);
            public Color BasinColor = new Color(0f, 0f, 0f, 0f);
        }

        private static readonly string[] Biomes = { "Las", "Pustynia", "Lod", "Wulkan", "Bagno", "Wybrzeze" };

        private static Shape[] GetShapes()
        {
            return new[]
            {
                // Las: rock with moss on the plateau tops.
                new Shape
                {
                    TierHeight = 9f, PlateauSize = 60f, Coverage = 0.22f, Seed = 1f, IsPainted = true,
                    CliffDark = new Color(0.34f, 0.32f, 0.28f), CliffLight = new Color(0.66f, 0.63f, 0.56f),
                    TopCover = new Color(0.30f, 0.40f, 0.13f, 0.35f),
                },
                // Pustynia: rock layers terracotta / ochre / sand.
                new Shape
                {
                    TierHeight = 9f, TierCount = 2, PlateauSize = 80f, Coverage = 0.25f, CliffSoftness = 0.02f, Seed = 2f, IsPainted = true,
                    StrataColors = new[] { new Color(0.70f, 0.36f, 0.20f), new Color(0.80f, 0.58f, 0.28f), new Color(0.85f, 0.70f, 0.48f) },
                    StrataBand = 2f, TopCover = new Color(0.93f, 0.80f, 0.58f, 0.3f),
                },
                // Lod: grey-blue rock, snow on the plateau tops.
                new Shape
                {
                    TierHeight = 7f, TierCount = 2, PlateauSize = 70f, Coverage = 0.25f, Seed = 3f, IsPainted = true,
                    CliffDark = new Color(0.32f, 0.36f, 0.44f), CliffLight = new Color(0.60f, 0.64f, 0.72f),
                    TopCover = new Color(0.95f, 0.97f, 1f, 0.8f),
                },
                // Wulkan: basalt with lava cracks, ash on the tops, lava sunk into its beds.
                new Shape
                {
                    TierHeight = 9f, TierCount = 2, PlateauSize = 70f, Coverage = 0.25f, CliffSoftness = 0.02f, Seed = 4f,
                    HasBasins = true, BasinLevel = -0.2f, BasinDepth = 2.5f, IsPainted = true,
                    CliffDark = new Color(0.07f, 0.07f, 0.08f), CliffLight = new Color(0.26f, 0.25f, 0.27f),
                    TopCover = new Color(0.40f, 0.38f, 0.37f, 0.4f), LavaCrackChance = 0.3f,
                },
                // Bagno: water, low hummocks of dark earth with wet moss, mud at the bottom.
                new Shape
                {
                    TierHeight = 4f, PlateauSize = 45f, Coverage = 0.18f, Seed = 5f,
                    HasBasins = true, BasinLevel = -0.2f, BasinDepth = 3f, HasWater = true, IsPainted = true,
                    CliffDark = new Color(0.16f, 0.13f, 0.09f), CliffLight = new Color(0.36f, 0.30f, 0.22f),
                    TopCover = new Color(0.30f, 0.40f, 0.12f, 0.5f), BasinColor = new Color(0.10f, 0.09f, 0.05f, 0.6f),
                },
                // Wybrzeze: bays, coastal rock cliffs, wet sand under the water.
                new Shape
                {
                    TierHeight = 10f, PlateauSize = 80f, Coverage = 0.25f, CliffSoftness = 0.02f, Seed = 6f,
                    HasBasins = true, BasinLevel = -0.25f, BasinDepth = 4f, HasWater = true, SurfaceOffset = 0.2f, IsPainted = true,
                    CliffDark = new Color(0.30f, 0.32f, 0.36f), CliffLight = new Color(0.62f, 0.64f, 0.68f),
                    TopCover = new Color(0.55f, 0.55f, 0.30f, 0.25f), BasinColor = new Color(0.45f, 0.36f, 0.22f, 0.6f),
                },
            };
        }

        [MenuItem("WSF GameJam/Setup/Biome Shaping/Apply")]
        private static void ApplyFromMenu()
        {
            ApplyAndSave(GetShapes());
        }

        [MenuItem("WSF GameJam/Setup/Biome Shaping/Remove")]
        private static void RemoveFromMenu()
        {
            ApplyAndSave(new[] { new Shape(), new Shape(), new Shape(), new Shape(), new Shape(), new Shape() });
        }

        /// <summary>Batch mode entry: renders the biomes before and after into -previewOutput, then saves the shaping.</summary>
        public static void ApplyAndRenderFromCommandLine()
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
            var head = Object.FindAnyObjectByType<WSFGameJam2026.Head.HeadMovement>();
            if (head != null)
                head.gameObject.SetActive(false);

            Directory.CreateDirectory(output);
            var sheets = new Texture2D[Biomes.Length];
            for (int i = 0; i < Biomes.Length; i++)
                sheets[i] = new Texture2D(ShotWidth * 2, ShotHeight * 2, TextureFormat.RGB24, false);

            var before = new[] { new Shape(), new Shape(), new Shape(), new Shape(), new Shape(), new Shape() };
            var after = GetShapes();
            for (int column = 0; column < 2; column++)
            {
                WriteShapes(column == 0 ? before : after);
                world.HeadTerrain.Deactivate();
                world.Rebuild();
                for (int biome = 0; biome < Biomes.Length; biome++)
                    RenderBiome(world, biome, column, sheets[biome]);
            }

            for (int biome = 0; biome < Biomes.Length; biome++)
            {
                sheets[biome].Apply();
                File.WriteAllBytes(Path.Combine(output, $"biome_{biome}_{Biomes[biome]}.png"), sheets[biome].EncodeToPNG());
                Object.DestroyImmediate(sheets[biome]);
            }

            // Only the profiles are saved: saving the scene in batch mode breaks the layout of its UI.
            AssetDatabase.SaveAssets();
            Debug.Log($"Biome shaping: applied and saved, previews in {output}");
        }

        private static void ApplyAndSave(Shape[] shapes)
        {
            WriteShapes(shapes);
            AssetDatabase.SaveAssets();

            var world = Object.FindAnyObjectByType<TorusWorld>();
            if (world != null)
            {
                SetWaterMaterial(world, GetOrCreateWaterMaterial());
                world.Rebuild();
                EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            }

            Debug.Log("Biome shaping: applied." + (world == null ? " Open the game scene and run it again to set the water material." : string.Empty));
        }

        private static void WriteShapes(Shape[] shapes)
        {
            for (int i = 0; i < Biomes.Length; i++)
            {
                var profile = AssetDatabase.LoadAssetAtPath<GridTorusSegmentProfile>($"{BiomeFolder}/Biome {Biomes[i]}.asset");
                if (profile == null)
                {
                    Debug.LogWarning($"Biome shaping: no profile for {Biomes[i]}.");
                    continue;
                }

                WriteShape(profile, shapes[i]);
                profile.Invalidate();
            }
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
            serialized.FindProperty("shaping.pathHalfWidth").floatValue = shape.PathHalfWidth;
            serialized.FindProperty("shaping.pathWander").floatValue = shape.PathWander;
            serialized.FindProperty("shaping.hasBasins").boolValue = shape.HasBasins;
            serialized.FindProperty("shaping.basinLevel").floatValue = shape.BasinLevel;
            serialized.FindProperty("shaping.shoreWidth").floatValue = shape.ShoreWidth;
            serialized.FindProperty("shaping.basinDepth").floatValue = shape.BasinDepth;
            serialized.FindProperty("shaping.hasWater").boolValue = shape.HasWater;
            serialized.FindProperty("shaping.surfaceOffset").floatValue = shape.SurfaceOffset;
            serialized.FindProperty("shaping.isPainted").boolValue = shape.IsPainted;
            serialized.FindProperty("shaping.cliffDark").colorValue = shape.CliffDark;
            serialized.FindProperty("shaping.cliffLight").colorValue = shape.CliffLight;
            serialized.FindProperty("shaping.strataBand").floatValue = shape.StrataBand;
            serialized.FindProperty("shaping.topCover").colorValue = shape.TopCover;
            serialized.FindProperty("shaping.tierLighten").floatValue = shape.TierLighten;
            serialized.FindProperty("shaping.lavaCrackChance").floatValue = shape.LavaCrackChance;
            serialized.FindProperty("shaping.lavaColor").colorValue = shape.LavaColor;
            serialized.FindProperty("shaping.basinColor").colorValue = shape.BasinColor;

            var strata = serialized.FindProperty("shaping.strataColors");
            strata.arraySize = shape.StrataColors.Length;
            for (int i = 0; i < shape.StrataColors.Length; i++)
                strata.GetArrayElementAtIndex(i).colorValue = shape.StrataColors[i];

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

        private static void RenderBiome(TorusWorld world, int biome, int column, Texture2D sheet)
        {
            var profile = AssetDatabase.LoadAssetAtPath<GridTorusSegmentProfile>($"{BiomeFolder}/Biome {Biomes[biome]}.asset");
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
            float aerialTheta = centre - 110f * meter;
            var aerialEye = surface.GetPoint(aerialTheta, top) + surface.GetBaseNormal(aerialTheta, top) * 75f;
            RenderShot(world, aerialEye, surface.GetPoint(centre, top), surface.GetBaseNormal(aerialTheta, top), sheet, column, 1);

            // Ground: like the game camera, a few meters above the path.
            float eyeTheta = centre - 45f * meter;
            var groundEye = surface.GetPoint(eyeTheta, top) + surface.GetBaseNormal(eyeTheta, top) * 9f;
            var groundLook = surface.GetPoint(centre + 15f * meter, top) + surface.GetBaseNormal(centre + 15f * meter, top) * 2f;
            RenderShot(world, groundEye, groundLook, surface.GetBaseNormal(eyeTheta, top), sheet, column, 0);
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
