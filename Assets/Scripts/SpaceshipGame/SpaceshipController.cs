using UnityEngine;

public class SpaceshipController : MonoBehaviour
{
    private Vector3 velocity;
    public Color shipColor;
    public SpaceshipProjectile projectilePrefab;

    public static SpaceshipController Create(SpaceshipController prefab, GameObject gameBoard)
    {
        SpaceshipController ship = Instantiate(prefab, gameBoard.transform);
        ship.velocity = Vector3.zero;
        ship.transform.localPosition = Vector3.zero;
        return ship;
    }

    void Start()
    {
        //shipColor = Random.ColorHSV(0, 1, 0.5f, 1, 0.5f, 1);
        //OnUpdateColor(shipColor);
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
    }


    public void OnUpdateColor(Color color)
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.SetColor("_EmissionColor", color);
        }

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
