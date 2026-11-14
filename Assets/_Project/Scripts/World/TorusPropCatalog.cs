using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace WSFGameJam2026.World
{
    /// <summary>
    /// All prop definitions of the game. Finds the definition of a prop by its mesh name (without the variant suffix).
    /// Filled by the menu WSF GameJam/Setup/Create Prop Definitions.
    /// </summary>
    [CreateAssetMenu(menuName = "WSF GameJam/World/Prop Catalog")]
    public class TorusPropCatalog : ScriptableObject
    {
        private static readonly Regex VariantSuffix = new Regex(@"_v\d+$");

        [SerializeField]
        private List<TorusPropDefinition> definitions = new List<TorusPropDefinition>();

        private Dictionary<string, TorusPropDefinition> lookup;

        /// <summary>Mesh name without the variant suffix: <c>Las_Drzewo_v2</c> becomes <c>Las_Drzewo</c>.</summary>
        public static string GetKey(string meshName)
        {
            return VariantSuffix.Replace(meshName ?? string.Empty, string.Empty);
        }

        public bool TryGetDefinition(string meshName, out TorusPropDefinition definition)
        {
            if (lookup == null)
                BuildLookup();

            return lookup.TryGetValue(GetKey(meshName), out definition);
        }

        private void BuildLookup()
        {
            lookup = new Dictionary<string, TorusPropDefinition>();
            foreach (var definition in definitions)
            {
                if (definition && string.IsNullOrEmpty(definition.MeshName) == false)
                    lookup[definition.MeshName] = definition;
            }
        }

        private void OnValidate()
        {
            lookup = null;
        }
    }
}
