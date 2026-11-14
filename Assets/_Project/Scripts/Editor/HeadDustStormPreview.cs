using System.IO;
using System.Reflection;
using UnityEngine;
using WSFGameJam2026.Head;
using WSFGameJam2026.World;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Renders the head's dust storm and the trees toppling in front of it in edit mode (used by batch mode):
    /// runs the head components once by hand, simulates the particles for a few seconds and takes pictures.
    /// </summary>
    public static class HeadDustStormPreview
    {
        private const int Width = 1600;
        private const int Height = 900;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void Render(string folder)
        {
            var world = Object.FindAnyObjectByType<TorusWorld>();
            var head = Object.FindAnyObjectByType<HeadMovement>();
            if (world == null || head == null)
            {
                Debug.LogError("Head dust storm preview: the scene needs a Torus World and a Head Movement.");
                return;
            }

            Directory.CreateDirectory(folder);
            var surface = world.Surface;
            float[] thetas = { 0.62f * surface.SegmentAngle, 1.55f * surface.SegmentAngle, 4.4f * surface.SegmentAngle };
            for (int shot = 0; shot < thetas.Length; shot++)
            {
                PlaceHead(world, head, thetas[shot]);
                SimulateStorm(head);
                RenderShots(world, folder, shot);
            }

            Debug.Log($"Head dust storm previews saved to {folder}");
        }

        private static void PlaceHead(TorusWorld world, HeadMovement head, float theta)
        {
            typeof(HeadMovement).GetField("theta", Private).SetValue(head, theta);
            head.Refresh();

            if (head.TryGetComponent<HeadTerrainBlend>(out var blend))
            {
                Invoke(blend, "Awake");
                Invoke(blend, "LateUpdate");
            }

            world.Rebuild();
        }

        private static void SimulateStorm(HeadMovement head)
        {
            if (head.TryGetComponent<HeadDustStorm>(out var storm) == false)
                return;

            Invoke(storm, "LateUpdate");
            foreach (var particles in storm.GetComponentsInChildren<ParticleSystem>())
            {
                particles.Clear(true);
                particles.Simulate(3.5f, false, true, false);
            }
        }

        private static void RenderShots(TorusWorld world, string folder, int shot)
        {
            var headTerrain = world.HeadTerrain;
            float front = headTerrain.NeckTheta + headTerrain.HeadLength;
            var surface = world.Surface;
            var worldTransform = world.transform;

            var cameraObject = new GameObject("Dust Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 4000f;
            camera.nearClipPlane = 0.3f;
            camera.fieldOfView = 60f;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;

            try
            {
                // Standing on the tail ahead of the head, looking back at the mouth.
                float phi = Mathf.PI * 0.5f;
                float eyeTheta = front + 110f / world.MajorRadius;
                var eye = surface.GetPoint(eyeTheta, phi) + surface.GetBaseNormal(eyeTheta, phi) * 22f;
                var look = surface.GetPoint(front, phi) + surface.GetBaseNormal(front, phi) * 15f;
                Save(camera, target, worldTransform, eye, look, surface.GetBaseNormal(eyeTheta, phi), Path.Combine(folder, $"storm_{shot}_front.png"));

                // From the side, over the shoulder of the tube.
                float sidePhi = Mathf.PI * 0.15f;
                float sideTheta = front + 40f / world.MajorRadius;
                var sideEye = surface.GetPoint(sideTheta, sidePhi) + surface.GetBaseNormal(sideTheta, sidePhi) * 70f;
                var sideLook = surface.GetPoint(front + 20f / world.MajorRadius, phi);
                Save(camera, target, worldTransform, sideEye, sideLook, surface.GetBaseNormal(sideTheta, sidePhi), Path.Combine(folder, $"storm_{shot}_side.png"));
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Save(Camera camera, RenderTexture target, Transform worldTransform, Vector3 localEye, Vector3 localLook, Vector3 localUp, string path)
        {
            var eye = worldTransform.TransformPoint(localEye);
            var look = worldTransform.TransformPoint(localLook);
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, worldTransform.TransformDirection(localUp)));
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

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, Private)?.Invoke(target, null);
        }
    }
}
