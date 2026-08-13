using UnityEngine;

namespace BeaconGame
{
    // Lightweight movement-only ship for the beacon game: no health, no abilities, no death.
    public class BeaconShipController : MonoBehaviour
    {
        public Color shipColor = Color.white;
        public BeaconGamePlayer player;

        new private Renderer renderer;
        private Material shipMaterial;
        private bool controllable = true;
        private Vector3 velocity;
        private float angularVelocity;

        public static BeaconShipController Create(
            BeaconShipController prefab,
            GameObject gameBoard,
            BeaconGamePlayer player,
            Vector3 localPos)
        {
            var ship = Instantiate(prefab, gameBoard.transform);
            // The shared ship prefab also carries a SpaceshipController. Force both to a known
            // state — self enabled, sibling disabled — regardless of the prefab's default,
            // so spawning always yields a working ship for beacon mode. (If the prefab had
            // this component disabled, only disabling the sibling would leave the ship dead.)
            ship.enabled = true;
            var spaceshipMode = ship.GetComponent<SpaceshipGame.SpaceshipController>();
            if (spaceshipMode != null)
                spaceshipMode.enabled = false;
            ship.transform.localPosition = localPos;
            ship.renderer = ship.GetComponentInChildren<Renderer>();
            ship.shipMaterial = ship.renderer != null ? ship.renderer.material : null;
            ship.shipColor = player.color;
            if (ship.shipMaterial != null)
                ship.shipMaterial.color = ship.shipColor;
            ship.controllable = true;
            ship.player = player;
            return ship;
        }

        public void OnStickInput(Vector2 leftStick, Vector2 rightStick)
        {
            if (!controllable) return;
            var consts = BeaconGameConstants.Instance;
            // Right stick steers heading
            angularVelocity += rightStick.x * consts.rotationAcceleration * Time.deltaTime;
            angularVelocity = Mathf.Clamp(angularVelocity, -consts.maxRotationSpeed, consts.maxRotationSpeed);
            // Left stick thrusts in ship-local space; X is strafe (scaled down)
            Vector3 localThrust = new Vector3(leftStick.x * consts.strafeFactor, 0f, leftStick.y);
            Vector3 thrust = transform.localRotation * localThrust;
            velocity += thrust * (consts.shipAcceleration * Time.deltaTime);
            velocity = Vector3.ClampMagnitude(velocity, consts.topSpeed);
        }

        public void OnUpdateColor(Color color)
        {
            shipColor = color;
            if (shipMaterial != null)
                shipMaterial.color = shipColor;
        }

        public void EnableControls() => controllable = true;
        public void DisableControls() => controllable = false;

        void Update()
        {
            var consts = BeaconGameConstants.Instance;

            transform.localPosition += velocity * Time.deltaTime;
            velocity *= Mathf.Pow(consts.frictionFactor, Time.deltaTime);

            transform.Rotate(0f, angularVelocity * Time.deltaTime, 0f, Space.Self);
            angularVelocity *= Mathf.Pow(consts.rotationFrictionFactor, Time.deltaTime);

            // Bounce off the circular boundary. Board dimensions come from SpaceshipGameConstants
            // (single source of truth for the physical canopy fixture) so both modes agree.
            float boundaryRadius = SpaceshipGame.SpaceshipGameConstants.Instance.boundaryRadius;
            float dist = transform.localPosition.magnitude;
            if (dist > boundaryRadius)
            {
                Vector3 normal = -transform.localPosition.normalized;
                velocity = Vector3.Reflect(velocity, normal);
                transform.localPosition = transform.localPosition.normalized * boundaryRadius;
            }
        }

        void OnDestroy()
        {
            if (shipMaterial != null)
                Destroy(shipMaterial);
        }
    }
}
