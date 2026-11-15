using TMPro;
using UnityEditor;
using UnityEngine;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// Gives the game name in the main menu ("Title" in the Menu Canvas prefab) the Papakilo Light font. Only that text changes.
    /// Creates the TextMeshPro font asset next to the TTF when it is missing. Safe to run again.
    /// </summary>
    public static class TitleFontSetupMenu
    {
        private const string FontPath = "Assets/_Project/Ui/Fonts/PapakiloLight.ttf";
        private const string FontAssetPath = "Assets/_Project/Ui/Fonts/PapakiloLight SDF.asset";
        private const string MenuCanvasPath = "Assets/_Project/Prefabs/Menu Canvas.prefab";
        private const string TitleName = "Title";

        [MenuItem("WSF GameJam/Setup/Apply Title Font")]
        public static void Apply()
        {
            var fontAsset = GetOrCreateFontAsset();
            if (fontAsset == null)
            {
                Debug.LogError("Title font setup: the font " + FontPath + " is missing.");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(MenuCanvasPath);
            try
            {
                var title = FindTitle(root);
                if (title == null)
                {
                    Debug.LogError("Title font setup: no \"" + TitleName + "\" text in " + MenuCanvasPath + ".");
                    return;
                }

                title.font = fontAsset;
                title.fontSharedMaterial = fontAsset.material;
                PrefabUtility.SaveAsPrefabAsset(root, MenuCanvasPath);
                Debug.Log("Title font setup: the game name uses " + fontAsset.name + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static TMP_FontAsset GetOrCreateFontAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
                return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null)
                return null;

            // Static atlas with the letters of the title (and the rest of the basic Latin set), so it works in the build.
            var fontAsset = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            fontAsset.name = "PapakiloLight SDF";
            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            foreach (var texture in fontAsset.atlasTextures)
            {
                texture.name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(texture, fontAsset);
            }

            const string characters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";
            fontAsset.TryAddCharacters(characters);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(fontAsset);
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static TextMeshProUGUI FindTitle(GameObject root)
        {
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.name == TitleName)
                    return text;
            }

            return null;
        }
    }
}
