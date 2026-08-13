using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BeaconGame
{
    public class BeaconGameController : MonoBehaviour
    {
        public static BeaconGameController instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() { instance = null; }

        private Dictionary<string, BeaconGamePlayer> players;
        private Dictionary<string, bool> prevFireState;

        [Header("Scene References")]
        public RenderTexture gameBoardTex;
        public Camera gameCamera;

        // Historically there was a gameBoard field here paralleling SpaceshipGameController's,
        // but SpaceshipGameController actually parents spawned ships under its own gameObject
        // (see SpaceshipGameController.Spawn passing `gameObject` to SpaceshipController.Create),
        // *not* under the gameBoard field. Beacon-game objects now do the same — parented under
        // `this.gameObject` — so both modes agree on the ships' parent transform and its scale.

        [Header("Prefabs")]
        public BeaconShipController shipPrefab;
        public BeaconController beaconPrefab;
        // Instantiated at beacon position on collection; optional shockwave/celebration VFX.
        public GameObject celebrationVFXPrefab;

        [Header("Ship Appearance")]
        // Scale applied to a spawned beacon-mode ship so its visual size matches what the
        // spaceship game grows ships to via ShipDefinition.defaultScale. Kept on the beacon
        // controller (rather than read from a ShipDefinition) because the beacon game has no
        // per-player-type ship variants.
        public Vector3 shipScale = new Vector3(2.3f, 2.3f, 2.3f);

        [Header("Game State (read-only)")]
        public int sharedPoints;
        public int level = 1;

        // Set when events occur; the node reads them via MiniGameController and clears.
        [HideInInspector] public bool beaconCollectedThisFrame;
        [HideInInspector] public bool levelUpThisFrame;

        private BeaconController activeBeacon;

        void Awake()
        {
            if (instance != null && instance != this)
                Destroy(instance);
            instance = this;
            players = new Dictionary<string, BeaconGamePlayer>();
            prevFireState = new Dictionary<string, bool>();
        }

        // Enters the beacon game: spawns the first beacon and resets state.
        // Called by MiniGameController when switching to BeaconGame mode.
        public void StartGame()
        {
            sharedPoints = 0;
            level = 1;
            beaconCollectedThisFrame = false;
            levelUpThisFrame = false;
            if (activeBeacon == null)
                SpawnBeacon();
        }

        // Tears down beacon-game state so the mode leaves nothing running.
        // Called by MiniGameController before switching away from BeaconGame mode.
        public void EndGame()
        {
            if (activeBeacon != null)
            {
                Destroy(activeBeacon.gameObject);
                activeBeacon = null;
            }
            ReconcileCanvasPlayers(new HashSet<string>());
            beaconCollectedThisFrame = false;
            levelUpThisFrame = false;
        }

        void Update()
        {
            if (activeBeacon == null) return;
            foreach (var player in players.Values)
            {
                if (!player.IsAlive) continue;
                float dist = Vector3.Distance(
                    player.ship.transform.position,
                    activeBeacon.transform.position);
                if (dist <= BeaconGameConstants.Instance.beaconCollectionRadius)
                {
                    OnBeaconCollected(player);
                    return;
                }
            }
        }

        private void OnBeaconCollected(BeaconGamePlayer collector)
        {
            var beaconPos = activeBeacon.transform.position;
            Destroy(activeBeacon.gameObject);
            activeBeacon = null;

            if (celebrationVFXPrefab != null)
                Instantiate(celebrationVFXPrefab, beaconPos, Quaternion.identity, transform);

            sharedPoints += BeaconGameConstants.Instance.pointsPerBeacon;
            beaconCollectedThisFrame = true;

            if (sharedPoints >= level * BeaconGameConstants.Instance.pointsToLevelUp)
            {
                level++;
                levelUpThisFrame = true;
                if (gameCamera != null)
                    StartCoroutine(DoCameraShake());
            }

            SpawnBeacon();
        }

        private IEnumerator DoCameraShake()
        {
            var cam = gameCamera;
            Vector3 originalPos = cam.transform.localPosition;
            float elapsed = 0f;
            float duration = BeaconGameConstants.Instance.shakeDuration;
            float intensity = BeaconGameConstants.Instance.shakeIntensity;
            while (elapsed < duration)
            {
                float t = 1f - (elapsed / duration);
                cam.transform.localPosition = originalPos + new Vector3(
                    Random.Range(-1f, 1f) * intensity * t,
                    0f,
                    Random.Range(-1f, 1f) * intensity * t);
                elapsed += Time.deltaTime;
                yield return null;
            }
            cam.transform.localPosition = originalPos;
        }

        private void SpawnBeacon()
        {
            if (beaconPrefab == null) return;
            var consts = BeaconGameConstants.Instance;
            // Board dimensions live on SpaceshipGameConstants (single source of truth for the
            // canopy fixture); BeaconGameConstants only holds beacon-specific tuning.
            float boundaryRadius = SpaceshipGame.SpaceshipGameConstants.Instance.boundaryRadius;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(consts.beaconMinRadiusFraction, consts.beaconMaxRadiusFraction)
                * boundaryRadius;
            Vector3 localPos = new Vector3(r * Mathf.Cos(angle), 0f, r * Mathf.Sin(angle));
            activeBeacon = Instantiate(beaconPrefab, transform);
            activeBeacon.transform.localPosition = localPos;
        }

        // Applies one frame of canvas input to the matching player's ship, creating it if needed.
        public void ApplyCanvasInput(SpaceshipGamePlayerData data)
        {
            if (string.IsNullOrEmpty(data.playerId)) return;
            var ship = EnsureCanvasPlayer(data.playerId);
            if (ship == null) return;

            ship.OnStickInput(data.leftStick, data.rightStick);

            prevFireState.TryGetValue(data.playerId, out bool prevFire);
            prevFireState[data.playerId] = data.fire;

            if (data.hasColor)
            {
                ship.OnUpdateColor(data.color);
                ship.player.color = data.color;
            }
        }

        // Removes canvas players whose ports have been disconnected or removed.
        public void ReconcileCanvasPlayers(HashSet<string> activeIds)
        {
            var stale = players.Values
                .Where(p => !activeIds.Contains(p.id))
                .ToList();
            foreach (var player in stale)
            {
                if (player.ship != null) Destroy(player.ship.gameObject);
                players.Remove(player.id);
                prevFireState.Remove(player.id);
            }
        }

        // Returns the live ship for a canvas player, spawning one on first call.
        private BeaconShipController EnsureCanvasPlayer(string id)
        {
            if (!players.TryGetValue(id, out var player))
            {
                player = new BeaconGamePlayer
                {
                    id = id,
                    state = BeaconPlayerState.Spawning,
                    color = Color.white
                };
                players[id] = player;
                SpawnPlayer(player);
            }
            return player.ship;
        }

        private void SpawnPlayer(BeaconGamePlayer player)
        {
            if (shipPrefab == null) return;
            float boundaryRadius = SpaceshipGame.SpaceshipGameConstants.Instance.boundaryRadius;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = 0.25f * boundaryRadius;
            Vector3 localPos = new Vector3(r * Mathf.Cos(angle), 0f, r * Mathf.Sin(angle));
            player.ship = BeaconShipController.Create(shipPrefab, gameObject, player, localPos);
            player.ship.transform.localScale = shipScale;
            player.state = BeaconPlayerState.Alive;
        }
    }
}
