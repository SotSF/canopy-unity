
using System.Collections.Generic;
using System.Linq;
using System;

using UnityEngine;
using UnityEngine.VFX;
using WebSocketServer;
using LitMotion;

using Random = UnityEngine.Random;
using LitMotion.Extensions;

namespace SpaceshipGame
{
    public class SpaceshipGameController : MonoBehaviour
    {

        public static SpaceshipGameController instance;

        // Fast Enter Play Mode keeps statics alive between sessions; clear the stale
        // singleton ref so Awake repopulates it cleanly on the next play entry.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            instance = null;
        }

        // Single source of truth for every player. A player's live ship (if any) is
        // player.ship; there is deliberately no separate ship dictionary to fall out of
        // sync with it. No more than 32 players.
        private Dictionary<string, SpaceshipGamePlayer> players;

        // Last-seen input state per canvas player: button states for rising-edge ("fire on
        // press") detection, and the last applied color so we only push color changes.
        private Dictionary<string, CanvasPlayerInputState> canvasState;

        private struct CanvasPlayerInputState
        {
            public bool fire;
            public bool altFire;
            public bool colorApplied;
            public Color color;
        }

        public RenderTexture gameBoardTex;
        public RenderTexture fluidVelocityTex;

        public SpaceshipController spaceshipPrefab;
        public GameObject gameBoard;

        public WebSocketServer.WebSocketServer server;

        public VisualEffect absorptionVfx;

        public SpaceshipGamePlayer GetPlayerById(string id)
        {
            return players.TryGetValue(id, out var player) ? player : null;
        }

        public void Awake()
        {
            if (instance != null)
            {
                Destroy(instance);
            }
            instance = this;
            canvasState = new Dictionary<string, CanvasPlayerInputState>();
            players = new Dictionary<string, SpaceshipGamePlayer>();
            fluidVelocityTex = new RenderTexture(SpaceshipGameConstants.Instance.gameBoardSize.x, SpaceshipGameConstants.Instance.gameBoardSize.y, 0);
            fluidVelocityTex.useMipMap = false;
            fluidVelocityTex.autoGenerateMips = false;
            fluidVelocityTex.enableRandomWrite = true;
            fluidVelocityTex.filterMode = FilterMode.Point;
            fluidVelocityTex.wrapModeU = TextureWrapMode.Repeat;
            fluidVelocityTex.wrapModeV = TextureWrapMode.Clamp;
            fluidVelocityTex.Create();
        }

        // 1 byte for event id, 4 bytes for two floats r & theta. Pre-initialize with the position event type representation
        private byte[] shipPositionEventBuffer = new byte[1 + 4 * 2] { (byte)SpaceshipGameEventType.ShipPosition,0,0,0,0,0,0,0,0 }; 
        void Update()
        {
            foreach (var player in players.Values)
            {
                // Only web players have a remote client expecting position events.
                if (!player.IsAlive || player.playerType != PlayerType.Web)
                    continue;
                var id = player.id;
                var ship = player.ship;

                float r = ship.transform.localPosition.magnitude / SpaceshipGameConstants.Instance.boundaryRadius;
                float theta = Mathf.Atan2(ship.transform.localPosition.z, ship.transform.localPosition.x);
                byte[] rBytes = BitConverter.GetBytes(r);
                byte[] thetaBytes = BitConverter.GetBytes(theta);
                Buffer.BlockCopy(rBytes, 0, shipPositionEventBuffer, 1, 4);
                Buffer.BlockCopy(thetaBytes, 0, shipPositionEventBuffer, 5, 4);
                server.SendBinary(id, shipPositionEventBuffer);
            }
        }

        private byte[] gameDataUpdateBuffer = new byte[1 + 2 + 2 + 12];
        public void SendHitEvent(SpaceshipController ship)
        {
            var playerId = ship.player.id;

            gameDataUpdateBuffer[0] = (byte)SpaceshipGameEventType.GameDataUpdate;
            short msgIdx = 0; // "Hit!"
            byte[] msgIdxBytes = BitConverter.GetBytes(msgIdx);
            short gameIdx = 0; // "SpaceshipGame"
            byte[] gameIdxBytes = BitConverter.GetBytes(gameIdx);
            short health = (short)ship.health;
            byte[] healthBytes= BitConverter.GetBytes(health);
            short bufferOffset = 1;
            Buffer.BlockCopy(msgIdxBytes, 0, gameDataUpdateBuffer, bufferOffset, 2);
            bufferOffset += 2;
            Buffer.BlockCopy(gameIdxBytes, 0, gameDataUpdateBuffer, bufferOffset, 2);
            bufferOffset += 2;
            Buffer.BlockCopy(healthBytes, 0, gameDataUpdateBuffer, bufferOffset, 2);
            server.SendBinary(playerId, gameDataUpdateBuffer);
        }

        public void OnShipDestroyed(SpaceshipController ship)
        {
            var player = ship.player;
            // Ignore a stale callback from a ship the player has already moved on from
            // (e.g. an orphaned ship being torn down), so we don't clobber a fresh ship.
            if (player.ship != ship)
                return;
            player.state = PlayerState.Dead;
            player.ship = null;
            Respawn(player);
        }

        // Applies one frame of bundled canvas input to the matching ship.
        public void ApplyCanvasInput(SpaceshipGamePlayerData data)
        {
            if (string.IsNullOrEmpty(data.playerId))
                return;
            var ship = AddCanvasPlayer(data.playerId, data.playerType);
            if (ship == null)
                return;

            ship.OnStickInput(data.leftStick, data.rightStick);

            canvasState.TryGetValue(data.playerId, out var prev);
            var next = prev;

            // Rising-edge so a held button fires once (mirrors the web Press event).
            if (data.fire && !prev.fire)
                ship.OnButtonPress(0);
            if (data.altFire && !prev.altFire)
                ship.OnButtonPress(1);
            next.fire = data.fire;
            next.altFire = data.altFire;

            // Only push color on first sight or when it changes (mirrors the web ChangeColor event).
            if (data.hasColor && (!prev.colorApplied || prev.color != data.color))
            {
                ship.OnUpdateColor(data.color);
                next.colorApplied = true;
                next.color = data.color;
                ship.player.color = data.color;
            }

            canvasState[data.playerId] = next;
        }

        // Destroys canvas ships whose players are no longer being driven (port disconnected or
        // source node removed), so the active id set is the single source of truth each frame.
        public void ReconcileCanvasPlayers(HashSet<string> activeIds)
        {
            // Materialize first: we mutate `players` inside the loop.
            var stale = players.Values
                .Where(p => p.IsCanvas && !activeIds.Contains(p.id))
                .ToList();
            foreach (var player in stale)
            {
                if (player.ship != null)
                    Destroy(player.ship.gameObject);
                players.Remove(player.id);
                canvasState.Remove(player.id);
            }
        }

        struct SpaceshipGameEvent
        {
            SpaceshipGameEventType evt;
            Color32 player;
            float[] data;
        }

        public async void Spawn(SpaceshipGamePlayer player, Vector3 position)
        {
            if (player.state != PlayerState.Spawning)
            {
                Debug.Log("Invalid player state for spawn: " + player.state);
                return;
            }
            if (player.ship == null)
            {
                // First spawn or respawn: build the ship.
                player.ship = SpaceshipController.Create(spaceshipPrefab, gameObject, player, position);
                player.ship.OnUpdateColor(player.color);
                player.ship.DisableControls();
                await LMotion.Create(Vector3.zero, SpaceshipGameConstants.Instance.defaultShipScale, 1f).BindToLocalScale(player.ship.transform);
                player.ship.EnableControls();
            }
            player.state = PlayerState.Alive;
        }

        public void Spawn(SpaceshipGamePlayer player)
        {
            var rotation = Quaternion.Euler(0, Random.Range(0,360), 0);
            var localPos = rotation * Vector3.left * 0.25f * SpaceshipGameConstants.Instance.boundaryRadius;
            Spawn(player, localPos);
        }

        public async void Respawn(SpaceshipGamePlayer player)
        {
            if (player.state != PlayerState.Dead)
            {
                Debug.Log("Invalid player state for spawn: " + player.state);
                return;
            }
            // Wait to start playing absorb vfx
            Debug.Log($"Started waiting at time {Time.time}, will wait {SpaceshipGameConstants.Instance.respawnStartPlayingVFXTime} seconds to do VFX");
            await Awaitable.WaitForSecondsAsync(SpaceshipGameConstants.Instance.respawnStartPlayingVFXTime);
            // Do absorption VFX
            var rotation = Quaternion.Euler(0, Random.Range(0,360), 0);
            var localPos = rotation * Vector3.left * 0.25f * SpaceshipGameConstants.Instance.boundaryRadius;
            VisualEffect vfx = Instantiate(absorptionVfx, transform.position+localPos, Quaternion.Euler(0, 0, 0), transform);
            vfx.SendEvent("OnPlay");
            vfx.SetVector4("PrimaryColor", player.color);
            var remaining = SpaceshipGameConstants.Instance.respawnTime - SpaceshipGameConstants.Instance.respawnStartPlayingVFXTime;
            Debug.Log($"Created vfx at {Time.time}, will wait {remaining} to spawn");
            await Awaitable.WaitForSecondsAsync(remaining);
            vfx.SendEvent("OnStop");
            // The player may have disconnected (OnClose) or been reconciled away during the
            // delay; don't spawn a ship for someone who's no longer registered.
            if (!players.TryGetValue(player.id, out var current) || current != player)
                return;
            player.state = PlayerState.Spawning;
            Spawn(player, localPos);
            await Awaitable.WaitForSecondsAsync(2f);
            Destroy(vfx);
        }

        // Gets (or lazily creates) the ship for a canvas player. Idempotent, so it's safe to
        // call every frame and after a Play restart (when players starts empty again).
        // Returns null while the player has no controllable ship (dead/respawning).
        public SpaceshipController AddCanvasPlayer(string id, PlayerType playerType)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            players.TryGetValue(id, out var player);
            // Mid-respawn (or awaiting first spawn): no controllable ship yet.
            if (player?.state == PlayerState.Dead || player?.state == PlayerState.Spawning)
                return null;
            if (player == null)
            {
                player = new SpaceshipGamePlayer { id = id, state = PlayerState.Spawning, ship = null, playerType = playerType };
                players[id] = player;
                Spawn(player);
                Debug.Log($"Added canvas player with id {id}");
            }
            player.playerType = playerType;
            return player.ship;
        }

        public void AddWebPlayer(string id)
        {
            var state = PlayerState.Spawning;
            var player = new SpaceshipGamePlayer { id = id, state = state, ship = null, playerType = PlayerType.Web };
            players[id] = player;
            Spawn(player);
        }

        public void OnOpen(WebSocketConnection connection)
        {
            AddWebPlayer(connection.id);
            Debug.Log($"Received websocket connection with id {connection.id}");
        }

        public void OnClose(WebSocketConnection connection)
        {
            if (!players.TryGetValue(connection.id, out var leavingPlayer))
                return;
            // May be null if they disconnected mid-respawn; that's fine.
            if (leavingPlayer.ship != null)
                Destroy(leavingPlayer.ship.gameObject);
            players.Remove(connection.id);
            canvasState.Remove(connection.id);
        }

        /*
        Binary format

        EventType.ChangeColor:
        0x00                < Event type
        0x00 0x00 0x00      < Player hex color

        EventType.Press:
        0x00                < Event type
        0x00                < Button id

        EventType.Update:
        0x00                < Event type
        0x00 0x00 0x00 0x00 < float data 0 (lx)
        0x00 0x00 0x00 0x00 < float data 1 (ly)
        0x00 0x00 0x00 0x00 < float data 2 (rx)
        0x00 0x00 0x00 0x00 < float data 3 (ry)

        EventType.GameDataUpdate
        2 bytes (1 short): DisplayMessageId
        2 bytes (1 short): GameID
        12 bytes (unstructured): Game info (health, ammo count, shields, snake length, etc)

        */
        public void OnMessage(WebSocketMessage message)
        {
            //message.connection;
            if (message.text != null)
                Debug.Log(message.text);
            if (message.rawdata != null)
            {
            
                SpaceshipGameEventType evt = (SpaceshipGameEventType)message.rawdata[0];
                var data = message.rawdata;
                var conn = message.connection.id;
                // Player may be mid-respawn (no ship) or already gone; drop input that
                // can't be applied rather than throwing.
                if (!players.TryGetValue(conn, out var player) || player.ship == null)
                    return;
                var ship = player.ship;
                switch (evt)
                {
                    case SpaceshipGameEventType.ChangeColor:
                        var r = data[1];
                        var g = data[2];
                        var b = data[3];
                        Color32 color = new Color32(r, g, b, 255);
                        ship.OnUpdateColor(color);
                        player.color = color;
                        Debug.Log($"Received ColorChange event for conn {conn} to {color}");
                        break;
                    case SpaceshipGameEventType.Update:
                        float data1 = System.BitConverter.ToSingle(data, 1);
                        float data2 = System.BitConverter.ToSingle(data, 5);
                        float data3 = System.BitConverter.ToSingle(data, 9);
                        float data4 = System.BitConverter.ToSingle(data, 13);
                        ship.OnStickInput(new Vector2(data1, data2), new Vector2(data3, data4));
                        //Debug.Log($"Received Update event for conn {conn} with data <{data1:0.00}, {data2:0.00}>, <{data3:0.00}, {data4:0.00}");
                        break;
                    case SpaceshipGameEventType.Press:
                        var buttonId = data[1];
                        ship.OnButtonPress(buttonId);
                        Debug.Log($"Received Press event for conn {conn} for button {buttonId}");
                        break;
                    case SpaceshipGameEventType.Rotate:
                        float radians = System.BitConverter.ToSingle(data, 1);
                        // ship.OnCalibrateRotation(radians);
                        break;
                    case SpaceshipGameEventType.CalibrationStatus:
                        byte status = data[1];
                        ship.OnCalibrationStatus(status);
                        break;
                    case SpaceshipGameEventType.TouchPosition:
                        float touchR = System.BitConverter.ToSingle(data, 1);
                        float touchTheta = System.BitConverter.ToSingle(data, 5);
                        ship.OnTouchInput(touchR, touchTheta);
                        break;
                }
            }
        }
    }

}