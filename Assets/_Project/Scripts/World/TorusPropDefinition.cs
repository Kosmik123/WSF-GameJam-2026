using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Settings of one class of biome props (all variants of a mesh, so <c>Las_Drzewo_v0</c>...<c>_v3</c> share one definition named <c>Las_Drzewo</c>).
    /// The catalog finds the definition by the mesh name. Props without a definition use the defaults (no blocking, always visible).
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

        [Tooltip("How far from the player along the body (theta, degrees, plus and minus) the prop is drawn. 180 = always visible (trees), 0 = the class is turned off completely: not placed, not drawn, does not block. Small props such as grass get a small value.")]
        [SerializeField]
        [Range(0f, 180f)]
        private float visibleRangeDegrees = 180f;
        public float VisibleRangeDegrees => visibleRangeDegrees;

        public bool IsDisabled => visibleRangeDegrees <= 0f;
    }
}
