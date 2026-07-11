using System.Collections.Generic;
using UnityEngine;

namespace SpaceshipGame
{
    /// <summary>
    /// Maps each <see cref="PlayerType"/> to its <see cref="ShipDefinition"/>. Assigned
    /// on the ship prefab so the health and loadout for every ship type are authored in
    /// one place, editable without code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "Spaceship/Ship Definition Registry", fileName = "ShipDefinitionRegistry")]
    public class ShipDefinitionRegistry : ScriptableObject
    {
        [Tooltip("One entry per ship type.")]
        public List<ShipDefinition> definitions = new List<ShipDefinition>();

        /// <summary>Returns the definition for the given type, or null if none is registered.</summary>
        public ShipDefinition Get(PlayerType playerType)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].playerType == playerType)
                    return definitions[i];
            }
            return null;
        }
    }
}
