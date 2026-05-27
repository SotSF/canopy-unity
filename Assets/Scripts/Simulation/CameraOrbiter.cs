using UnityEngine;

// Drives this.transform along a Lissajous figure traced on a sphere centered
// at targetPoint. Azimuth and elevation are independent sine oscillators;
// their frequency ratio + phase offset define the figure's shape. Radius
// modulation is an optional third dimension (default off).
public class CameraOrbiter : MonoBehaviour
{
    public Transform targetPoint;
    [Tooltip("Re-orient transform to face target each frame.")]
    public bool lookAtTarget = true;

    [Header("Time")]
    [Tooltip("If true, t is driven by Time.time. Uncheck to scrub t manually.")]
    public bool trackGametime = true;
    public float t;
    [Tooltip("Base rate (Hz). All *Ratio fields multiply this.")]
    public float frequencyHz = 0.05f;

    [Header("Orbit Geometry")]
    [Tooltip("Mean distance from target.")]
    public float radius = 5f;
    [Tooltip("Fractional radius oscillation (0 = constant radius, 1 = full in/out breathing).")]
    [Range(0f, 1f)] public float radiusAmplitude = 0f;
    public float radiusRatio = 1f;
    public float radiusPhaseDegrees = 0f;

    [Header("Azimuth (horizontal sweep around target's up axis)")]
    public float azimuthCenterDegrees = 0f;
    [Tooltip("Peak deviation from center, in degrees. 180 sweeps the full circle each way.")]
    public float azimuthAmplitudeDegrees = 180f;
    [Tooltip("Frequency multiplier on frequencyHz. Classical Lissajous: small integers.")]
    public float azimuthRatio = 1f;
    public float azimuthPhaseDegrees = 0f;

    [Header("Elevation (angle above horizontal plane)")]
    [Range(-89f, 89f)] public float elevationCenterDegrees = 20f;
    public float elevationAmplitudeDegrees = 30f;
    public float elevationRatio = 2f;
    [Tooltip("90 with 1:1 ratio = circle, 0 = degenerate line, in between = ellipse / figure-eight.")]
    public float elevationPhaseDegrees = 90f;

    [Header("Gizmo")]
    public bool drawPath = true;
    [Range(1, 16)] public int gizmoCycles = 4;
    [Range(64, 2048)] public int gizmoSamples = 512;

    Vector3 SampleOffset(float time)
    {
        float w = 2f * Mathf.PI * frequencyHz;

        float azDeg = azimuthCenterDegrees + azimuthAmplitudeDegrees *
            Mathf.Sin(w * azimuthRatio * time + azimuthPhaseDegrees * Mathf.Deg2Rad);
        float elDeg = elevationCenterDegrees + elevationAmplitudeDegrees *
            Mathf.Sin(w * elevationRatio * time + elevationPhaseDegrees * Mathf.Deg2Rad);
        // Clamp away from poles so LookAt with world-up stays stable.
        elDeg = Mathf.Clamp(elDeg, -89f, 89f);

        float r = radius * (1f + radiusAmplitude *
            Mathf.Sin(w * radiusRatio * time + radiusPhaseDegrees * Mathf.Deg2Rad));

        float az = azDeg * Mathf.Deg2Rad;
        float el = elDeg * Mathf.Deg2Rad;
        float cosEl = Mathf.Cos(el);

        return new Vector3(
            r * cosEl * Mathf.Sin(az),
            r * Mathf.Sin(el),
            r * cosEl * Mathf.Cos(az)
        );
    }

    void Update()
    {
        if (trackGametime) t = Time.time;
        if (targetPoint == null) return;

        transform.position = targetPoint.position + SampleOffset(t);
        if (lookAtTarget) transform.LookAt(targetPoint.position, Vector3.up);
    }

    void OnDrawGizmosSelected()
    {
        if (!drawPath || targetPoint == null || frequencyHz <= 0f) return;

        Vector3 origin = targetPoint.position;
        float duration = gizmoCycles / frequencyHz;

        Gizmos.color = Color.cyan;
        Vector3 prev = origin + SampleOffset(0f);
        for (int i = 1; i <= gizmoSamples; i++)
        {
            float ti = (i / (float)gizmoSamples) * duration;
            Vector3 cur = origin + SampleOffset(ti);
            Gizmos.DrawLine(prev, cur);
            prev = cur;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, 0.15f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(origin + SampleOffset(t), 0.15f);
    }
}
