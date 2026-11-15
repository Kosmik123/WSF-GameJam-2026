using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Settings of one biome prop mesh. The catalog finds the definition by the mesh itself, the name of the prop is the name of the mesh.
    /// Props without a definition use the defaults (no blocking, always visible).
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Prop Definition")]
    public class TorusPropDefinition : ScriptableObject
    {
        [Tooltip("The mesh of the prop. The catalog finds the definition by it.")]
        [SerializeField]
        private Mesh mesh;
        public Mesh Mesh => mesh;

        /// <summary>Name of the prop: the name of its mesh.</summary>
        public string MeshName => mesh ? mesh.name : name;

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
