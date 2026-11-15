using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WSFGameJam2026.Vfx;

namespace WSFGameJam2026.Skills
{
    /// <summary>
    /// Simple effects of skills and enemies on top of the stylized effects of <see cref="StylizedFx"/>: rings on the ground,
    /// lightning through points, glowing orbs. The ground under a point is found on the torus (<see cref="VfxGround"/>).
    /// Richer, skill specific effects call <see cref="StylizedFx"/> directly. Every effect removes itself.
    /// </summary>
    public static class SkillVfx
    {
        private const int RingSegments = 40;
        private const float RingLift = 0.08f;

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

        /// <summary>Circle on the ground under <paramref name="center"/>: a thin line and a ring of soft light spreading out, which fade.</summary>
        public static void Ring(Vector3 center, Vector3 up, float radius, Color color, float duration = 0.4f, float width = 0.15f)
        {
            var ground = VfxGround.Below(center, out var groundUp);
            if (Vector3.Dot(groundUp, up) < 0.5f)
                groundUp = up;

            var line = CreateLine("Skill Ring", color, width, duration);
            line.loop = true;
            line.positionCount = RingSegments;

            var lifted = ground + groundUp * RingLift;
            VfxGround.GetTangents(groundUp, out var right, out var forward);
            for (int i = 0; i < RingSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / RingSegments;
                line.SetPosition(i, lifted + (right * Mathf.Cos(angle) + forward * Mathf.Sin(angle)) * radius);
            }

            int blobs = Mathf.Clamp(Mathf.RoundToInt(radius * 8f), 10, 36);
            StylizedFx.Ring(ground, groundUp, radius, color, blobs, Mathf.Max(0.25f, duration));
        }

        /// <summary>Lightning through the points (crystals and a flickering line on every segment), with a flash at the end.</summary>
        public static void Bolt(IReadOnlyList<Vector3> points, Vector3 up, Color color, float duration = 0.25f, float width = 0.18f)
        {
            if (points.Count < 2)
                return;

            for (int i = 0; i < points.Count - 1; i++)
                StylizedFx.Bolt(points[i], points[i + 1], up, color, width * 2f, duration);

            StylizedFx.Flash(points[points.Count - 1], color, 0.6f, 0.12f);
        }

        /// <summary>Glowing orb (sphere, crystal, rings, aura, light, trail), removed after <paramref name="lifetime"/> seconds (also when its owner stops).</summary>
        public static Transform Orb(Color color, float size, float lifetime)
        {
            var orb = new GameObject("Skill Orb");
            StylizedFx.OrbVisual(orb.transform, color, size);
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
