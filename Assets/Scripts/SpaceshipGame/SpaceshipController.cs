using UnityEngine;

public class SpaceshipController : MonoBehaviour
{
    public static GameObject GameField;
    public static GameObject shipPrefab;
    public GameObject shipInstance;

    private Vector3 velocity;
    public const float VELOCITYSCALE = 5f;
    // 16 feet (Canopy physical size) to meters (/2 for radius)
    public const float BOUNDARYRADIUS = 4.8768f / 2;
    public const float FRICTIONFACTOR = 0.98f;

    public Color shipColor;

    public static void CreateNewShip()
    {

    }

    void Start()
    {
        shipColor = Random.ColorHSV(0, 1, 0.5f, 1, 0.5f, 1);
        OnUpdateColor(shipColor);
    }

    void OnUpdateXAxisControl(float input)
    {
        float newXVelocity = input * VELOCITYSCALE;
        velocity += new Vector3(input, 0, 0);
    }

    void OnUpdateYAxisControl(float playerInput)
    {
        float newYVelocity = playerInput * VELOCITYSCALE;
        velocity += new Vector3(0, playerInput, 0);
    }

    void OnUpdateColor(Color color)
    {
        if (shipInstance != null)
        {
            Renderer renderer = shipInstance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.SetColor("_EmissionColor", color);
            }
        }
    }

    void OnDoSpecialAction()
    {

    }

    void Update()
    {
        // Continue moving in velocity direction
        transform.position += velocity * Time.deltaTime;

        // Decay velocity
        velocity *= FRICTIONFACTOR;

        // Check bounds, bounce off circular boundary at edge
        float distanceFromCenter = transform.position.magnitude;
        if (distanceFromCenter > BOUNDARYRADIUS)
        {
            Vector3 normal = (Vector3.zero - transform.position).normalized;
            velocity = Vector3.Reflect(velocity, normal);
            transform.position = transform.position.normalized * BOUNDARYRADIUS;
        }
    }
}
