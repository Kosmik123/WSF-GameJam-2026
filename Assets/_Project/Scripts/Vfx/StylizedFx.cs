using System;
using UnityEngine;
using WSFGameJam2026.Cameras;
using Random = UnityEngine.Random;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Stylized low poly effects built in code (port of the LD57 MageStylizedFx, SkillImpactVfx and VoxelFxRecipes, without
    /// DOTween and physics): bursts of shards and cubes, rings and glows on the ground, flashes, lightning, ice, smoke, orbs,
    /// arcs of the sword and the spin, shockwaves. On the torus "up" differs at every spot, so every effect that has a
    /// direction takes it explicitly (see <see cref="VfxGround"/> for the ground under a point). Every effect removes itself.
    /// </summary>
    public static class StylizedFx
    {
        public static readonly Color HotWhite = new Color(1f, 0.92f, 0.75f);
        public static readonly Color Orange = new Color(1f, 0.5f, 0.15f);
        public static readonly Color Gold = new Color(1f, 0.75f, 0.25f);
        public static readonly Color Red = new Color(1f, 0.2f, 0.1f);
        public static readonly Color Rock = new Color(0.32f, 0.27f, 0.23f);
        public static readonly Color SmokeGray = new Color(0.12f, 0.11f, 0.11f);

        private static TorusFollowCamera followCamera;

        // ------------------------------------------------------------ bursts and sparks

        /// <summary>Shards (octahedra) flying out in all directions, a bit more along the up.</summary>
        public static void Burst(Vector3 position, Vector3 up, Color color, int count = 10, float size = 0.18f, float speed = 5f, float life = 0.5f)
        {
            for (int i = 0; i < count; i++)
            {
                var direction = UpperHemisphere(up, 0.8f, 0.2f);
                float shardSize = size * Random.Range(0.6f, 1.3f);
                VfxParticles.Shard(position + direction * 0.1f, direction * speed * Random.Range(0.5f, 1.1f), Tint(color, 0.3f),
                    new Vector3(shardSize, shardSize * Random.Range(0.8f, 1.6f), shardSize), life * Random.Range(0.7f, 1.2f));
            }
        }

        /// <summary>Voxel cubes flying out, pushed along <paramref name="push"/> (the direction of a hit). Hit splashes, debris.</summary>
        public static void CubeBurst(Vector3 position, Vector3 up, Color color, int count, float size, float speed, float life, Vector3 push = default)
        {
            for (int i = 0; i < count; i++)
            {
                var direction = (UpperHemisphere(up, 0.8f, 0.3f) + push * 0.8f).normalized;
                VfxParticles.Cube(position + direction * 0.15f, direction * speed * Random.Range(0.5f, 1f), Tint(color, 0.15f),
                    size * Random.Range(0.6f, 1.3f), life * Random.Range(0.7f, 1.2f));
            }
        }

        /// <summary>Small bright solid flying back (and up) and slowing down.</summary>
        public static void Spark(Vector3 position, Vector3 back, Vector3 up, Color color)
        {
            var velocity = back * Random.Range(1.2f, 3.2f) + up * Random.Range(1f, 2.5f) + Random.insideUnitSphere * 0.8f;
            float size = Random.Range(0.05f, 0.1f);
            VfxParticles.Shard(position + Random.insideUnitSphere * 0.08f, velocity, Tint(color, 0.5f), new Vector3(size, size * 1.4f, size),
                Random.Range(0.4f, 0.7f));
        }

        /// <summary>Spark of fire, from white-yellow to orange.</summary>
        public static void FireSpark(Vector3 position, Vector3 back, Vector3 up)
        {
            Spark(position, back, up, Color.Lerp(new Color(1f, 0.95f, 0.55f), new Color(1f, 0.45f, 0.1f), Random.value));
        }

        /// <summary>Dark puff of smoke that grows and rises along the up.</summary>
        public static void SmokePuff(Vector3 position, Vector3 up, float rise = 0.6f, float size = 0.36f)
        {
            float gray = Random.Range(0.07f, 0.14f);
            VfxParticles.Smoke(position + Random.insideUnitSphere * 0.12f, up * rise * Random.Range(0.7f, 1.4f) + Random.insideUnitSphere * 0.25f,
                new Color(gray, gray * 0.95f, gray * 0.95f, 1f), size * Random.Range(0.8f, 1.25f), Random.Range(1.2f, 1.8f));
        }

        /// <summary>Shards flying into a point (implosion).</summary>
        public static void Implode(Vector3 center, Vector3 up, Color color, int count, float radius, float life)
        {
            for (int i = 0; i < count; i++)
            {
                var direction = Random.onUnitSphere;
                direction -= up * Vector3.Dot(direction, up) * 0.4f;
                var start = center + direction.normalized * radius * Random.Range(0.6f, 1f);
                float size = Random.Range(0.12f, 0.24f);
                VfxParticles.Shard(start, (center - start) / life * 1.4f, Tint(color, 0.3f), new Vector3(size, size * 1.5f, size), life);
            }
        }

        /// <summary>A body falls apart: shards from its whole height fly out to the sides and up.</summary>
        public static void Dissolve(Vector3 feet, Vector3 up, Color color, int count, float height = 1.7f)
        {
            VfxGround.GetTangents(up, out var right, out var forward);
            var center = feet + up * height * 0.5f;
            for (int i = 0; i < count; i++)
            {
                var disc = Random.insideUnitCircle * 0.3f;
                var position = feet + right * disc.x + forward * disc.y + up * Random.Range(0.1f, height);
                var outward = Vector3.ProjectOnPlane(position - center, up);
                var velocity = outward.normalized * Random.Range(1f, 2.6f) + up * Random.Range(0.4f, 1.6f);
                float size = Random.Range(0.1f, 0.18f);
                VfxParticles.Shard(position, velocity, Tint(color, 0.3f), new Vector3(size, size * 1.5f, size), Random.Range(0.55f, 0.9f));
            }
        }

        /// <summary>Cubes rising in a column from the ground (war cry, ground slam).</summary>
        public static void Column(Vector3 ground, Vector3 up, float height, Color color, int count)
        {
            VfxGround.GetTangents(up, out var right, out var forward);
            for (int i = 0; i < count; i++)
            {
                var disc = Random.insideUnitCircle * 0.5f;
                var position = ground + right * disc.x + forward * disc.y + up * Random.Range(0f, 0.4f);
                VfxParticles.Cube(position, up * Random.Range(height * 0.8f, height * 1.6f) + (right * disc.x + forward * disc.y) * 0.6f,
                    Tint(color, 0.2f), Random.Range(0.1f, 0.22f), Random.Range(0.45f, 0.8f));
            }
        }

        // ------------------------------------------------------------ rings and the ground

        /// <summary>Ring of soft blobs spreading on the ground.</summary>
        public static void Ring(Vector3 ground, Vector3 up, float radius, Color color, int count = 24, float life = 0.5f)
        {
            VfxGround.GetTangents(up, out var right, out var forward);
            float blob = Mathf.Clamp(radius * 0.32f, 0.35f, 1.1f);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value * 0.5f) / count * Mathf.PI * 2f;
                var direction = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                float speed = radius * 0.85f / life * Random.Range(0.92f, 1.08f);
                VfxParticles.Soft(ground + direction * radius * 0.15f + up * 0.15f, direction * speed, Tint(color, 0.35f),
                    blob * Random.Range(0.8f, 1.2f), life);
            }
        }

        /// <summary>Ring of cubes spreading on the ground (voxel style of the warrior).</summary>
        public static void CubeRing(Vector3 ground, Vector3 up, float radius, Color color, float life = 0.4f, int count = 28)
        {
            VfxGround.GetTangents(up, out var right, out var forward);
            for (int i = 0; i < count; i++)
            {
                float angle = (i + Random.value * 0.4f) / count * Mathf.PI * 2f;
                var direction = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                VfxParticles.Cube(ground + direction * radius * 0.2f + up * 0.15f, direction * radius * 0.9f / life + up * Random.Range(0f, 1f),
                    Tint(color, 0.25f), Random.Range(0.12f, 0.22f), life * Random.Range(0.85f, 1.15f));
            }
        }

        /// <summary>Glowing spot on the ground: a soft decal, rising soft blobs and a light, for <paramref name="seconds"/>.</summary>
        public static void GroundGlow(Vector3 ground, Vector3 up, float radius, Color color, float seconds, float rise = 0.4f)
        {
            var glowObject = new GameObject("Vfx Ground Glow");
            glowObject.transform.position = ground;
            glowObject.AddComponent<VfxGroundGlow>().Play(up, radius, color, seconds, rise);
        }

        /// <summary>Dark burnt spot on the ground that stays for a while and fades.</summary>
        public static void Scorch(Vector3 ground, Vector3 up, float radius, float seconds = 6f)
        {
            VfxParticles.Decal(ground + up * 0.03f, up, Vector3.zero, new Color(0.04f, 0.03f, 0.03f), radius * 2.2f, seconds);
        }

        /// <summary>Embers on the ground: a soft fading orange spot.</summary>
        public static void Ember(Vector3 ground, Vector3 up, float radius, float seconds)
        {
            VfxParticles.Decal(ground + up * 0.05f, up, Vector3.zero, new Color(1f, 0.4f, 0.08f), radius * 2f, seconds);
        }

        /// <summary>Puddle of a color on the ground (remains of an enemy).</summary>
        public static void Puddle(Vector3 ground, Vector3 up, float radius, Color color, float seconds)
        {
            VfxParticles.Decal(ground + up * 0.04f, up, Vector3.zero, color, radius * 2f, seconds);
        }

        /// <summary>
        /// Shockwave of a blow into the ground (port of the LD57 SkillImpactVfx.Shockwave): two growing rings, a dark crack mark,
        /// dust thrown out, a ring of cubes, rock debris and a camera shake.
        /// </summary>
        public static void Shockwave(Vector3 ground, Vector3 up, float radius, Color color, float shake = 0.3f)
        {
            var lifted = ground + up * 0.06f;
            GrowingRing(lifted, up, radius * 1.25f, color, 0.5f, 0.35f);
            GrowingRing(lifted, up, radius * 0.6f, Color.Lerp(color, Color.white, 0.6f), 0.35f, 0.2f);
            Scorch(ground, up, radius * 0.7f, 1.6f);
            Dust(ground, up, radius, 20);
            CubeRing(lifted, up, radius, color, 0.45f);
            CubeBurst(lifted + up * 0.2f, up, Rock, 10, 0.18f, 5f, 0.6f);
            Shake(shake, 0.3f);
        }

        /// <summary>Thin ring lying on the ground, growing to the given radius and fading.</summary>
        public static void GrowingRing(Vector3 ground, Vector3 up, float radius, Color color, float alpha, float seconds)
        {
            var ringObject = CreateRenderer("Vfx Shockwave Ring", VfxShape.Quad, VfxMaterials.Ring, out var ringRenderer);
            ringObject.transform.SetPositionAndRotation(ground, Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0f, Random.value * 360f, 0f));
            ringObject.AddComponent<VfxFade>().Play(ringRenderer, color, alpha, 2f, Vector3.one * 0.3f, Vector3.one * radius * 2f, seconds, seconds);
        }

        // ------------------------------------------------------------ light and energy

        /// <summary>Big bright flash (glowing low poly sphere) with a point light.</summary>
        public static void Flash(Vector3 position, Color color, float size = 1.2f, float duration = 0.18f)
        {
            new GameObject("Vfx Flash").AddComponent<VfxFlash>().Play(position, color, size, duration);
        }

        /// <summary>Lightning between two points: a zigzag of crystals and a flickering line.</summary>
        public static void Bolt(Vector3 from, Vector3 to, Vector3 up, Color color, float thickness = 0.3f, float seconds = 0.2f)
        {
            new GameObject("Vfx Bolt").AddComponent<VfxBolt>().Play(from, to, up, color, thickness, seconds);
        }

        /// <summary>Line of soft blobs fading after <paramref name="life"/> seconds (the path of a teleport).</summary>
        public static void Line(Vector3 from, Vector3 to, Color color, int count, float life)
        {
            for (int i = 0; i < count; i++)
            {
                float progress = (i + 0.5f) / count;
                VfxParticles.Soft(Vector3.Lerp(from, to, progress) + Random.insideUnitSphere * 0.08f, Vector3.zero, Tint(color, 0.4f),
                    Random.Range(0.35f, 0.55f), life);
            }
        }

        /// <summary>Sparks jumping around a body hit by lightning.</summary>
        public static void Electrify(Transform target, Color color, float seconds = 0.4f)
        {
            if (target == null)
                return;

            new GameObject("Vfx Electrify").AddComponent<VfxElectrify>().Play(target, color, seconds);
        }

        /// <summary>Ice crystals growing from the ground, standing and shattering, with a frosty mist.</summary>
        public static void IceGrowth(Vector3 ground, Vector3 up, float radius, Color color, int count = 16, int mist = 8)
        {
            var iceObject = new GameObject("Vfx Ice Growth");
            iceObject.transform.position = ground;
            iceObject.AddComponent<VfxIceSpikes>().Play(up, radius, color, count, mist);
        }

        /// <summary>
        /// Orb: a glowing sphere with a crystal inside, two spinning rings, an aura of small solids, a light and a shard trail.
        /// <see cref="PulseOrb"/> makes it pulse.
        /// </summary>
        public static GameObject OrbVisual(Transform parent, Color color, float size = 0.34f)
        {
            var root = new GameObject("Orb Visual");
            root.transform.SetParent(parent, false);
            var body = new GameObject("Orb Body");
            body.transform.SetParent(root.transform, false);
            root.AddComponent<VfxOrbPulse>().Init(body.transform);

            MakeSpinningShape(body.transform, VfxShape.Sphere, color, size, 0.9f, 3.2f, new Vector3(40f, 90f, 0f));
            MakeSpinningShape(body.transform, VfxShape.Octa, Color.Lerp(color, Color.white, 0.8f), size * 0.45f, 1f, 4.5f, new Vector3(-200f, 300f, 120f));
            var outerRing = MakeSpinningShape(body.transform, VfxShape.Torus, Color.Lerp(color, Color.white, 0.3f), size * 2.1f, 1f, 3f, new Vector3(0f, 200f, 0f));
            outerRing.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            var innerRing = MakeSpinningShape(body.transform, VfxShape.Torus, color, size * 1.7f, 1f, 3f, new Vector3(0f, -260f, 0f));
            innerRing.transform.localRotation = Quaternion.Euler(-50f, 0f, 70f);

            var aura = new GameObject("Orb Aura");
            aura.transform.SetParent(body.transform, false);
            aura.transform.localRotation = Quaternion.Euler(20f, 0f, 15f);
            aura.AddComponent<VfxSpin>().Init(new Vector3(0f, 420f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i / 4f * Mathf.PI * 2f;
                var shape = i % 2 == 0 ? VfxShape.Octa : VfxShape.Tetra;
                var bit = MakeShape(aura.transform, "Aura", shape, Color.Lerp(color, Color.white, 0.5f), size * 0.3f, 0.95f, 3.5f);
                bit.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle * 2f) * 0.2f, Mathf.Sin(angle)) * size * 1.15f;
                bit.AddComponent<VfxSpin>().Init(Random.onUnitSphere * 400f);
            }

            AddLight(root, color, 1.6f, 3f);
            AddTrail(root, color, size * 0.5f, 0.5f, false);
            return root;
        }

        /// <summary>Pulse of an orb made by <see cref="OrbVisual"/> (its body grows and shrinks back).</summary>
        public static void PulseOrb(Transform orb)
        {
            if (orb == null)
                return;

            var pulse = orb.GetComponentInChildren<VfxOrbPulse>();
            if (pulse)
                pulse.Pulse();
        }

        // ------------------------------------------------------------ arcs

        /// <summary>
        /// Arc of a spinning blade (port of the LD57 SkillImpactVfx.SpinArc): a 300 degree fading ring sector attached to the
        /// caster, turning fast for <paramref name="duration"/>, with a ring on the ground at the start.
        /// </summary>
        public static void SpinArc(Transform origin, Vector3 ground, float radius, float duration, Color color, float height = 0.8f)
        {
            if (origin == null)
                return;

            var arcObject = CreateArc(origin, "Vfx Spin Arc", 300f, 0.5f, height, out var arcRenderer);
            var scale = new Vector3(radius, 1f, radius);
            arcObject.AddComponent<VfxFade>().Play(arcRenderer, color, 1f, 2.5f, scale, scale, duration, 0.25f, 900f);
            GrowingRing(ground + origin.up * 0.05f, origin.up, radius * 0.9f, color, 0.35f, 0.25f);
        }

        /// <summary>
        /// Swing of a sword (cheap port of the LD57 SwordTrail and SmearController): a fading ring sector in front of the caster
        /// that sweeps across the swing, with cubes flying along it. A mirrored arc sweeps the other way (the next combo step).
        /// </summary>
        public static void SwordArc(Transform origin, float radius, float arcDegrees, bool isMirrored, Color color, float duration, float height = 0.8f)
        {
            if (origin == null)
                return;

            const float Sweep = 50f;
            var arcObject = CreateArc(origin, "Vfx Sword Arc", arcDegrees, 0.45f, height, out var arcRenderer);
            float side = isMirrored ? -1f : 1f;
            arcObject.transform.localRotation = Quaternion.Euler(0f, -Sweep * 0.5f * side, 0f);
            var scale = new Vector3(radius * side, 1f, radius);
            arcObject.AddComponent<VfxFade>().Play(arcRenderer, color, 0.9f, 2.5f, scale * 0.85f, scale, duration, duration * 0.7f, Sweep / duration * side);

            const int CubeCount = 10;
            var center = origin.position + origin.up * height;
            for (int i = 0; i < CubeCount; i++)
            {
                float angle = Mathf.Lerp(-arcDegrees * 0.5f, arcDegrees * 0.5f, i / (float)(CubeCount - 1));
                var direction = Quaternion.AngleAxis(angle, origin.up) * origin.forward;
                var tangent = Vector3.Cross(origin.up, direction) * side;
                var cubeColor = i % 3 == 0 ? HotWhite : color;
                VfxParticles.Cube(center + direction * radius * Random.Range(0.75f, 0.95f) + origin.up * Random.Range(-0.1f, 0.15f),
                    tangent * Random.Range(3f, 5f) + direction * 0.8f, cubeColor, Random.Range(0.08f, 0.16f), 0.22f);
            }
        }

        // ------------------------------------------------------------ helpers

        /// <summary>Calls <paramref name="tick"/> with the progress (0-1) every <paramref name="interval"/> seconds for <paramref name="duration"/> seconds.</summary>
        public static void Repeat(float duration, float interval, Action<float> tick)
        {
            new GameObject("Vfx Repeater").AddComponent<VfxRepeater>().Play(duration, interval, tick);
        }

        /// <summary>Adds (or sets up again) a shard trail. A positive <paramref name="seconds"/> removes it after that time.</summary>
        public static VfxShardTrail AddTrail(GameObject target, Color color, float size, float life, bool isEmber, float height = 0f, float seconds = 0f)
        {
            if (target.TryGetComponent<VfxShardTrail>(out var trail) == false)
                trail = target.AddComponent<VfxShardTrail>();

            trail.Configure(color, size, life, isEmber, height, seconds);
            return trail;
        }

        /// <summary>Shakes the follow camera, when there is one.</summary>
        public static void Shake(float amplitude, float duration)
        {
            if (followCamera == null)
                followCamera = UnityEngine.Object.FindAnyObjectByType<TorusFollowCamera>();

            if (followCamera)
                followCamera.Shake(amplitude, duration);
        }

        /// <summary>Thrown axe built from solids: a handle and a two-sided head, spinning around the local up.</summary>
        public static GameObject AxeVisual(Transform parent, Color glow)
        {
            var root = new GameObject("Axe Visual");
            root.transform.SetParent(parent, false);
            var spinner = new GameObject("Axe Spin");
            spinner.transform.SetParent(root.transform, false);
            spinner.AddComponent<VfxSpin>().Init(new Vector3(0f, 720f, 0f));

            var handle = MakeShape(spinner.transform, "Handle", VfxShape.Cube, new Color(0.45f, 0.3f, 0.18f), 1f, 1f, 1f);
            handle.transform.localScale = new Vector3(0.08f, 0.08f, 0.8f);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                var blade = MakeShape(spinner.transform, "Blade", VfxShape.Tetra, new Color(0.85f, 0.88f, 0.95f), 1f, 1f, 1.6f);
                blade.transform.localPosition = new Vector3(0.16f * side, 0f, 0.3f);
                blade.transform.localScale = new Vector3(0.3f, 0.06f, 0.3f);
            }

            AddLight(root, glow, 1.2f, 2.5f);
            return root;
        }

        /// <summary>Low poly solid with a glowing color (<paramref name="intensity"/> multiplies the RGB), without a collider.</summary>
        public static GameObject MakeShape(Transform parent, string name, VfxShape shape, Color color, float size, float alpha, float intensity)
        {
            var shapeObject = CreateRenderer(name, shape, VfxMaterials.Solid, out var shapeRenderer);
            shapeObject.transform.SetParent(parent, false);
            shapeObject.transform.localScale = Vector3.one * size;
            VfxMaterials.SetColor(shapeRenderer, color, alpha, intensity);
            return shapeObject;
        }

        /// <summary>The color with a random amount of white and full alpha (variety of particles).</summary>
        public static Color Tint(Color color, float whiten)
        {
            var tinted = Color.Lerp(color, Color.white, Random.value * whiten);
            tinted.a = 1f;
            return tinted;
        }

        private static Vector3 UpperHemisphere(Vector3 up, float upScale, float upBias)
        {
            var direction = Random.onUnitSphere;
            float along = Vector3.Dot(direction, up);
            direction += up * (Mathf.Abs(along) * upScale + upBias - along);
            return direction.normalized;
        }

        private static void Dust(Vector3 ground, Vector3 up, float radius, int count)
        {
            VfxGround.GetTangents(up, out var right, out var forward);
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var direction = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                var color = Color.Lerp(new Color(0.45f, 0.38f, 0.3f), new Color(0.3f, 0.25f, 0.2f), Random.value);
                VfxParticles.Smoke(ground + direction * 0.3f + up * 0.1f, direction * Random.Range(radius * 1.2f, radius * 2.4f) + up * Random.Range(0.5f, 2f),
                    color, Random.Range(0.25f, 0.55f), Random.Range(0.6f, 1f));
            }
        }

        private static GameObject CreateArc(Transform origin, string name, float spanDegrees, float innerRadius, float height, out MeshRenderer arcRenderer)
        {
            var arcObject = new GameObject(name);
            arcObject.transform.SetParent(origin, false);
            arcObject.transform.localPosition = Vector3.up * height;
            arcObject.AddComponent<MeshFilter>().sharedMesh = VfxMeshes.GetArc(spanDegrees, innerRadius);
            arcRenderer = arcObject.AddComponent<MeshRenderer>();
            VfxMaterials.Prepare(arcRenderer, VfxMaterials.Solid);
            return arcObject;
        }

        private static GameObject CreateRenderer(string name, VfxShape shape, Material material, out MeshRenderer meshRenderer)
        {
            var rendererObject = new GameObject(name);
            rendererObject.AddComponent<MeshFilter>().sharedMesh = VfxMeshes.Get(shape);
            meshRenderer = rendererObject.AddComponent<MeshRenderer>();
            VfxMaterials.Prepare(meshRenderer, material);
            return rendererObject;
        }

        private static GameObject MakeSpinningShape(Transform parent, VfxShape shape, Color color, float size, float alpha, float intensity, Vector3 spin)
        {
            var shapeObject = MakeShape(parent, "Orb Part", shape, color, size, alpha, intensity);
            shapeObject.AddComponent<VfxSpin>().Init(spin);
            return shapeObject;
        }

        private static void AddLight(GameObject target, Color color, float intensity, float range)
        {
            var light = target.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }
    }
}
