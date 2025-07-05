using UnityEngine;
using System.Collections;
using System.Linq;

public class Rotator : MonoBehaviour
{
    public float rotationSpeed = 0;
    public Vector3 rotationAxis = Vector3.forward;
    public Transform rotationTarget;

    void Start()
    {

    }
    private void Update()
    {
        rotationTarget.transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
