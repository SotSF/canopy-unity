using UnityEngine;

public class Rotator : MonoBehaviour
{
    public float rotationSpeed = 0;
    public Vector3 rotationAxis = Vector3.forward;

    private void Update()
    {
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
