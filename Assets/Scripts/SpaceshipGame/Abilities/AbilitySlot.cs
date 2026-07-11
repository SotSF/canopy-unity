namespace SpaceshipGame
{
    /// <summary>
    /// Per-ship runtime state for one equipped ability: the shared
    /// <see cref="Ability"/> definition plus this ship's own cooldown timer.
    /// One slot per equipped ability; a ship holds a list of these.
    /// </summary>
    public class AbilitySlot
    {
        public readonly Ability ability;
        public float cooldownRemaining;

        public AbilitySlot(Ability ability)
        {
            this.ability = ability;
        }

        /// <summary>
        /// Activate the ability if it is off cooldown. Returns true if it fired.
        /// </summary>
        public bool TryActivate(in AbilityContext context)
        {
            if (ability == null || cooldownRemaining > 0f)
                return false;
            ability.Activate(in context);
            cooldownRemaining = ability.cooldown;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (cooldownRemaining > 0f)
                cooldownRemaining -= deltaTime;
        }
    }
}
