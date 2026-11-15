using UnityEditor;
using UnityEngine;

namespace WSFGameJam2026.Editor
{
    /// <summary>
    /// The hero material (made by <see cref="HeroSetupMenu"/>) used the shared world shader "WSF/Vertex Color Lit".
    /// The heroes have their own shader now ("WSF/Hero Vertex Color": toon ramp, rim light, hit flash). This switches the
    /// existing material asset to it once, by itself, after the scripts are compiled. Safe to run again.
    /// </summary>
    [InitializeOnLoad]
    public static class HeroShaderAutoSetup
    {
        internal const string ShaderName = "WSF/Hero Vertex Color";
        private const string MaterialPath = "Assets/_Project/Materials/Player/Ququmatz Vertex Color.mat";
        private const string SessionKey = "WSFGameJam2026.HeroShaderAutoSetup.Done";

        static HeroShaderAutoSetup()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            // The editor is busy: try again a moment later.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            SessionState.SetBool(SessionKey, true);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material && Apply(material))
                AssetDatabase.SaveAssets();
        }

        /// <summary>Sets the hero shader on the material when it has another one. Returns true when it changed it.</summary>
        internal static bool Apply(Material material)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null || material.shader == shader)
                return false;

            material.shader = shader;
            if (material.HasProperty("_SrgbVertexColor"))
                material.SetFloat("_SrgbVertexColor", 1f);

            EditorUtility.SetDirty(material);
            Debug.Log("Hero shader auto setup: the material " + MaterialPath + " uses " + ShaderName + " now.");
            return true;
        }
    }
}
