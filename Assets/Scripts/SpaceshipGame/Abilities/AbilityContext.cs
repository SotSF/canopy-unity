namespace SpaceshipGame
{
    /// <summary>
    /// Everything an ability needs to act, passed in at activation time so that
    /// abilities stay decoupled from <see cref="SpaceshipController"/> internals.
    /// Read spawn geometry from the ship (e.g. <c>ship.transform</c>,
    /// <c>ship.ProjectileSpawnPosition</c>).
    /// </summary>
    public readonly struct AbilityContext
    {
        public readonly SpaceshipController ship;
        public readonly SpaceshipGamePlayer player;

        public AbilityContext(SpaceshipController ship, SpaceshipGamePlayer player)
        {
            this.ship = ship;
            this.player = player;
        }
    }
}
