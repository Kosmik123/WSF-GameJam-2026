using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WSFGameJam2026.Skills
{
    /// <summary>
    /// Placeholder effects of the skills built at runtime from line renderers and spheres (instead of the 1200-line
    /// LD57 MageStylizedFx): rings on the ground, zigzag lightning, glowing orbs. Every effect removes itself.
    /// To be replaced by real effects; listen to <see cref="SkillUser.OnSkillExecuted"/> for that.
    /// </summary>
    public static class SkillVfx
    {
        private const int RingSegments = 40;
        private const float GroundDepth = 0.8f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static Material material;

        private static Material Material
        {
            get
            {
                // Sprites/Default is always in the build, unlit and uses vertex colors of line renderers.
                if (material == null)
                    material = new Material(Shader.Find("Sprites/Default")) { name = "Skill Vfx (runtime)" };

                return material;
            }
        }

        /// <summary>Flat circle on the ground under <paramref name="center"/>, which fades out.</summary>
        public static void Ring(Vector3 center, Vector3 up, float radius, Color color, float duration = 0.4f, float width = 0.15f)
        {
            var line = CreateLine("Skill Ring", color, width, duration);
            line.loop = true;
            line.positionCount = RingSegments;

            var ground = center - up * GroundDepth;
            var right = Vector3.Cross(up, Mathf.Abs(Vector3.Dot(up, Vector3.forward)) > 0.9f ? Vector3.right : Vector3.forward).normalized;
            var forward = Vector3.Cross(right, up);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                line.SetPosition(i, ground + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius);
            }
        }

        /// <summary>Zigzag lightning through the points, which fades out.</summary>
        public static void Bolt(IReadOnlyList<Vector3> points, Vector3 up, Color color, float duration = 0.25f, float width = 0.18f)
        {
            if (points.Count < 2)
                return;

            var path = new List<Vector3>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                var start = points[i];
                var end = points[i + 1];
                int segments = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(start, end) / 0.6f));
                var side = Vector3.Cross((end - start).normalized, up).normalized;
                for (int s = 0; s < segments; s++)
                {
                    var point = Vector3.Lerp(start, end, s / (float)segments);
                    if (s > 0)
                    {
                        point += side * Random.Range(-0.25f, 0.25f) + up * Random.Range(-0.15f, 0.15f);
                    }

                    path.Add(point);
                }
            }

            path.Add(points[points.Count - 1]);

            var line = CreateLine("Skill Bolt", color, width, duration);
            line.positionCount = path.Count;
            line.SetPositions(path.ToArray());
        }

        /// <summary>Glowing sphere without a collider, removed after <paramref name="lifetime"/> seconds (also when its owner stops).</summary>
        public static Transform Orb(Color color, float size, float lifetime)
        {
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Skill Orb";
            Object.Destroy(orb.GetComponent<Collider>());
            orb.transform.localScale = Vector3.one * size;

            var renderer = orb.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);

            Object.Destroy(orb, lifetime);
            return orb.transform;
        }

        private static LineRenderer CreateLine(string name, Color color, float width, float duration)
        {
            var lineObject = new GameObject(name);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = Material;
            line.useWorldSpace = true;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.startColor = color;
            line.endColor = color;

            var lifetime = lineObject.AddComponent<SkillVfxLifetime>();
            lifetime.Play(line, width, duration);
            return line;
        }
    }
}
