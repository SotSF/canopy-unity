using System.Collections.Generic;
using BeaconGame;
using SpaceshipGame;
using UnityEngine;

namespace MiniGame
{
    // Coordinator that owns the "current game mode" and routes canvas input to the right
    // game controller. Both SpaceshipGameController and BeaconGameController live alongside
    // this component (typically on the same GameObject) so they can share the game board,
    // camera, and ship prefab; SetMode enables one and disables the other.
    public class MiniGameController : MonoBehaviour
    {
        public static MiniGameController instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState() { instance = null; }

        [SerializeField]
        private GameMode initialMode = GameMode.SpaceshipGame;

        public GameMode CurrentMode { get; private set; }

        void Awake()
        {
            if (instance != null && instance != this)
                Destroy(instance);
            instance = this;
            CurrentMode = initialMode;
        }

        void Start()
        {
            // Deferred to Start so both sub-controllers have finished Awake and registered
            // their singletons.
            SetMode(initialMode);
        }

        // Resolves a controller reference. Falls back to a scene scan that includes inactive
        // GameObjects so we can still find (and later activate) a controller whose GameObject
        // was left disabled in the editor — otherwise its Awake never runs, its .instance
        // stays null, and mode switches silently no-op on it.
        private static T ResolveController<T>(T staticInstance) where T : Component
        {
            if (staticInstance != null)
                return staticInstance;
            return FindFirstObjectByType<T>(FindObjectsInactive.Include);
        }

        // Brings a controller into the "active" state: makes sure the GameObject is active
        // (so Awake runs and .instance registers) and the component itself is enabled.
        private static void ActivateController(Component controller)
        {
            if (controller == null) return;
            if (!controller.gameObject.activeSelf)
                controller.gameObject.SetActive(true);
            var behaviour = controller as MonoBehaviour;
            if (behaviour != null && !behaviour.enabled)
                behaviour.enabled = true;
        }

        public void SetMode(GameMode mode)
        {
            CurrentMode = mode;
            var spaceship = ResolveController(SpaceshipGameController.instance);
            var beacon = ResolveController(BeaconGameController.instance);

            switch (mode)
            {
                case GameMode.SpaceshipGame:
                    if (beacon != null)
                    {
                        beacon.EndGame();
                        beacon.enabled = false;
                    }
                    // Symmetric with the BeaconGame branch: guarantee the target controller
                    // is fully live (GameObject active + component enabled) rather than just
                    // toggling the component. Fixes the case where a mode's controller was
                    // left inactive in the scene setup and would otherwise never come online.
                    ActivateController(spaceship);
                    break;

                case GameMode.BeaconGame:
                    if (spaceship != null)
                    {
                        // Clear canvas-driven ships; web players (if any) are left alone since
                        // their connections aren't managed by the mode switch.
                        spaceship.ReconcileCanvasPlayers(new HashSet<string>());
                        spaceship.enabled = false;
                    }
                    ActivateController(beacon);
                    beacon?.StartGame();
                    break;
            }
        }

        // Routes bundled canvas input to whichever controller is active this frame.
        public void ApplyCanvasInput(SpaceshipGamePlayerData data)
        {
            switch (CurrentMode)
            {
                case GameMode.SpaceshipGame:
                    SpaceshipGameController.instance?.ApplyCanvasInput(data);
                    break;
                case GameMode.BeaconGame:
                    BeaconGameController.instance?.ApplyCanvasInput(data);
                    break;
            }
        }

        public void ReconcileCanvasPlayers(HashSet<string> activeIds)
        {
            switch (CurrentMode)
            {
                case GameMode.SpaceshipGame:
                    SpaceshipGameController.instance?.ReconcileCanvasPlayers(activeIds);
                    break;
                case GameMode.BeaconGame:
                    BeaconGameController.instance?.ReconcileCanvasPlayers(activeIds);
                    break;
            }
        }

        // The board texture is shared across modes when both controllers point at the same
        // RenderTexture, but we still resolve through the active controller so a future
        // split (e.g. a beacon-only render target) doesn't force a node change.
        public RenderTexture GameBoardTex => CurrentMode == GameMode.SpaceshipGame
            ? SpaceshipGameController.instance?.gameBoardTex
            : BeaconGameController.instance?.gameBoardTex;

        public RenderTexture FluidVelocityTex =>
            SpaceshipGameController.instance?.fluidVelocityTex;

        public bool LevelUpThisFrame =>
            CurrentMode == GameMode.BeaconGame
            && (BeaconGameController.instance?.levelUpThisFrame ?? false);

        public bool BeaconCollectedThisFrame =>
            CurrentMode == GameMode.BeaconGame
            && (BeaconGameController.instance?.beaconCollectedThisFrame ?? false);

        // Called by the node after reading pulses this tick so each event is consumed once.
        public void ClearFrameEvents()
        {
            var beacon = BeaconGameController.instance;
            if (beacon != null)
            {
                beacon.levelUpThisFrame = false;
                beacon.beaconCollectedThisFrame = false;
            }
        }
    }
}
