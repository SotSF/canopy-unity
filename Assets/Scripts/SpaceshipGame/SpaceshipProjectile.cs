using UnityEngine;

public class SpaceshipProjectile : MonoBehaviour, IDamageSource
{

    public Vector3 velocity;
    public LineRenderer line;
    public SpaceshipController parent;


    new public Collider collider;

    public void OnTriggerEnter(Collider other)
    {
        var otherDamageable = other.GetComponent<IDamageable>();
        var otherProjectile = other.GetComponent<SpaceshipProjectile>();
        if (otherShip != null)
        {
            // Hit a ship
        }
        else if (otherProjectile != null)
        {
            // Hit a projectile

        }
    }

    // Update is called once per frame
    void Update()
    {
        transform.localPosition += velocity * Time.deltaTime;
        // projectileDragFactor is the fraction of speed retained per second
        velocity *= Mathf.Pow(SpaceshipGameConstants.Instance.projectileDragFactor, Time.deltaTime);
        // line.SetPosition(0, transform.localPosition);
        if (transform.localPosition.magnitude > SpaceshipGameConstants.Instance.boundaryRadius)
        {
            Destroy(this.gameObject);
        }
    }
}
