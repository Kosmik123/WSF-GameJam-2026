using UnityEngine;
using UnityEngine.Rendering;

namespace WSFGameJam2026.Vfx
{
    /// <summary>
    /// Runtime materials of the effects (port of the LD57 SwordTrail.CreateTransparentMaterial): URP Particles/Unlit,
    /// transparent, double sided, multiplied by the vertex color. The color of a solid goes through a MaterialPropertyBlock,
    /// so one material serves all of them. Colors above 1 glow when the camera has bloom.
    /// </summary>
    public static class VfxMaterials
    {
        private const int SoftTextureSize = 64;
        private const int RingTextureSize = 128;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private static Material solid;
        private static Material soft;
        private static Material ring;
        private static Material line;
        private static Texture2D softTexture;
        private static Texture2D ringTexture;
        private static MaterialPropertyBlock block;

        /// <summary>Material of solids, arcs and the shard particles.</summary>
        public static Material Solid
        {
            get
            {
                if (solid == null)
                    solid = CreateTransparent("Vfx Solid (runtime)", Color.white);

                return solid;
            }
        }

        /// <summary>Soft round blob, for glows, rings of light and scorch marks.</summary>
        public static Material Soft
        {
            get
            {
                if (soft == null)
                    soft = CreateTransparent("Vfx Soft (runtime)", new Color(2.5f, 2.5f, 2.5f, 1f), SoftTexture);

                return soft;
            }
        }

        /// <summary>Thin ring with soft edges, for shockwaves.</summary>
        public static Material Ring
        {
            get
            {
                if (ring == null)
                    ring = CreateTransparent("Vfx Ring (runtime)", Color.white, RingTexture);

                return ring;
            }
        }

        /// <summary>Bright material of line renderers (lightning).</summary>
        public static Material Line
        {
            get
            {
                if (line == null)
                    line = CreateTransparent("Vfx Line (runtime)", new Color(3f, 3f, 3f, 1f));

                return line;
            }
        }

        private static Texture2D SoftTexture
        {
            get
            {
                if (softTexture == null)
                    softTexture = CreateRadialTexture("Vfx Soft (runtime)", SoftTextureSize, distance => SmoothStep(1f - distance));

                return softTexture;
            }
        }

        private static Texture2D RingTexture
        {
            get
            {
                if (ringTexture == null)
                    ringTexture = CreateRadialTexture("Vfx Ring (runtime)", RingTextureSize, distance => SmoothStep(1f - Mathf.Abs(distance - 0.85f) / 0.13f));

                return ringTexture;
            }
        }

        public static Material CreateTransparent(string name, Color color, Texture texture = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var material = new Material(shader) { name = name };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor(BaseColorId, color);
            material.SetColor(ColorId, color);
            if (texture)
            {
                material.SetTexture(BaseMapId, texture);
                material.SetTexture(MainTexId, texture);
            }

            return material;
        }

        /// <summary>Color of one renderer of an effect. <paramref name="intensity"/> multiplies the RGB (glow).</summary>
        public static void SetColor(Renderer renderer, Color color, float alpha, float intensity)
        {
            if (block == null)
                block = new MaterialPropertyBlock();

            var glowing = new Color(color.r * intensity, color.g * intensity, color.b * intensity, alpha);
            block.Clear();
            block.SetColor(BaseColorId, glowing);
            block.SetColor(ColorId, glowing);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Renderer without shadows, for effect objects.</summary>
        public static void Prepare(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static float SmoothStep(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * (3f - 2f * clamped);
        }

        private static Texture2D CreateRadialTexture(string name, int size, System.Func<float, float> alphaOfDistance)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var center = new Vector2(size * 0.5f, size * 0.5f);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size * 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alphaOfDistance(distance));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
