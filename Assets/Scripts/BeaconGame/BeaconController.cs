using System.Collections.Generic;
using UnityEngine;

namespace BeaconGame
{
    // A pulsing pickup that ships navigate toward. Assign a prefab with a visible renderer.
    public class BeaconController : MonoBehaviour
    {
        private float pulseTime;
        private Vector3 baseScale;
        private Material beaconMaterial;
        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        // Base emission color; set by BeaconGameController after instantiation if desired.
        public Color emissionColor = Color.white;

        void Awake()
        {
            baseScale = transform.localScale;
            var r = GetComponentInChildren<Renderer>();
            if (r != null)
                beaconMaterial = r.material;
        }

        void Update()
        {
            pulseTime += Time.deltaTime * BeaconGameConstants.Instance.beaconPulseRate;
            float pulse = 0.75f + 0.1f * Mathf.Sin(pulseTime * Mathf.PI * 2f);
            transform.localScale = baseScale * pulse;

            if (beaconMaterial != null && beaconMaterial.HasProperty(EmissionColorID))
                beaconMaterial.SetColor(EmissionColorID, emissionColor * (0.5f + 0.5f * pulse));
        }

        // Moves away from the alive ships at the given speed, staying in the XZ plane and
        // inside the beacon spawn band's outer radius. Called by BeaconGameController only
        // when evasion is enabled, so a stationary beacon pays no per-frame cost here.
        // All positions are in the shared game-board local space (ships and beacon are
        // parented under the same controller GameObject).
        public void Evade(Dictionary<string, BeaconGamePlayer>.ValueCollection players, float speed)
        {
            Vector3 pos = transform.localPosition;
            Vector3 flee = Vector3.zero;
            foreach (var p in players)
            {
                if (!p.IsAlive) continue;
                Vector3 away = pos - p.ship.transform.localPosition;
                away.y = 0f;
                float sqrDist = away.sqrMagnitude;
                if (sqrDist < 1e-6f) continue;
                // Inverse-square weighting: the nearest ship dominates the flee direction.
                flee += away / sqrDist;
            }
            // Zero when there are no alive ships, or the beacon sits exactly between them.
            if (flee.sqrMagnitude < 1e-8f) return;

            Vector3 next = pos + flee.normalized * (speed * Time.deltaTime);
            float maxRadius = SpaceshipGame.SpaceshipGameConstants.Instance.boundaryRadius
                * BeaconGameConstants.Instance.beaconMaxRadiusFraction;
            // Radially clamping the stepped position makes the beacon slide along the rim
            // rather than pin against it when the flee direction points outward.
            Vector3 planar = new Vector3(next.x, 0f, next.z);
            if (planar.sqrMagnitude > maxRadius * maxRadius)
                planar = planar.normalized * maxRadius;
            transform.localPosition = new Vector3(planar.x, pos.y, planar.z);
        }

        void OnDestroy()
        {
            if (beaconMaterial != null)
                Destroy(beaconMaterial);
        }
    }
}
