using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace WSFGameJam2026.Editor.AztecUi
{
    /// <summary>
    /// Fonts of the design: Cinzel for titles and numbers, Inter for the rest (both SIL Open Font License).
    /// Put their TTF files in <see cref="Folder"/>; a TextMeshPro font asset is made for each on the first run.
    /// Without the files the default TextMeshPro font is used and the console says which file is missing.
    /// </summary>
    internal static class AztecUiFonts
    {
        public const string Folder = "Assets/_Project/Ui/Fonts";

        public static void Load()
        {
            UiKit.HeadingFont = FindOrCreate("Cinzel");
            UiKit.BodyFont = FindOrCreate("Inter");
        }

        private static TMP_FontAsset FindOrCreate(string family)
        {
            string assetPath = $"{Folder}/{family} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing)
                return existing;

            var font = FindFont(family);
            if (font == null)
            {
                Debug.LogWarning($"Aztec UI: no {family} font in {Folder} (download it from Google Fonts, SIL Open Font License). The default font is used.");
                return null;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fontAsset.name = $"{family} SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);

            fontAsset.material.name = $"{family} SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            foreach (var atlas in fontAsset.atlasTextures)
            {
                atlas.name = $"{family} SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        // The bold static file first, then any file of the family (a variable font).
        private static Font FindFont(string family)
        {
            if (AssetDatabase.IsValidFolder(Folder) == false)
                return null;

            Font fallback = null;
            foreach (string guid in AssetDatabase.FindAssets("t:Font", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.StartsWith(family, System.StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                var font = AssetDatabase.LoadAssetAtPath<Font>(path);
                if (fileName.IndexOf("Bold", System.StringComparison.OrdinalIgnoreCase) >= 0 && family == "Cinzel")
                    return font;

                if (fallback == null || fileName.IndexOf("Regular", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    fallback = font;
            }

            return fallback;
        }
    }
}
