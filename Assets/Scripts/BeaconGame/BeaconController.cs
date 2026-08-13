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
            float pulse = 0.75f + 0.25f * Mathf.Sin(pulseTime * Mathf.PI * 2f);
            transform.localScale = baseScale * pulse;

            if (beaconMaterial != null && beaconMaterial.HasProperty(EmissionColorID))
                beaconMaterial.SetColor(EmissionColorID, emissionColor * (0.5f + 0.5f * pulse));
        }

        void OnDestroy()
        {
            if (beaconMaterial != null)
                Destroy(beaconMaterial);
        }
    }
}
