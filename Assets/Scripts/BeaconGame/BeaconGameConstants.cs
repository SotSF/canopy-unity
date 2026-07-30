using UnityEngine;

namespace BeaconGame
{
    public class BeaconGameConstants : Singleton<BeaconGameConstants>
    {
        // Board dimensions (gameBoardSize, boundaryRadius) intentionally live on
        // SpaceshipGameConstants and are read from there — the canopy hardware is a single
        // physical fixture, so both game modes must agree on where the wall is.

        [Header("Ship Movement")]
        public float shipAcceleration = 6f;
        public float topSpeed = 5f;
        public float strafeFactor = 0.25f;
        public float maxRotationSpeed = 180f;
        public float rotationAcceleration = 1440f;
        public float rotationFrictionFactor = 0.002f;
        public float frictionFactor = 0.025f;

        [Header("Beacon")]
        public float beaconCollectionRadius = 0.35f;
        // Fraction of boundaryRadius for spawn band
        public float beaconMinRadiusFraction = 0.15f;
        public float beaconMaxRadiusFraction = 0.85f;
        public float beaconPulseRate = 2f;

        [Header("Scoring")]
        public int pointsPerBeacon = 1;
        public int pointsToLevelUp = 5;

        [Header("Camera Shake")]
        public float shakeIntensity = 0.08f;
        public float shakeDuration = 0.6f;
    }
}
