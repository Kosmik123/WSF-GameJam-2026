using UnityEngine;
using UnityEngine.Rendering;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Shared particle systems of the effects (port of the LD57 MageStylizedFx systems): one system per kind of particle,
    /// created on first use, kept between scenes, emitted by hand. There is no gravity, because "down" changes all over
    /// the torus: particles slow down by drag instead and callers give them velocities along the local up.
    /// </summary>
    internal static class VfxParticles
    {
        private enum Kind
        {
            Shard,
            Cube,
            Crystal,
            Soft,
            Decal,
            Smoke,
        }

        private const float ShardGlow = 3f;
        private const int KindCount = 6;

        private static readonly ParticleSystem[] systems = new ParticleSystem[KindCount];
        private static Material shardMaterial;

        private static Material ShardMaterial
        {
            get
            {
                if (shardMaterial == null)
                    shardMaterial = VfxMaterials.CreateTransparent("Vfx Shards (runtime)", new Color(ShardGlow, ShardGlow, ShardGlow, 1f));

                return shardMaterial;
            }
        }

        /// <summary>Tumbling glowing octahedron (sparks, splinters).</summary>
        public static void Shard(Vector3 position, Vector3 velocity, Color color, Vector3 size, float life)
        {
            Emit(Kind.Shard, position, velocity, color, size, life, Random.rotation.eulerAngles);
        }

        /// <summary>Tumbling cube (voxel debris, hit splashes).</summary>
        public static void Cube(Vector3 position, Vector3 velocity, Color color, float size, float life)
        {
            Emit(Kind.Cube, position, velocity, color, Vector3.one * size, life, Random.rotation.eulerAngles);
        }

        /// <summary>Still crystal stretched along its rotation (lightning segments).</summary>
        public static void Crystal(Vector3 position, Color color, Vector3 size, float life, Quaternion rotation)
        {
            Emit(Kind.Crystal, position, Vector3.zero, color, size, life, rotation.eulerAngles);
        }

        /// <summary>Soft blob facing the camera.</summary>
        public static void Soft(Vector3 position, Vector3 velocity, Color color, float size, float life)
        {
            Emit(Kind.Soft, position, velocity, color, Vector3.one * size, life, Vector3.zero);
        }

        /// <summary>Soft blob lying flat on the ground with the given up.</summary>
        public static void Decal(Vector3 position, Vector3 up, Vector3 velocity, Color color, float size, float life)
        {
            var rotation = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, Random.value * 360f, 0f);
            Emit(Kind.Decal, position, velocity, color, Vector3.one * size, life, rotation.eulerAngles);
        }

        /// <summary>Dark puff that grows and fades.</summary>
        public static void Smoke(Vector3 position, Vector3 velocity, Color color, float size, float life)
        {
            Emit(Kind.Smoke, position, velocity, color, Vector3.one * size, life, Random.rotation.eulerAngles);
        }

        private static void Emit(Kind kind, Vector3 position, Vector3 velocity, Color color, Vector3 size, float life, Vector3 rotation)
        {
            var system = GetSystem(kind);
            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startColor = color,
                startLifetime = Mathf.Max(0.05f, life),
            };

            if (kind == Kind.Soft)
            {
                parameters.startSize = size.x;
                parameters.rotation = Random.value * 360f;
            }

            if (kind != Kind.Soft)
            {
                parameters.startSize3D = size;
                parameters.rotation3D = rotation;
            }

            system.Emit(parameters, 1);
        }

        private static ParticleSystem GetSystem(Kind kind)
        {
            int index = (int)kind;
            if (systems[index])
                return systems[index];

            var systemObject = new GameObject($"Vfx Particles {kind}");
            Object.DontDestroyOnLoad(systemObject);
            var system = systemObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            bool isSoft = kind == Kind.Soft;

            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 4000;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.startSize3D = isSoft == false;
            main.startRotation3D = isSoft == false;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = CreateFade(kind == Kind.Smoke);

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, GetSizeCurve(kind));

            if (kind == Kind.Shard || kind == Kind.Cube || kind == Kind.Smoke)
            {
                float spin = kind == Kind.Smoke ? 1.2f : 5f;
                var rotationOverLifetime = system.rotationOverLifetime;
                rotationOverLifetime.enabled = true;
                rotationOverLifetime.separateAxes = true;
                rotationOverLifetime.x = new ParticleSystem.MinMaxCurve(-spin, spin);
                rotationOverLifetime.y = new ParticleSystem.MinMaxCurve(-spin, spin);
                rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-spin, spin);

                // Instead of gravity: thrown debris slows down by itself.
                var limitVelocity = system.limitVelocityOverLifetime;
                limitVelocity.enabled = true;
                limitVelocity.limit = new ParticleSystem.MinMaxCurve(1000f);
                limitVelocity.drag = new ParticleSystem.MinMaxCurve(kind == Kind.Smoke ? 1f : 2.5f);
                limitVelocity.multiplyDragByParticleSize = false;
                limitVelocity.multiplyDragByParticleVelocity = false;
            }

            var renderer = systemObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = isSoft ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Mesh;
            if (isSoft == false)
            {
                renderer.mesh = VfxMeshes.Get(GetShape(kind));
                renderer.alignment = ParticleSystemRenderSpace.World;
            }

            renderer.sharedMaterial = isSoft || kind == Kind.Decal ? VfxMaterials.Soft : ShardMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            systems[index] = system;
            return system;
        }

        private static VfxShape GetShape(Kind kind)
        {
            return kind switch
            {
                Kind.Shard => VfxShape.Octa,
                Kind.Cube => VfxShape.Cube,
                Kind.Crystal => VfxShape.CrystalForward,
                Kind.Smoke => VfxShape.Sphere,
                _ => VfxShape.Quad,
            };
        }

        private static AnimationCurve GetSizeCurve(Kind kind)
        {
            return kind switch
            {
                Kind.Soft => new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.7f)),
                Kind.Decal => new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.2f, 1f), new Keyframe(1f, 1f)),
                Kind.Smoke => AnimationCurve.Linear(0f, 0.5f, 1f, 1.7f),
                Kind.Crystal => new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1.05f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.4f)),
                _ => AnimationCurve.Linear(0f, 1f, 1f, 0.2f),
            };
        }

        private static Gradient CreateFade(bool isSmoke)
        {
            var gradient = new Gradient();
            var colors = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            var alphas = isSmoke
                ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.12f), new GradientAlphaKey(0.5f, 0.55f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) };
            gradient.SetKeys(colors, alphas);
            return gradient;
        }
    }
}
