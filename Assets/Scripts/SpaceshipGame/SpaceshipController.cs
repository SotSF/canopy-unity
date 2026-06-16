using UnityEngine;

public class SpaceshipController : MonoBehaviour, IDamageable
{
    public enum PlayerType
    {
        Web,
        Controller,
        Oddball,
        GenericCanvas
    }
    private Vector3 velocity;
    // Turn rate about the Y axis, in degrees/second. Built up by steering, decays when idle.
    private float angularVelocity;
    // Player's assigned color, mirrored onto fired projectiles.
    public Color playerColor = Color.white;
    private Gradient playerGradient;
    public SpaceshipProjectile projectilePrefab;
    public GameObject deathVFXprefab;

    public string id;
    public bool isCanvasPlayer = false;

    // Velocity along polar axes, ie radial (in/out) speed and circumferential (around circle speed)
    private Vector2 polarVelocity;
    private bool calibrated;

    new public Renderer renderer;
    new public Collider collider;
    public float health = 3;
    private PlayerType playerType;
    private bool controllable = true;

    public short score = 0;
    public short deaths = 0;

    private Material deathVfxMaterial;
    private Material shipMaterial;

    public static SpaceshipController Create(SpaceshipController prefab, GameObject gameBoard, PlayerType playerType)
    {
        SpaceshipController ship = Instantiate(prefab, gameBoard.transform);
        ship.velocity = Vector3.zero;
        // Instantiate near edge of game board
        var rotation = Quaternion.Euler(0, 0, 0);
        ship.transform.localPosition = rotation * Vector3.left * 0.15f * SpaceshipGameConstants.Instance.boundaryRadius;
        ship.calibrated = false;
        ship.playerType = playerType;
        ship.renderer = ship.GetComponent<Renderer>();
        ship.shipMaterial = ship.renderer.material;
        ship.shipMaterial.color = ship.playerColor;
        ship.controllable = true;
        return ship;
    }

    public void Respawn()
    {

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
        playerColor = color;
        shipMaterial.color = playerColor;
        // Solid gradient: both ends are the player color, so the trail matches the ship.
        playerGradient = new Gradient();
        playerGradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(playerColor, 0f), new GradientColorKey(playerColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(playerColor.a, 0f), new GradientAlphaKey(playerColor.a, 1f) }
        );
    }

    public void OnCalibrationStatus(byte status)
    {
        if (status == 0)
        {
            calibrated = false;
            shipMaterial.SetFloat("_Flashing", 1);
        }
        else
        {
            calibrated = true;
            shipMaterial.SetFloat("_Flashing", 0);
        }
    }

    public void OnCalibrateRotation(float angleRadians)
    {
        // transform.localPosition = Quaternion.Euler(0, angleRadians*Mathf.Rad2Deg, 0) * transform.localPosition;
    }

    public void OnButtonPress(byte buttonId)
    {
        if (controllable)
        {
            FireProjectile();
        }
    }

    public void TakeDamage(float damage, IDamageSource source)
    {
        Debug.Log($"Take damage called with {damage} damage, health from {health} to {health-damage}");
        if (playerType == PlayerType.Web)
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
        var deathVfxMaterial = renderer.material;
        deathVfx.gameObject.SetActive(true);
        deathVfxMaterial.color = playerColor;
    }

    public void DisableControls()
    {
        controllable = false;
    }

    public void OnDeath()
    {
        // Do death VFX, respawn?
        DoDeathVFX();
        DisableControls();
        deaths++;
        Destroy(this.gameObject);
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

    Vector3 verticalOffset = new Vector3(0,0.15f, 0);
    public void FireProjectile()
    {
        SpaceshipProjectile projectile = Instantiate(projectilePrefab, 
            transform.position + verticalOffset,
            transform.rotation,
            transform.parent
        );

        // projectile.line.colorGradient = playerGradient;

        projectile.gameObject.SetActive(true);
        projectile.parent = this;
        projectile.velocity = transform.forward * SpaceshipGameConstants.Instance.projectileInitialSpeed;
    }

    void Update()
    {
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
    }
}
