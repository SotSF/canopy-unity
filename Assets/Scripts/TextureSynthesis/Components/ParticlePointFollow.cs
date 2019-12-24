using UnityEngine;
using System.Collections;

[RequireComponent(typeof(ParticleSystem))]
public class ParticlePointFollow : MonoBehaviour
{
    public enum ParticleState
    {
        Ramping,
        Slowing,
        Exploding
    }

    public float distanceSpeedFactor = 0.01f;
    int lastcount = 0;
    ParticleSystem sys;
    ParticleSystem.Particle[] particles;
    ParticleState state;

    float beganSlowing;

    Vector3[] velocities;
    
    public void BeginSlowing()
    {
        beganSlowing = Time.time;
        state = ParticleState.Slowing;
        var emission = sys.emission;
        emission.enabled = false;
        velocities = new Vector3[sys.particleCount];
        InitializeParticleArray();
        sys.GetParticles(particles);
        for (int i = 0; i < particles.Length; i++)
        {
            velocities[i] = particles[i].velocity;
        }
    }

    public void Freeze()
    {
        state = ParticleState.Exploding;
    }

    void Start()
    {
        state = ParticleState.Ramping;
        sys = GetComponent<ParticleSystem>();
        InitializeParticleArray();
    }

    void Update()
    {
        if (sys.particleCount == 0 && state == ParticleState.Exploding)
            return;
        if (sys.particleCount != lastcount)
        {
            InitializeParticleArray();
        }
        sys.GetParticles(particles);
        switch (state)
        {
            case ParticleState.Ramping:
                MoveTowardCenterpoint();
                break;
            case ParticleState.Slowing:
                SlowToStop();
                break;
            case ParticleState.Exploding:
                ExplodeParticles();
                break;
        }
        sys.SetParticles(particles, particles.Length);
    }

    void ExplodeParticles()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            var particle = particles[i];
            float distance = Vector3.Distance(particle.position, transform.position);
            Vector3 normalizedDirection = (particle.position - transform.position).normalized;
            particle.velocity += (normalizedDirection/75) / (distance * distance);
            particles[i] = particle;
        }
    }

    void AddParticleLifetime(float life=2)
    {
        for (int i = 0; i < particles.Length; i++)
        {
            var particle = particles[i];
            particle.remainingLifetime = life;
            particles[i] = particle;
        }
    }

    private void SlowToStop()
    {
        //Slow down wub time before drop
        var x = sys.sizeOverLifetime;
        x.enabled = false;
        float slowTime = 1.33f;
        float normalizedStoppingTime = Mathf.InverseLerp(beganSlowing, beganSlowing + slowTime, Time.time);
        for (int i = 0; i < particles.Length; i++)
        {
            x.enabled = false;
            var particle = particles[i];
            float distance = Vector3.Distance(particle.position, transform.position);
            if (distance > 0.01f)
            {
                particle.velocity = Vector3.Lerp(velocities[i], Vector3.zero, normalizedStoppingTime);
            } else
            {
                particle.velocity = Vector3.zero;
            }
            particle.remainingLifetime = 2;
            particles[i] = particle;
        }
    }

    private void MoveTowardCenterpoint()
    {
        for (int i = 0; i < particles.Length; i++)
        {
            var particle = particles[i];
            float distance = Vector3.Distance(particle.position, transform.position);
            if (distance > 0.01f)
            {
                var speed = particle.velocity.magnitude;
                particle.velocity = Mathf.Clamp(distance * distanceSpeedFactor + speed, 0, 20) * (transform.position - particle.position).normalized;
            }
            else
            {
                particle.remainingLifetime = 0;
            }
            particles[i] = particle;
        }
    }

    private void InitializeParticleArray()
    {
        particles = new ParticleSystem.Particle[sys.particleCount];
        lastcount = sys.particleCount;
    }
}
