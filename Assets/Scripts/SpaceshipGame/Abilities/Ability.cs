using UnityEngine;

namespace SpaceshipGame
{
    /// <summary>
    /// Base definition for a ship ability. A concrete subclass is authored as a
    /// ScriptableObject asset that holds the ability's tuning and knows how to
    /// activate itself.
    ///
    /// Assets are shared across every ship that equips them, so this class holds
    /// definition + behavior ONLY. Per-ship runtime state (cooldown timers, active
    /// durational effects) lives outside the asset in <see cref="AbilitySlot"/> or
    /// in MonoBehaviours the ability spawns. Never store mutable per-ship state here.
    /// </summary>
    public abstract class Ability : ScriptableObject
    {
        [Tooltip("Display name for this ability.")]
        public string abilityName;

        [Tooltip("Seconds before this ability can be used again after activation.")]
        public float cooldown = 0f;

        /// <summary>
        /// Perform the ability. For instantaneous abilities this does the work
        /// directly; for durational ones it spawns a runtime MonoBehaviour that
        /// owns the ongoing behavior and reads its config back from this asset.
        /// </summary>
        public abstract void Activate(in AbilityContext context);
    }
}
