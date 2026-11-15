using System.Collections.Generic;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// All prop definitions of the game. Finds the definition of a prop by its mesh.
    /// Filled by the menu WSF GameJam/Setup/Create Prop Definitions.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Prop Catalog")]
    public class TorusPropCatalog : ScriptableObject
    {
        [SerializeField]
        private List<TorusPropDefinition> definitions = new List<TorusPropDefinition>();

        private Dictionary<Mesh, TorusPropDefinition> lookup;

        public bool TryGetDefinition(Mesh mesh, out TorusPropDefinition definition)
        {
            if (lookup == null)
                BuildLookup();

            return lookup.TryGetValue(mesh, out definition);
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<Mesh, TorusPropDefinition>();
            foreach (var definition in definitions)
            {
                if (definition && definition.Mesh)
                    lookup[definition.Mesh] = definition;
            }
        }

        private void OnValidate()
        {
            lookup = null;
        }
    }
}
