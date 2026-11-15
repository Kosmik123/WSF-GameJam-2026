using System.IO;
using UnityEditor;
using UnityEngine;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Renders preview images of the torus world with temporary cameras: the whole torus and a view
    /// from the top of the tube in the middle of every segment (biome). Used by the batch mode biome setup.
    /// </summary>
    public static class BiomeTorusPreview
    {
        private const int Width = 1600;
        private const int Height = 900;

        [MenuItem("WSF GameJam/Setup/Render Biome Previews")]
        private static void RenderFromMenu()
        {
            var world = Object.FindAnyObjectByType<TorusWorld>();
            if (world == null)
                return;

            string folder = Path.GetFullPath("Temp/BiomePreviews");
            Render(world, folder);
            EditorUtility.RevealInFinder(folder);
        }

        public static void Render(TorusWorld world, string folder)
        {
            Directory.CreateDirectory(folder);
            var cameraObject = new GameObject("Biome Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 4000f;
            camera.nearClipPlane = 0.3f;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            try
            {
                var worldTransform = world.transform;
                float size = world.MajorRadius + world.MinorRadius;
                camera.fieldOfView = 50f;
                var overview = worldTransform.TransformPoint(new Vector3(0f, size * 1.6f, -size * 1.9f));
                cameraObject.transform.SetPositionAndRotation(overview, Quaternion.LookRotation(worldTransform.position - overview, worldTransform.up));
                Save(camera, target, Path.Combine(folder, "torus_overview.png"));

                var surface = world.Surface;
                camera.fieldOfView = 60f;
                for (int segment = 0; segment < surface.SegmentCount; segment++)
                {
                    float theta = (segment + 0.5f) * surface.SegmentAngle;
                    float phi = Mathf.PI * 0.5f;
                    var ground = surface.GetPoint(theta, phi);
                    var up = surface.GetBaseNormal(theta, phi);
                    var forward = TorusGeometry.GetThetaDirection(theta);
                    var eye = worldTransform.TransformPoint(ground + up * 14f - forward * 30f);
                    var look = worldTransform.TransformPoint(ground + forward * 25f);
                    cameraObject.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, worldTransform.TransformDirection(up)));
                    Save(camera, target, Path.Combine(folder, $"segment_{segment}.png"));
                }

                // Borders between biomes, seen from above the seam across the blend zone.
                for (int segment = 0; segment < surface.SegmentCount; segment++)
                {
                    float theta = segment * surface.SegmentAngle;
                    float phi = Mathf.PI * 0.5f;
                    var ground = surface.GetPoint(theta, phi);
                    var up = surface.GetBaseNormal(theta, phi);
                    var side = TorusGeometry.GetPhiDirection(theta, phi);
                    var eye = worldTransform.TransformPoint(ground + up * 55f - side * 45f);
                    var look = worldTransform.TransformPoint(ground + side * 10f);
                    cameraObject.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, worldTransform.TransformDirection(up)));
                    Save(camera, target, Path.Combine(folder, $"border_{segment}.png"));
                }

                Debug.Log($"Biome previews saved to {folder}");
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Save(Camera camera, RenderTexture target, string path)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
