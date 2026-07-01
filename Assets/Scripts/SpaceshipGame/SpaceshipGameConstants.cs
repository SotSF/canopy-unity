using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;

using WebSocketServer;
namespace SpaceshipGame
{
    public class SpaceshipGameConstants : Singleton<SpaceshipGameConstants>
    {
        public Vector2Int gameBoardSize = new Vector2Int(512, 512);
        public Vector2 gameBoardCenter = new Vector2(255, 255);
        public float velocityScaling = 0.25f;
        // Top ship speed, in meters/second
        public float maxSpeed = 5f;
        // Thrust applied while steering, in meters/second^2
        public float shipAcceleration = 6f;
        // Sideways (strafe) thrust as a fraction of forward thrust; lower = less disorienting
        public float strafeFactor = 0.25f;
        // Top ship turn rate, in degrees/second (angular velocity is clamped to this)
        public float maxRotationSpeed = 180f;
        // How fast the ship builds turn rate toward the steered heading, in degrees/second^2
        public float rotationAcceleration = 1440f;
        // Fraction of turn rate retained after one second; very low = snappy stop when steering ends
        public float rotationFrictionFactor = 0.002f;
        // 16 feet (Canopy physical size) to meters (/2 for radius)
        public float boundaryRadius = 4.8768f / 2;
        // Fraction of ship speed retained after one second (drag); lower = more drag
        public float frictionFactor = 0.025f;
        public float shipDragFactor = 0.005f;

        // Projectile launch speed, in meters/second
        public float projectileInitialSpeed = 1f;
        // Fraction of projectile speed retained after one second (drag); lower = more drag
        public float projectileDragFactor = 0.002f;

        public float respawnStartPlayingVFXTime = 2.5f;
        public float respawnTime = 3f;

        public Vector3 defaultShipScale = new Vector3(2.3f, 2.3f, 2.3f);

        public Dictionary<PlayerType, float> shipTypeStartingHealth = new Dictionary<PlayerType, float> {
            { PlayerType.Web, 5f },
            { PlayerType.Controller, 3f },
            { PlayerType.Oddball, 10f },
            { PlayerType.GenericCanvas, 5f }
        };
    }
}