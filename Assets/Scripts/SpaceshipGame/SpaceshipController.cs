using UnityEngine;

public class SpaceshipController : MonoBehaviour
{
    public GameObject shipPrefab;
    public GameObject shipInstance;

    private Vector3 velocity;
    public const float VELOCITYSCALE = 5f;


    void Start()
    {

    }

    void OnUpdateXAxis(float input)
    {
        float newXVelocity = input * VELOCITYSCALE;
        velocity += new Vector3(input, 0, 0);
    }

    void OnUpdateYAxis(float playerInput)

    void Update()
    {
        // Continue moving in velocity direction
        transform.position += velocity * Time.deltaTime;
    }
}
