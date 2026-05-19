using UnityEngine;

public class SpaceshipProjectile : MonoBehaviour
{

    public Vector3 velocity;
    public LineRenderer line;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.localPosition += velocity;
        velocity *= SpaceshipGameConstants.Instance.projectileDragFactor;
        line.SetPosition(0, transform.localPosition);
        if (transform.localPosition.magnitude > SpaceshipGameConstants.Instance.boundaryRadius)
        {
            Destroy(this);
        }
    }
}
