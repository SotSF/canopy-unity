using UnityEngine;

public class SpaceshipProjectile : MonoBehaviour, IDamageSource
{

    public Vector3 velocity;
    public LineRenderer line;
    public SpaceshipController parent;
    public float damageAmount = 1;

    public GameObject VFXPrefab;


    new public Collider collider;

    public void OnTriggerEnter(Collider other)
    {
        var otherShip = other.GetComponent<SpaceshipController>();
        if (otherShip == parent)
        {
            // Owning ship, do nothing
            return;
        }
        var otherDamageable = other.GetComponent<IDamageable>();
        if (otherDamageable != null)
        {
            // Hit a damageable target
            Debug.Log($"Projectile hit {otherDamageable}, dealing {damageAmount} damage");
            otherDamageable.TakeDamage(damageAmount, this);
            DoVFX();
            Destroy(this.gameObject);
        }
    }
    private void DoVFX()
    {
        var vfx = Instantiate(VFXPrefab, transform.position, Quaternion.Euler(90, 0, 0), transform.parent);
        var particleMaterial = vfx.GetComponent<ParticleSystemRenderer>().material;
        particleMaterial.color = parent.playerColor;
        
        //particle.renderer
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
