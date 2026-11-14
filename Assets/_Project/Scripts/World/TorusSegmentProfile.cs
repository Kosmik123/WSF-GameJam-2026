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

        /// <summary>
        /// Terrain height above the base tube at the given undisplaced point of the torus.
        /// </summary>
        /// <param name="along">Position along the segment: 0 at its start, 1 at its end. Outside 0-1 when a neighbour samples this profile near the border.</param>
        /// <param name="around">Position around the tube: 0-1 (one full turn).</param>
        /// <param name="basePoint">The same place as a point of the base torus, in world units.</param>
        public abstract float SampleHeight(float along, float around, Vector3 basePoint);

        /// <summary>
        /// Vertex color of the terrain at the same place as <see cref="SampleHeight"/>. White by default.
        /// </summary>
        public virtual Color SampleColor(float along, float around, Vector3 basePoint)
        {
            return Color.white;
        }

        /// <summary>Forget cached data. Called before the world is rebuilt, so edited source assets are picked up.</summary>
        public virtual void Invalidate() { }

        /// <summary>
        /// Modifies vertices of a segment mesh made by <see cref="TorusSegmentMeshBuilder"/>
        /// (vertices on the base tube, radial normals, uv.x = position along the segment, uv.y = position around the tube).
        /// Near the segment ends the height and the color are cross-faded with the neighbouring profiles so borders match.
        /// Vertex colors are set for every segment (white where a profile has none).
        /// </summary>
        public virtual void Deform(Mesh mesh, TorusSegmentProfile previous, TorusSegmentProfile next, float blendWidth)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var colors = new Color[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                float height = TorusSegmentBlend.GetHeight(this, previous, next, uvs[i].x, uvs[i].y, blendWidth, vertices[i]);
                colors[i] = TorusSegmentBlend.GetColor(this, previous, next, uvs[i].x, uvs[i].y, blendWidth, vertices[i]);
                vertices[i] += normals[i] * height;
            }

            mesh.vertices = vertices;
            mesh.colors = colors;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}
