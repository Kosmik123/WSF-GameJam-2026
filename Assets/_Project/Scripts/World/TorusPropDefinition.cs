using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Settings of one class of biome props (all variants of a mesh, so <c>Las_Drzewo_v0</c>...<c>_v3</c> share one definition named <c>Las_Drzewo</c>).
    /// The catalog finds the definition by the mesh name. Props without a definition use the defaults (no blocking, never hidden).
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Prop Definition")]
    public class TorusPropDefinition : ScriptableObject
    {
        [Tooltip("Name of the mesh without the variant suffix (_v0, _v1...). The key the catalog looks the prop up by.")]
        [SerializeField]
        private string meshName;
        public string MeshName => meshName;

        [Tooltip("Radius of the area around the prop that can not be entered, in world units. 0 = can be walked through.")]
        [SerializeField]
        [Min(0f)]
        private float blockRadius;
        public float BlockRadius => blockRadius;

        [Tooltip("Hide the prop when it is further from the camera than the distance below. For small props such as grass. Trees stay visible far away.")]
        [SerializeField]
        private bool hideWhenFar;
        public bool HideWhenFar => hideWhenFar;

        [Tooltip("Distance from the camera, in world units, behind which the prop is hidden.")]
        [SerializeField]
        [Min(1f)]
        private float hideDistance = 120f;
        public float HideDistance => hideDistance;

        [Tooltip("Turns the whole class off: it is not placed, not drawn and does not block.")]
        [SerializeField]
        private bool isDisabled;
        public bool IsDisabled => isDisabled;
    }
}
