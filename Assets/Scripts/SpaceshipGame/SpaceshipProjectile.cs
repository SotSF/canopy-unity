using UnityEngine;
namespace SpaceshipGame
{
    
public class SpaceshipProjectile : MonoBehaviour, IDamageSource
{

    public Vector3 velocity;
    public LineRenderer line;
    public SpaceshipController parent;
    public float damageAmount = 1;

    public GameObject VFXPrefab;


    new public Collider collider;
    private Material[] impactVfxMaterials;
    private Material projectileMaterial;

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
            DoVFX(other.ClosestPoint(transform.position));
            Destroy(this.gameObject);
        }
    }

    void Start()
    {
        projectileMaterial = line.material;
        projectileMaterial.color = parent.playerColor;
    }

    public void OnScoreKill(IDamageable target)
    {
        parent.score++;
    }
    private void DoVFX(Vector3 point)
    {
        var impactVfx = Instantiate(VFXPrefab, point, Quaternion.Euler(90, 0, 0), transform.parent);
        var renderer = impactVfx.GetComponent<ParticleSystemRenderer>();
        impactVfx.gameObject.SetActive(true);
        impactVfxMaterials = renderer.materials;
        foreach (var impactMaterial in impactVfxMaterials)
        {
            impactMaterial.color = parent.playerColor;
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

    void OnDestroy()
    {
        Destroy(projectileMaterial);
    }
}

}