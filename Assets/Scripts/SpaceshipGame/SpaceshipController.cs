using System.Collections.Generic;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine;
using UnityEngine.VFX;
namespace SpaceshipGame
{

    public class SpaceshipController : MonoBehaviour, IDamageable
    {
        // Turn rate about the Y axis, in degrees/second. Built up by steering, decays when idle.
        // Ship assigned color (typically player's color), mirrored onto fired projectiles.
        public Color shipColor = Color.white;

        public SpaceshipGamePlayerData playerInputData;
        public SpaceshipGamePlayer player;
        
        new public Renderer renderer;
        new public Collider collider;
        
        private bool controllable = true;

        private Vector3 velocity;
        private float angularVelocity;
        public float health = 3;

        public GameObject deathVFXprefab;
        public VisualEffect absorbVFXprefab;

        [Header("Abilities")]
        // Registry mapping each ship type to its starting health + ability loadout.
        // Slot index maps to button id in OnButtonPress (0 = primary fire, 1 = alt fire, ...).
        [Tooltip("Maps each ship type to its health and ability loadout.")]
        public ShipDefinitionRegistry shipDefinitions;

        private readonly List<AbilitySlot> abilitySlots = new List<AbilitySlot>();

        // Muzzle offset above the ship origin; projectiles spawn from here.
        private readonly Vector3 verticalOffset = new Vector3(0, 0.15f, 0);
        public Vector3 ProjectileSpawnPosition => transform.position + verticalOffset;
        
        private Material deathVfxMaterial;
        private Material shipMaterial;

        private float lastHitTime = -1000;

        public static SpaceshipController Create(
            SpaceshipController prefab,
            GameObject gameBoard,
            SpaceshipGamePlayer player,
            Vector3 localPos)
        {
            SpaceshipController ship = Instantiate(prefab, gameBoard.transform);
            ship.velocity = Vector3.zero;
            ship.transform.localPosition = localPos;
            ship.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(ship.transform.localPosition.y,ship.transform.localPosition.x), 0);
            ship.renderer = ship.GetComponentInChildren<MeshRenderer>();
            ship.shipMaterial = ship.renderer.material;
            ship.shipColor = player.color;
            ship.shipMaterial.color = ship.shipColor;
            ship.controllable = true;
            ship.player = player;
            // Applies starting health + ability loadout for the player's type.
            ship.OnShipTypeChange(player.playerType);
            return ship;
        }

        public static SpaceshipController Create(
            SpaceshipController prefab,
            GameObject gameBoard,
            SpaceshipGamePlayer player)
        {
            // Instantiate near edge of game board
            var rotation = Quaternion.Euler(0, Random.Range(0,360), 0);
            var localPos = rotation * Vector3.left * 0.25f * SpaceshipGameConstants.Instance.boundaryRadius;
            var ship = Create(prefab, gameBoard, player, localPos);
            return ship;
        }

        // Apply the health + ability loadout for a ship type, from the registry.
        // Called at spawn and whenever a live ship's type changes.
        public void OnShipTypeChange(PlayerType playerType)
        {
            abilitySlots.Clear();
            ShipDefinition definition = shipDefinitions != null ? shipDefinitions.Get(playerType) : null;
            if (definition == null)
            {
                Debug.LogWarning($"No ShipDefinition registered for {playerType}; ship spawns with no abilities.");
                return;
            }
            health = definition.startingHealth;
            foreach (Ability ability in definition.abilities)
            {
                if (ability != null)
                    abilitySlots.Add(new AbilitySlot(ability));
            }
        }

        public void OnStickInput(Vector2 leftStick, Vector2 rightStick)
        {
            // Right stick steers the heading; left stick throttles thrust along it.
            if (controllable)
            {
                UpdateRotation(rightStick);
                UpdateVelocity(leftStick);
            }
        }

        // Right stick X sets the direction of angular thrust; holding it keeps turning. Its
        // magnitude scales how hard we accelerate the turn. Angular velocity builds and decays
        // (in Update) for a momentum feel, clamped to maxRotationSpeed.
        public void UpdateRotation(Vector2 input)
        {
            angularVelocity += input.x * SpaceshipGameConstants.Instance.rotationAcceleration * Time.deltaTime;
            angularVelocity = Mathf.Clamp(angularVelocity,
                -SpaceshipGameConstants.Instance.maxRotationSpeed,
                SpaceshipGameConstants.Instance.maxRotationSpeed);
        }

        public void UpdateVelocity(Vector2 input)
        {
            // Thrust in ship-local space: Y drives forward/reverse along the heading, X strafes
            // sideways (powerful cold-gas thrusters), scaled down so off-axis travel is less jarring.
            Vector3 localThrust = new Vector3(input.x * SpaceshipGameConstants.Instance.strafeFactor, 0f, input.y);
            Vector3 thrust = transform.localRotation * localThrust;
            velocity += thrust * (SpaceshipGameConstants.Instance.shipAcceleration * Time.deltaTime);
            velocity = Vector3.ClampMagnitude(velocity, SpaceshipGameConstants.Instance.maxSpeed);
        }

        public void OnTouchInput(float r, float theta)
        {
            // Convert polar input to canopy position and compute direction from ship to touch point
            // then use that direction to update velocity
            if (controllable)
            {
                var scaledRadius = r * SpaceshipGameConstants.Instance.boundaryRadius;
                Vector3 targetPosition = new Vector3(scaledRadius * Mathf.Cos(theta), 0, scaledRadius * Mathf.Sin(theta));
                Vector3 direction = targetPosition - transform.localPosition;
                if (direction.sqrMagnitude < 1e-6f)
                    return;
                velocity += direction.normalized * (SpaceshipGameConstants.Instance.shipAcceleration * Time.deltaTime);
                velocity = Vector3.ClampMagnitude(velocity, SpaceshipGameConstants.Instance.maxSpeed);
                // Keep the nose pointed where we're accelerating, so touch feels like stick steering.
                float desiredHeading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                SteerToward(desiredHeading, 1f);
            }
        }

        // Accumulate angular velocity that turns the ship toward desiredHeading (degrees), scaled by
        // steering strength (0..1) and clamped to maxRotationSpeed. Integration and decay run in Update().
        private void SteerToward(float desiredHeading, float strength)
        {
            float error = Mathf.DeltaAngle(transform.localEulerAngles.y, desiredHeading);
            angularVelocity += Mathf.Sign(error) * strength
                * SpaceshipGameConstants.Instance.rotationAcceleration * Time.deltaTime;
            angularVelocity = Mathf.Clamp(angularVelocity,
                -SpaceshipGameConstants.Instance.maxRotationSpeed,
                SpaceshipGameConstants.Instance.maxRotationSpeed);
        }

        public void OnUpdateColor(Color color)
        {
            shipColor = color;
            shipMaterial.color = shipColor;
        }

        public void OnCalibrationStatus(byte status)
        {
            if (status == 0)
            {
                shipMaterial.SetFloat("_Flashing", 1);
            }
            else
            {
                shipMaterial.SetFloat("_Flashing", 0);
            }
        }

        public void OnButtonPress(byte buttonId)
        {
            if (!controllable)
                return;
            // Button id selects the ability slot (0 = primary fire, 1 = alt fire, ...).
            if (buttonId < abilitySlots.Count)
                abilitySlots[buttonId].TryActivate(new AbilityContext(this, player));
        }

        public async void DoDamageFlash()
        {
            shipMaterial.SetFloat("_Flashing", 1);
            await Awaitable.WaitForSecondsAsync(0.7f);
            shipMaterial.SetFloat("_Flashing", 0);
        }

        public void TakeDamage(float damage, IDamageSource source)
        {
            lastHitTime = Time.time;
            if (player.playerType == PlayerType.Web)
            {
                SpaceshipGameController.instance.SendHitEvent(this);
            }
            health -= damage;
            if (health <= 0)
            {
                source.OnScoreKill(this);
                OnDeath();
            }
        }

        private void DoDeathVFX()
        {
            var deathVfx = Instantiate(deathVFXprefab, transform.position, Quaternion.Euler(0, 0, 0), transform.parent);
            var renderer = deathVfx.GetComponent<ParticleSystemRenderer>();
            deathVfx.SetActive(true);
            renderer.material.color = shipColor;
        }

        public void DisableControls()
        {
            controllable = false;
        }

        public void EnableControls()
        {
            controllable = true;
        }

        public async void OnDeath()
        {
            // Do death VFX, respawn?
            DoDeathVFX();
            DisableControls();
            await LMotion.Create(SpaceshipGameConstants.Instance.defaultShipScale, Vector3.zero, 0.75f).BindToLocalScale(transform);
            SpaceshipGameController.instance.OnShipDestroyed(this);
            player.deaths++;
            Destroy(gameObject);
        }

        public void OnDestroy()
        {
            Destroy(deathVfxMaterial);
            Destroy(shipMaterial);
        }
        public void OnScoreHit(SpaceshipController other)
        {
        
        }

        public void OnTriggerEnter(Collider other)
        {
            var otherShip = other.GetComponent<SpaceshipController>();
            var otherProjectile = other.GetComponent<SpaceshipProjectile>();
            if (otherShip != null)
            {
                // Bumped into another ship
            }
            else if (otherProjectile != null)
            {
                if (otherProjectile.parent == this)
                {
                    // Our own projectile, do nothing
                }
                else
                {
                    // We've been shot!!!
                }
            }
        }

        void Update()
        {
            // Advance ability cooldowns.
            for (int i = 0; i < abilitySlots.Count; i++)
                abilitySlots[i].Tick(Time.deltaTime);

            Vector3 positionUpdate = velocity * Time.deltaTime;
            // Continue moving in velocity direction
            transform.localPosition += positionUpdate;

            // Decay velocity (frictionFactor is the fraction of speed retained per second)
            velocity *= Mathf.Pow(SpaceshipGameConstants.Instance.frictionFactor, Time.deltaTime);

            // Spin toward the steered heading, then bleed the turn rate off quickly when steering stops.
            transform.Rotate(0f, angularVelocity * Time.deltaTime, 0f, Space.Self);
            angularVelocity *= Mathf.Pow(SpaceshipGameConstants.Instance.rotationFrictionFactor, Time.deltaTime);

            // Check bounds, bounce off circular boundary at edge
            float distanceFromCenter = transform.localPosition.magnitude;
            if (distanceFromCenter > SpaceshipGameConstants.Instance.boundaryRadius)
            {
                Vector3 normal = (Vector3.zero - transform.localPosition).normalized;
                velocity = Vector3.Reflect(velocity, normal);
                transform.localPosition = transform.localPosition.normalized * SpaceshipGameConstants.Instance.boundaryRadius;
            }

            // Set shader props for hit effect
            var timeSinceLastHit = Time.time - lastHitTime;
            if ( timeSinceLastHit < 1)
            shipMaterial.SetFloat("_TimeSinceLastHit", timeSinceLastHit);
        }
    }
}