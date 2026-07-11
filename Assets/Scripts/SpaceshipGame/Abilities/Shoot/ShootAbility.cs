using UnityEngine;

namespace SpaceshipGame
{
    /// <summary>
    /// Fires one or more projectiles. Covers the whole family of shot patterns:
    /// a single forward shot, a centered forward spread, or a full 360° ring
    /// (e.g. 5 shots at 72°). Shots are always centered on the ship's heading.
    /// </summary>
    [CreateAssetMenu(menuName = "Spaceship/Abilities/Shoot", fileName = "ShootAbility")]
    public class ShootAbility : Ability
    {
        [Tooltip("Number of projectiles fired per activation.")]
        public int numShots = 1;

        [Tooltip("Angular gap in degrees between adjacent shots, centered on the ship's " +
                 "heading. numShots × spacingDegrees = 360 gives an evenly spaced ring " +
                 "(e.g. 5 shots × 72°). Smaller values give a forward spread.")]
        public float spacingDegrees = 72f;

        [Tooltip("Initial launch speed of each projectile, in meters/second.")]
        public float projectileVelocity = 1f;

        [Tooltip("Projectile prefab to spawn for each shot.")]
        public SpaceshipProjectile projectilePrefab;

        public override void Activate(in AbilityContext context)
        {
            SpaceshipController ship = context.ship;
            Transform shipTransform = ship.transform;
            Vector3 spawnPosition = ship.ProjectileSpawnPosition;

            // Center the spread on the heading: shot i is offset by
            // (i - (numShots-1)/2) * spacingDegrees. For a full ring this is
            // equivalent (mod 360) to evenly distributing shots around the circle.
            float center = (numShots - 1) * 0.5f;
            for (int i = 0; i < numShots; i++)
            {
                float angle = (i - center) * spacingDegrees;
                Quaternion offset = Quaternion.Euler(0f, angle, 0f);

                SpaceshipProjectile projectile = Instantiate(
                    projectilePrefab,
                    spawnPosition,
                    shipTransform.rotation * offset,
                    shipTransform.parent);
                projectile.gameObject.SetActive(true);
                projectile.parent = ship;
                projectile.velocity = (offset * shipTransform.forward) * projectileVelocity;
            }
        }
    }
}
