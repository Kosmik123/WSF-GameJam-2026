using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// Describes how one snake body segment is shaped. Derive from it to add a new kind of terrain:
    /// implement <see cref="SampleHeight"/> or override <see cref="Deform"/> to modify the vertices directly.
    /// </summary>
    public abstract class TorusSegmentProfile : ScriptableObject
    {
        [Header("Mesh")]
        [Tooltip("Number of mesh subdivisions along the snake body. Borders between segments always match.")]
        [SerializeField]
        [Range(2, 256)]
        private int lengthResolution = 32;
        public int LengthResolution => lengthResolution;

        [Tooltip("Optional. Used instead of the world's default material.")]
        [SerializeField]
        private Material materialOverride;
        public Material MaterialOverride => materialOverride;

        /// <summary>Terrain height above the base tube at the given undisplaced point of the torus.</summary>
        public abstract float SampleHeight(Vector3 basePoint);

        /// <summary>
        /// Modifies vertices of a segment mesh made by <see cref="TorusSegmentMeshBuilder"/>
        /// (vertices on the base tube, radial normals, uv.x = position along the segment).
        /// Near the segment ends the height is cross-faded with the neighbouring profiles so borders match.
        /// </summary>
        public virtual void Deform(Mesh mesh, TorusSegmentProfile previous, TorusSegmentProfile next, float blendWidth)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;

            for (int i = 0; i < vertices.Length; i++)
            {
                float height = TorusSegmentBlend.GetHeight(this, previous, next, uvs[i].x, blendWidth, vertices[i]);
                vertices[i] += normals[i] * height;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
