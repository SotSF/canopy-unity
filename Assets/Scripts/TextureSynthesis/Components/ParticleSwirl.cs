using UnityEngine;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleSwirl : MonoBehaviour
{
    int lastcount = 0;
    ParticleSystem sys;
    ParticleSystem.Particle[] particles;

    float beganSlowing;

    Vector3[] velocities;
    
    public void BeginSlowing()
    {
        beganSlowing = Time.time;
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

    void Start()
    {
        sys = GetComponent<ParticleSystem>();
        InitializeParticleArray();
    }

    IEnumerator WiggleExplode(float duration)
    {

        //    var radial = velocityModule.radial;
        //    radial.mode = ParticleSystemCurveMode.Constant;
        InitializeParticleArray();
        sys.GetParticles(particles);
        Vector3[] startPositions = new Vector3[particles.Length];
        for (int i = 0; i < particles.Length; i++)
        {
            startPositions[i] = particles[i].position;
        }
        float began = Time.time;
        while (Time.time < began + duration)
        {


            //radial.constant = 
            //Debug.Log(radial.constant);
            //velocityModule.radial = radial;
            var normalizedTime = Mathf.InverseLerp(began, began + duration, Time.time);
            var oscillator = Mathf.Sin((2 * Mathf.PI * normalizedTime) * (7));
            //Debug.LogFormat("Oscillator: {0}, normTime: {1}", oscillator, normalizedTime);
            sys.GetParticles(particles);
            for (int i = 0; i < particles.Length; i++)
            {
                var particle = particles[i];
                float distance = Vector3.Distance(particle.position, transform.position);
                Vector3 normalizedDirection = (particle.position - transform.position).normalized;
                particle.position = startPositions[i] + (oscillator / 10) * (startPositions[i] - transform.position);
                particle.remainingLifetime = 2;
                particles[i] = particle;
            }
            sys.SetParticles(particles, particles.Length);
            yield return null;
        }
        StartCoroutine(Outwards());
    }

    private IEnumerator Outwards()
    {
        float start = Time.time;
        while (Time.time < start + 2)
        {
            var emission = sys.emission;
            emission.enabled = false;
            var velocityModule = sys.velocityOverLifetime;
            velocityModule.enabled = true;
            velocityModule.speedModifier = 7;
            var radial = velocityModule.radial;
            radial.mode = ParticleSystemCurveMode.Constant;
            radial.constant = 1;
            velocityModule.radial = radial;
            yield return null;
        }
    }

    private IEnumerator SlowToStop(float slowtime)
    {
        var velocityModule = sys.velocityOverLifetime;
        float began = Time.time;
        float startspeed = velocityModule.speedModifier.constant;
        while (Time.time < began + slowtime + 0.02f)
        {
            float normalizedStoppingTime = Mathf.InverseLerp(began, began + slowtime, Time.time);
            velocityModule.speedModifier = Mathf.Lerp(startspeed, 0, normalizedStoppingTime);
            yield return null;
        }
        var orbital = velocityModule.orbitalY;
        orbital.constant = 0;
        velocityModule.orbitalY = orbital;
        velocityModule.enabled = false;
    }

    public void Slow(float duration)
    {
        StartCoroutine(SlowToStop(duration));
    }

    public void Explode(float duration)
    {
        StartCoroutine(WiggleExplode(duration));
    }

    private void InitializeParticleArray()
    {
        particles = new ParticleSystem.Particle[sys.particleCount];
        lastcount = sys.particleCount;
    }
}
