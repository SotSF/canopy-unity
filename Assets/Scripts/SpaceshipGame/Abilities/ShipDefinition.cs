using System.Collections.Generic;
using UnityEngine;

namespace SpaceshipGame
{
    /// <summary>
    /// Per-ship-type configuration: the starting health and ability loadout for one
    /// <see cref="PlayerType"/>. Author one asset per ship type and collect them in a
    /// <see cref="ShipDefinitionRegistry"/>. This replaces the old hardcoded health
    /// dictionary and the type-branching ability selection.
    /// </summary>
    [CreateAssetMenu(menuName = "Spaceship/Ship Definition", fileName = "ShipDefinition")]
    public class ShipDefinition : ScriptableObject
    {
        [Tooltip("The player/control type this definition applies to.")]
        public PlayerType playerType;

        [Tooltip("Health a freshly spawned ship of this type starts with.")]
        public float startingHealth = 3f;

        [Tooltip("Energy a freshly spawned ship of this type starts with.")]
        public float startingEnergy = 10f;

        public float topSpeed = 5f;

        public Vector3 defaultScale = new Vector3(2.3f, 2.3f, 2.3f);

        [Tooltip("Prefab with ship model etc")]
        public SpaceshipController shipPrefab;

        [Tooltip("Abilities equipped, in slot order. Slot 0 = primary fire, 1 = alt fire, ...")]
        public List<Ability> abilities = new List<Ability>();
    }
}
