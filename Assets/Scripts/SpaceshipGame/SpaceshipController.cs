using UnityEngine;

public class SpaceshipController : MonoBehaviour
{
    public static GameObject gameField;
    public static GameObject shipPrefab;

    private Vector3 velocity;
    public const float VELOCITYSCALE = .5f;
    // 16 feet (Canopy physical size) to meters (/2 for radius)
    public const float BOUNDARYRADIUS = 4.8768f / 2;
    public const float FRICTIONFACTOR = 0.99f;

    public Color shipColor;

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
        velocity += new Vector3(input.x, 0, input.y)*VELOCITYSCALE;
        velocity = Vector3.ClampMagnitude(velocity, SpaceshipGameController.instance.maxSpeed);
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
    }

    public void OnDoSpecialAction()
    {

    }

    void Update()
    {
        Vector3 positionUpdate = velocity * Time.deltaTime;
        // Continue moving in velocity direction
        transform.localPosition += positionUpdate;

        // Decay velocity
        velocity *= FRICTIONFACTOR;

        // Check bounds, bounce off circular boundary at edge
        float distanceFromCenter = transform.localPosition.magnitude;
        if (distanceFromCenter > BOUNDARYRADIUS)
        {
            Vector3 normal = (Vector3.zero - transform.localPosition).normalized;
            velocity = Vector3.Reflect(velocity, normal);
            transform.localPosition = transform.localPosition.normalized * BOUNDARYRADIUS;
        }
    }
}
