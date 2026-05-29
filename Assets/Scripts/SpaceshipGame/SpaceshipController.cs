using UnityEngine;

public class SpaceshipController : MonoBehaviour
{
    private Vector3 velocity;
    public SpaceshipProjectile projectilePrefab;

    // Velocity along polar axes, ie radial (in/out) speed and circumferential (around circle speed)
    private Vector2 polarVelocity;
    private bool calibrated;

    new public Renderer renderer;

    public static SpaceshipController Create(SpaceshipController prefab, GameObject gameBoard)
    {
        SpaceshipController ship = Instantiate(prefab, gameBoard.transform);
        ship.velocity = Vector3.zero;
        // Instantiate near edge of game board
        var rotation = Quaternion.Euler(0, 0, 0);
        ship.transform.localPosition = rotation * Vector3.right * 0.75f * SpaceshipGameConstants.Instance.boundaryRadius;
        ship.calibrated = false;
        return ship;
    }

    public void OnStickInput(Vector2 leftStick, Vector2 rightStick)
    {
        UpdateVelocity(leftStick);
        // Right stick could be used for rotation or special actions
    }

    public void UpdateVelocity(Vector2 input)
    {
        velocity += new Vector3(input.x, 0, input.y)*SpaceshipGameConstants.Instance.velocityScalingFactor;
        velocity = Vector3.ClampMagnitude(velocity, SpaceshipGameConstants.Instance.maxSpeed);
        //transform.localRotation = Vector3.RotateTowards(transform.)
    }

    public void OnTouchInput(float r, float theta)
    {
        // Convert polar input to canopy position and compute direction from ship to touch point
        // then use that direction to update velocity
        var scaledRadius = r * SpaceshipGameConstants.Instance.boundaryRadius;
        Vector3 targetPosition = new Vector3(scaledRadius * Mathf.Cos(theta), 0, scaledRadius * Mathf.Sin(theta));
        Vector3 direction = targetPosition - transform.localPosition;
        velocity += direction.normalized* SpaceshipGameConstants.Instance.velocityScalingFactor;
        velocity = Vector3.ClampMagnitude(velocity, SpaceshipGameConstants.Instance.maxSpeed);
    }

    public void OnUpdateColor(Color color)
    {
        if (renderer != null)
        {
            renderer.material.SetColor("_Color", color);
        }
    }

    public void OnCalibrationStatus(byte status)
    {
        if (status == 0)
        {
            calibrated = false;
            renderer.material.SetFloat("_Flashing", 1);
        }
        else
        {
            calibrated = true;
            renderer.material.SetFloat("_Flashing", 0);
        }
    }

    public void OnCalibrateRotation(float angleRadians)
    {
        transform.localPosition = Quaternion.Euler(0, angleRadians*Mathf.Rad2Deg, 0) * transform.localPosition;
    }

    public void OnButtonPress(byte buttonId)
    {
        Debug.Log($"Got button press {buttonId.ToString()}");
        FireProjectile();
    }

    public void OnDoSpecialAction()
    {

    }

    public void FireProjectile()
    {
        SpaceshipProjectile projectile = Instantiate(projectilePrefab, 
            transform.position,
            transform.rotation,
            transform.parent);
        projectile.gameObject.SetActive(true);
        projectile.velocity = transform.forward * SpaceshipGameConstants.Instance.projectileInitialSpeed;

    }

    void Update()
    {
        Vector3 positionUpdate = velocity * Time.deltaTime;
        // Continue moving in velocity direction
        transform.localPosition += positionUpdate;

        // Decay velocity
        velocity *= SpaceshipGameConstants.Instance.frictionFactor;

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
